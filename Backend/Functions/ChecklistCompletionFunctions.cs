using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VolunteerCheckin.Functions.Helpers;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Functions;

/// <summary>
/// Functions for completing and uncompleting checklist items, plus reporting.
/// Separated from CRUD and query operations for better maintainability.
/// </summary>
public class ChecklistCompletionFunctions
{
    private readonly ILogger<ChecklistCompletionFunctions> _logger;
    private readonly IChecklistItemRepository _checklistItemRepository;
    private readonly IChecklistCompletionRepository _checklistCompletionRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IAdminUserRepository _adminUserRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly ChecklistContextHelper _contextHelper;

    public ChecklistCompletionFunctions(
        ILogger<ChecklistCompletionFunctions> logger,
        IChecklistItemRepository checklistItemRepository,
        IChecklistCompletionRepository checklistCompletionRepository,
        IMarshalRepository marshalRepository,
        ILocationRepository locationRepository,
        IAdminUserRepository adminUserRepository,
        IAssignmentRepository assignmentRepository,
        IAreaRepository areaRepository,
        IEventRoleRepository eventRoleRepository)
    {
        _logger = logger;
        _checklistItemRepository = checklistItemRepository;
        _checklistCompletionRepository = checklistCompletionRepository;
        _marshalRepository = marshalRepository;
        _locationRepository = locationRepository;
        _adminUserRepository = adminUserRepository;
        _assignmentRepository = assignmentRepository;
        _contextHelper = new ChecklistContextHelper(assignmentRepository, locationRepository, areaRepository, eventRoleRepository, marshalRepository);
    }

#pragma warning disable MA0051
    [Function("CompleteChecklistItem")]
    public async Task<IActionResult> CompleteChecklistItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "checklist-items/{eventId}/{itemId}/complete")] HttpRequest req,
        string eventId,
        string itemId)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CompleteChecklistItemRequest? request = JsonSerializer.Deserialize<CompleteChecklistItemRequest>(body,
                FunctionHelpers.JsonOptions);

            if (request == null || string.IsNullOrWhiteSpace(request.MarshalId))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidRequest });
            }

            // Get the checklist item
            ChecklistItemEntity? item = await _checklistItemRepository.GetAsync(eventId, itemId);
            if (item == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorChecklistItemNotFound });
            }

            // Build marshal context
            ChecklistScopeHelper.MarshalContext context = await _contextHelper.BuildMarshalContextAsync(eventId, request.MarshalId);

            // Build checkpoint lookup dictionary for area-based matching
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
            Dictionary<string, LocationEntity> checkpointLookup = allLocations.ToDictionary(l => l.RowKey);

            // Verify permission to complete
            if (!ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup))
            {
                _logger.LogWarning("Marshal {MarshalId} attempted to complete checklist item {ItemId} without permission", request.MarshalId, itemId);
                return new ForbidResult();
            }

            // Get marshal details for denormalization (context owner)
            MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, request.MarshalId);
            if (marshal == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorMarshalNotFound });
            }

            // Determine context for completion FIRST (before checking for existing completions)
            // This allows us to check the specific context from the request
            string contextType;
            string contextId;
            if (!string.IsNullOrEmpty(request.ContextType) && !string.IsNullOrEmpty(request.ContextId))
            {
                contextType = request.ContextType;
                contextId = request.ContextId;
            }
            else
            {
                (contextType, contextId, _) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);
            }

            // Get all completions for this item
            List<ChecklistCompletionEntity> itemCompletions =
                [.. await _checklistCompletionRepository.GetByItemAsync(eventId, itemId)];

            // Check if already completed in this specific context
            // For linked tasks and personal scopes at checkpoints, we need to check by context (not just marshal ID)
            // because the same marshal can complete the same task at different checkpoints
            bool isAlreadyCompleted;
            if (item.LinksToCheckIn || contextType == Constants.ChecklistContextCheckpoint)
            {
                // Check for completion in this specific checkpoint context for this marshal
                isAlreadyCompleted = itemCompletions.Any(c =>
                    c.ContextOwnerMarshalId == request.MarshalId &&
                    c.CompletionContextType == contextType &&
                    c.CompletionContextId == contextId &&
                    !c.IsDeleted);
            }
            else
            {
                // Use standard context-based check for other scopes
                isAlreadyCompleted = ChecklistScopeHelper.IsItemCompletedInContext(item, context, checkpointLookup, itemCompletions);
            }

            if (isAlreadyCompleted)
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorChecklistAlreadyCompleted });
            }

            // For linked tasks, ensure we use Checkpoint context with the checkpoint ID
            // This handles cases where the context is Personal (from DetermineCompletionContext or old frontend data)
            if (item.LinksToCheckIn && contextType != Constants.ChecklistContextCheckpoint)
            {
                // Try to find the checkpoint from request or assigned locations
                string? checkpointId = null;
                if (request.ContextType == Constants.ChecklistContextCheckpoint && !string.IsNullOrEmpty(request.ContextId))
                {
                    checkpointId = request.ContextId;
                }
                if (string.IsNullOrEmpty(checkpointId))
                {
                    checkpointId = context.AssignedLocationIds.FirstOrDefault();
                }
                if (!string.IsNullOrEmpty(checkpointId))
                {
                    contextType = Constants.ChecklistContextCheckpoint;
                    contextId = checkpointId;
                }
            }

            // Determine actor (who is actually performing this action)
            // Priority: ActorMarshalId (if provided) > Admin email header
            // This ensures that when a user is both admin and marshal, completing a task
            // as an area lead uses their marshal identity, not their admin identity.
            string actorType;
            string actorId;
            string actorName;
            string adminEmail = req.Headers[Constants.AdminEmailHeader].ToString();

            if (!string.IsNullOrWhiteSpace(request.ActorMarshalId))
            {
                // A marshal (e.g., area lead) is completing this on behalf of another marshal
                // Prioritize this over admin email to use the correct identity
                MarshalEntity? actorMarshal = await _marshalRepository.GetAsync(eventId, request.ActorMarshalId);
                if (actorMarshal == null)
                {
                    return new NotFoundObjectResult(new { message = "Actor marshal not found" });
                }

                actorType = Constants.ActorTypeMarshal;
                actorId = request.ActorMarshalId;
                actorName = actorMarshal.Name;
            }
            else if (!string.IsNullOrWhiteSpace(adminEmail))
            {
                // Admin is completing this on behalf of the marshal (no ActorMarshalId provided)
                actorType = Constants.ActorTypeEventAdmin;
                actorId = adminEmail;

                // Try to get admin name, default to email if not found or empty
                AdminUserEntity? admin = await _adminUserRepository.GetByEmailAsync(adminEmail);
                actorName = !string.IsNullOrWhiteSpace(admin?.Name) ? admin.Name : adminEmail;
            }
            else
            {
                // Marshal is completing their own task
                actorType = Constants.ActorTypeMarshal;
                actorId = request.MarshalId;
                actorName = marshal.Name;
            }

            // Create completion record
            string completionId = Guid.NewGuid().ToString();
            string partitionKey = ChecklistCompletionEntity.CreatePartitionKey(eventId);
            string rowKey = ChecklistCompletionEntity.CreateRowKey(itemId, completionId);
            ChecklistCompletionEntity completion = new()
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                EventId = eventId,
                ChecklistItemId = itemId,
                CompletionContextType = contextType,
                CompletionContextId = contextId,
                ContextOwnerMarshalId = request.MarshalId,
                ContextOwnerMarshalName = marshal.Name,
                ActorType = actorType,
                ActorId = actorId,
                ActorName = actorName,
                CompletedAt = DateTime.UtcNow
            };

            await _checklistCompletionRepository.AddAsync(completion);

            _logger.LogInformation("Checklist item {ItemId} completed by {ActorType} {ActorName} (context owner: {ContextOwnerName}) at {CompletedAt}", itemId, actorType, actorName, marshal.Name, completion.CompletedAt);

            // Handle linked check-in: if LinksToCheckIn is true and this is a checkpoint context, check in the marshal
            LinkedCheckInInfo? linkedCheckIn = null;
            if (item.LinksToCheckIn && contextType == Constants.ChecklistContextCheckpoint && !string.IsNullOrEmpty(contextId))
            {
                linkedCheckIn = await TryCheckInMarshalAsync(eventId, request.MarshalId, contextId, actorName);
            }

            return new OkObjectResult(completion.ToResponse(linkedCheckIn));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing checklist item");
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Attempts to check in a marshal at a checkpoint when completing a linked task.
    /// Returns LinkedCheckInInfo if check-in occurred, null otherwise.
    /// </summary>
    private async Task<LinkedCheckInInfo?> TryCheckInMarshalAsync(string eventId, string marshalId, string checkpointId, string checkedInBy)
    {
        try
        {
            // Find the marshal's assignment at this checkpoint
            IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
            AssignmentEntity? assignment = assignments.FirstOrDefault(a => a.LocationId == checkpointId);

            if (assignment == null || assignment.IsCheckedIn)
            {
                return null; // No assignment or already checked in
            }

            // Get the location for the response and to update count
            LocationEntity? location = await _locationRepository.GetAsync(eventId, checkpointId);
            if (location == null)
            {
                return null;
            }

            // Check in the marshal
            assignment.IsCheckedIn = true;
            assignment.CheckInTime = DateTime.UtcNow;
            assignment.CheckInMethod = "Task"; // Special method indicating check-in via task completion
            assignment.CheckedInBy = checkedInBy;

            await _assignmentRepository.UpdateAsync(assignment);

            // Update location checked-in count with retry logic
            location.CheckedInCount++;
            bool locationUpdated = await FunctionHelpers.ExecuteWithRetryAsync(
                () => _locationRepository.UpdateAsync(location),
                maxRetries: 3,
                baseDelayMs: 100
            );

            if (!locationUpdated)
            {
                _logger.LogWarning("Failed to update location count after linked check-in for assignment {AssignmentId}", assignment.RowKey);
                // Don't rollback - the check-in is the primary operation
            }

            _logger.LogInformation("Marshal {MarshalId} checked in at {LocationName} via linked task completion", marshalId, location.Name);

            return new LinkedCheckInInfo(
                assignment.RowKey,
                checkpointId,
                location.Name,
                assignment.CheckInTime.Value
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during linked check-in for marshal {MarshalId} at checkpoint {CheckpointId}", marshalId, checkpointId);
            return null; // Don't fail the completion if check-in fails
        }
    }
#pragma warning restore MA0051

#pragma warning disable MA0051
    [Function("UncompleteChecklistItem")]
    public async Task<IActionResult> UncompleteChecklistItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "checklist-items/{eventId}/{itemId}/uncomplete")] HttpRequest req,
        string eventId,
        string itemId)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CompleteChecklistItemRequest? request = JsonSerializer.Deserialize<CompleteChecklistItemRequest>(body,
                FunctionHelpers.JsonOptions);

            if (request == null || string.IsNullOrWhiteSpace(request.MarshalId))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidRequest });
            }

            // Get the checklist item
            ChecklistItemEntity? item = await _checklistItemRepository.GetAsync(eventId, itemId);
            if (item == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorChecklistItemNotFound });
            }

            // Build marshal context
            ChecklistScopeHelper.MarshalContext context = await _contextHelper.BuildMarshalContextAsync(eventId, request.MarshalId);

            // Build checkpoint lookup dictionary for area-based matching
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
            Dictionary<string, LocationEntity> checkpointLookup = allLocations.ToDictionary(l => l.RowKey);

            // Determine context for completion
            (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

            // Get all completions for this item
            List<ChecklistCompletionEntity> itemCompletions =
                [.. await _checklistCompletionRepository.GetByItemAsync(eventId, itemId)];

            // Find the completion to uncomplete
            ChecklistCompletionEntity? completion;

            // For linked tasks or checkpoint contexts, we need to match by context type/id
            // because there can be multiple completions per marshal (one per checkpoint)
            if (item.LinksToCheckIn || contextType == Constants.ChecklistContextCheckpoint)
            {
                // Use request context if provided, otherwise use determined context
                string lookupContextType = !string.IsNullOrEmpty(request.ContextType) ? request.ContextType : contextType;
                string lookupContextId = !string.IsNullOrEmpty(request.ContextId) ? request.ContextId : contextId;

                completion = itemCompletions.FirstOrDefault(c =>
                    c.ContextOwnerMarshalId == request.MarshalId &&
                    c.CompletionContextType == lookupContextType &&
                    c.CompletionContextId == lookupContextId &&
                    !c.IsDeleted);
            }
            else if (ChecklistScopeHelper.IsPersonalScope(matchedScope))
            {
                completion = itemCompletions.FirstOrDefault(c =>
                    c.ContextOwnerMarshalId == request.MarshalId &&
                    !c.IsDeleted);
            }
            else
            {
                completion = itemCompletions.FirstOrDefault(c =>
                    c.CompletionContextType == contextType &&
                    c.CompletionContextId == contextId &&
                    !c.IsDeleted);
            }

            if (completion == null)
            {
                return new NotFoundObjectResult(new { message = "Completion not found" });
            }

            // Determine actor (who is actually performing this action)
            // Priority: ActorMarshalId (if provided) > Admin email header
            // This ensures that when a user is both admin and marshal, uncompleting a task
            // as an area lead uses their marshal identity, not their admin identity.
            string actorType;
            string actorId;
            string actorName;
            string adminEmail = req.Headers[Constants.AdminEmailHeader].ToString();

            if (!string.IsNullOrWhiteSpace(request.ActorMarshalId))
            {
                // A marshal (e.g., area lead) is uncompleting this
                // Prioritize this over admin email to use the correct identity
                MarshalEntity? actorMarshal = await _marshalRepository.GetAsync(eventId, request.ActorMarshalId);
                if (actorMarshal == null)
                {
                    return new NotFoundObjectResult(new { message = "Actor marshal not found" });
                }

                actorType = Constants.ActorTypeMarshal;
                actorId = request.ActorMarshalId;
                actorName = actorMarshal.Name;
            }
            else if (!string.IsNullOrWhiteSpace(adminEmail))
            {
                // Admin is uncompleting this (no ActorMarshalId provided)
                actorType = Constants.ActorTypeEventAdmin;
                actorId = adminEmail;

                // Try to get admin name, default to email if not found or empty
                AdminUserEntity? admin = await _adminUserRepository.GetByEmailAsync(adminEmail);
                actorName = !string.IsNullOrWhiteSpace(admin?.Name) ? admin.Name : adminEmail;
            }
            else
            {
                // Marshal is uncompleting their own task
                MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, request.MarshalId);
                if (marshal == null)
                {
                    return new NotFoundObjectResult(new { message = Constants.ErrorMarshalNotFound });
                }

                actorType = Constants.ActorTypeMarshal;
                actorId = request.MarshalId;
                actorName = marshal.Name;
            }

            // Soft delete the completion
            completion.IsDeleted = true;
            completion.UncompletedAt = DateTime.UtcNow;
            completion.UncompletedByActorType = actorType;
            completion.UncompletedByActorId = actorId;
            completion.UncompletedByActorName = actorName;
#pragma warning disable CS0618 // Keep legacy field populated for backwards compatibility
            completion.UncompletedByAdminEmail = actorType == Constants.ActorTypeEventAdmin ? actorId : string.Empty;
#pragma warning restore CS0618

            await _checklistCompletionRepository.UpdateAsync(completion);

            _logger.LogInformation("Checklist item {ItemId} uncompleted for marshal {MarshalId} by {ActorType} {ActorName}", itemId, request.MarshalId, actorType, actorName);

            // Handle linked check-out: if LinksToCheckIn is true, check out the marshal
            // Use the lookup context (from request or determined) since the stored completion may have legacy context type
            if (item.LinksToCheckIn)
            {
                // For linked tasks, determine the checkpoint ID
                // Priority: request context > completion context > determined context
                string? checkpointId = null;
                if (!string.IsNullOrEmpty(request.ContextId) && request.ContextType == Constants.ChecklistContextCheckpoint)
                {
                    checkpointId = request.ContextId;
                }
                else if (completion.CompletionContextType == Constants.ChecklistContextCheckpoint && !string.IsNullOrEmpty(completion.CompletionContextId))
                {
                    checkpointId = completion.CompletionContextId;
                }
                else if (contextType == Constants.ChecklistContextCheckpoint && !string.IsNullOrEmpty(contextId))
                {
                    checkpointId = contextId;
                }
                else
                {
                    // Fall back to finding the first assigned checkpoint for this marshal
                    checkpointId = context.AssignedLocationIds.FirstOrDefault();
                }

                if (!string.IsNullOrEmpty(checkpointId))
                {
                    await TryCheckOutMarshalAsync(eventId, request.MarshalId, checkpointId);
                }
            }

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uncompleting checklist item");
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Attempts to check out a marshal at a checkpoint when uncompleting a linked task.
    /// </summary>
    private async Task TryCheckOutMarshalAsync(string eventId, string marshalId, string checkpointId)
    {
        try
        {
            // Find the marshal's assignment at this checkpoint
            IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
            AssignmentEntity? assignment = assignments.FirstOrDefault(a => a.LocationId == checkpointId);

            if (assignment == null || !assignment.IsCheckedIn)
            {
                return; // No assignment or already checked out
            }

            // Get the location to update count
            LocationEntity? location = await _locationRepository.GetAsync(eventId, checkpointId);
            if (location == null)
            {
                return;
            }

            // Check out the marshal
            assignment.IsCheckedIn = false;
            assignment.CheckInTime = null;
            assignment.CheckInLatitude = null;
            assignment.CheckInLongitude = null;
            assignment.CheckInMethod = string.Empty;
            assignment.CheckedInBy = string.Empty;

            await _assignmentRepository.UpdateAsync(assignment);

            // Update location checked-in count with retry logic
            location.CheckedInCount--;
            bool locationUpdated = await FunctionHelpers.ExecuteWithRetryAsync(
                () => _locationRepository.UpdateAsync(location),
                maxRetries: 3,
                baseDelayMs: 100
            );

            if (!locationUpdated)
            {
                _logger.LogWarning("Failed to update location count after linked check-out for assignment {AssignmentId}", assignment.RowKey);
            }

            _logger.LogInformation("Marshal {MarshalId} checked out from {LocationName} via linked task uncomplete", marshalId, location.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during linked check-out for marshal {MarshalId} at checkpoint {CheckpointId}", marshalId, checkpointId);
            // Don't fail the uncomplete if check-out fails
        }
    }
#pragma warning restore MA0051

    [Function("GetChecklistCompletionReport")]
    public async Task<IActionResult> GetChecklistCompletionReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/checklist-report")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Get all items
            List<ChecklistItemEntity> items = [.. await _checklistItemRepository.GetByEventAsync(eventId)];

            // Get all completions
            List<ChecklistCompletionEntity> completions = [.. await _checklistCompletionRepository.GetByEventAsync(eventId)];

            // Get all marshals for this event
            IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);

            // Build report
            object report = new
            {
                TotalItems = items.Count,
                TotalCompletions = completions.Count,
                CompletionsByItem = items.Select(item => new
                {
                    ItemId = item.ItemId,
                    Text = item.Text,
                    ScopeConfigurations = JsonSerializer.Deserialize<List<ScopeConfiguration>>(item.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [],
                    CompletionCount = completions.Count(c => c.ChecklistItemId == item.ItemId),
                    IsRequired = item.IsRequired
                }),
                CompletionsByMarshal = marshals.Select(marshal => new
                {
                    MarshalId = marshal.MarshalId,
                    MarshalName = marshal.Name,
                    CompletionCount = completions.Count(c => c.ContextOwnerMarshalId == marshal.MarshalId)
                })
            };

            return new OkObjectResult(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checklist completion report");
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Gets detailed checklist status report showing task completion by person and by task.
    /// This is a more expensive query than the basic report, intended for admin reporting views.
    /// </summary>
    [Function("GetChecklistDetailedReport")]
#pragma warning disable MA0051 // Complex report generation with multiple aggregation views
    public async Task<IActionResult> GetChecklistDetailedReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/checklist-report/detailed")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Get all items, sorted by display order
            List<ChecklistItemEntity> items = [.. (await _checklistItemRepository.GetByEventAsync(eventId))
                .OrderBy(i => i.DisplayOrder)
                .ThenBy(i => i.Text)];

            // Get all completions (non-deleted only)
            List<ChecklistCompletionEntity> completions = [.. (await _checklistCompletionRepository.GetByEventAsync(eventId))
                .Where(c => !c.IsDeleted)];

            // Get all marshals for this event, sorted by name
            List<MarshalEntity> marshals = [.. (await _marshalRepository.GetByEventAsync(eventId))
                .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)];

            // Preload event data for efficient context building
            ChecklistContextHelper.PreloadedEventData preloadedData = await _contextHelper.PreloadEventDataAsync(eventId);

            // Build marshal contexts for all marshals
            Dictionary<string, ChecklistScopeHelper.MarshalContext> marshalContexts =
                ChecklistContextHelper.BuildMarshalContextsFromPreloaded(
                    marshals.Select(m => m.MarshalId),
                    preloadedData);

            // Build detailed report by person (marshal)
            List<object> detailedByPerson = [];
            foreach (MarshalEntity marshal in marshals)
            {
                ChecklistScopeHelper.MarshalContext context = marshalContexts[marshal.MarshalId];
                List<object> taskStatuses = [];

                foreach (ChecklistItemEntity item in items)
                {
                    // Check if this task applies to this marshal
                    if (!ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, preloadedData.LocationsById))
                    {
                        continue;
                    }

                    // Check completion status
                    bool isCompleted = ChecklistScopeHelper.IsItemCompletedInContext(item, context, preloadedData.LocationsById, completions);
                    (string? actorName, _, DateTime? completedAt) = ChecklistScopeHelper.GetCompletionDetails(item, context, preloadedData.LocationsById, completions);

                    taskStatuses.Add(new
                    {
                        ItemId = item.ItemId,
                        Text = item.Text,
                        DisplayOrder = item.DisplayOrder,
                        IsRequired = item.IsRequired,
                        IsCompleted = isCompleted,
                        CompletedAt = completedAt,
                        CompletedBy = actorName
                    });
                }

                if (taskStatuses.Count > 0)
                {
                    int completedCount = taskStatuses.Count(t => ((dynamic)t).IsCompleted);
                    detailedByPerson.Add(new
                    {
                        MarshalId = marshal.MarshalId,
                        MarshalName = marshal.Name,
                        TotalTasks = taskStatuses.Count,
                        CompletedTasks = completedCount,
                        Tasks = taskStatuses
                    });
                }
            }

            // Build detailed report by task (item)
            List<object> detailedByTask = [];
            foreach (ChecklistItemEntity item in items)
            {
                List<object> marshalStatuses = [];

                foreach (MarshalEntity marshal in marshals)
                {
                    ChecklistScopeHelper.MarshalContext context = marshalContexts[marshal.MarshalId];

                    // Check if this task applies to this marshal
                    if (!ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, preloadedData.LocationsById))
                    {
                        continue;
                    }

                    // Check completion status
                    bool isCompleted = ChecklistScopeHelper.IsItemCompletedInContext(item, context, preloadedData.LocationsById, completions);
                    (string? actorName, _, DateTime? completedAt) = ChecklistScopeHelper.GetCompletionDetails(item, context, preloadedData.LocationsById, completions);

                    marshalStatuses.Add(new
                    {
                        MarshalId = marshal.MarshalId,
                        MarshalName = marshal.Name,
                        IsCompleted = isCompleted,
                        CompletedAt = completedAt,
                        CompletedBy = actorName
                    });
                }

                int applicableCount = marshalStatuses.Count;
                int completedCount = marshalStatuses.Count(m => ((dynamic)m).IsCompleted);

                detailedByTask.Add(new
                {
                    ItemId = item.ItemId,
                    Text = item.Text,
                    DisplayOrder = item.DisplayOrder,
                    IsRequired = item.IsRequired,
                    ScopeConfigurations = JsonSerializer.Deserialize<List<ScopeConfiguration>>(item.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [],
                    ApplicableCount = applicableCount,
                    CompletedCount = completedCount,
                    Marshals = marshalStatuses
                });
            }

            // Build assignments grouped by location for checkpoint report
            Dictionary<string, List<(string MarshalId, string MarshalName)>> assignmentsByLocation = [];
            foreach (KeyValuePair<string, List<AssignmentEntity>> kvp in preloadedData.AssignmentsByMarshal)
            {
                MarshalEntity? marshal = marshals.FirstOrDefault(m => m.MarshalId == kvp.Key);
                if (marshal == null) continue;

                foreach (AssignmentEntity assignment in kvp.Value)
                {
                    if (!assignmentsByLocation.ContainsKey(assignment.LocationId))
                    {
                        assignmentsByLocation[assignment.LocationId] = [];
                    }
                    assignmentsByLocation[assignment.LocationId].Add((marshal.MarshalId, marshal.Name));
                }
            }

            // Build detailed report by checkpoint (Checkpoint > Person > Job)
            List<object> detailedByCheckpoint = [];
            List<LocationEntity> sortedLocations = [.. preloadedData.LocationsById.Values];

            foreach (LocationEntity location in sortedLocations)
            {
                List<(string MarshalId, string MarshalName)> assignedMarshals = assignmentsByLocation.GetValueOrDefault(location.RowKey) ?? [];
                // Sort marshals alphabetically
                assignedMarshals = [.. assignedMarshals.OrderBy(m => m.MarshalName, StringComparer.CurrentCultureIgnoreCase)];

                List<object> peopleStatuses = [];
                int totalTasksInCheckpoint = 0;
                int completedTasksInCheckpoint = 0;

                foreach ((string marshalId, string marshalName) in assignedMarshals)
                {
                    ChecklistScopeHelper.MarshalContext context = marshalContexts[marshalId];
                    List<object> taskStatuses = [];

                    foreach (ChecklistItemEntity item in items)
                    {
                        // Check if this task applies to this marshal at this checkpoint
                        if (!ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, preloadedData.LocationsById))
                        {
                            continue;
                        }

                        // Check completion status
                        bool isCompleted = ChecklistScopeHelper.IsItemCompletedInContext(item, context, preloadedData.LocationsById, completions);
                        (string? actorName, _, DateTime? completedAt) = ChecklistScopeHelper.GetCompletionDetails(item, context, preloadedData.LocationsById, completions);

                        taskStatuses.Add(new
                        {
                            ItemId = item.ItemId,
                            Text = item.Text,
                            DisplayOrder = item.DisplayOrder,
                            IsRequired = item.IsRequired,
                            IsCompleted = isCompleted,
                            CompletedAt = completedAt,
                            CompletedBy = actorName
                        });
                    }

                    if (taskStatuses.Count > 0)
                    {
                        int personCompletedCount = taskStatuses.Count(t => ((dynamic)t).IsCompleted);
                        totalTasksInCheckpoint += taskStatuses.Count;
                        completedTasksInCheckpoint += personCompletedCount;

                        peopleStatuses.Add(new
                        {
                            MarshalId = marshalId,
                            MarshalName = marshalName,
                            TotalTasks = taskStatuses.Count,
                            CompletedTasks = personCompletedCount,
                            Tasks = taskStatuses
                        });
                    }
                }

                if (peopleStatuses.Count > 0)
                {
                    detailedByCheckpoint.Add(new
                    {
                        CheckpointId = location.RowKey,
                        CheckpointName = location.Name,
                        CheckpointDescription = location.Description,
                        TotalTasks = totalTasksInCheckpoint,
                        CompletedTasks = completedTasksInCheckpoint,
                        TotalPeople = peopleStatuses.Count,
                        People = peopleStatuses
                    });
                }
            }

            // Build detailed report by area (Area > Person > Job)
            List<object> detailedByArea = [];
            List<AreaEntity> sortedAreas = [.. preloadedData.Areas];

            foreach (AreaEntity area in sortedAreas)
            {
                // Find all marshals assigned to checkpoints in this area
                List<string> areaLocationIds = preloadedData.LocationsById.Values
                    .Where(loc =>
                    {
                        List<string> locAreaIds = JsonSerializer.Deserialize<List<string>>(loc.AreaIdsJson, FunctionHelpers.JsonOptions) ?? [];
                        return locAreaIds.Contains(area.RowKey);
                    })
                    .Select(loc => loc.RowKey)
                    .ToList();

                // Get unique marshals assigned to any checkpoint in this area
                HashSet<string> marshalIdsInArea = [];
                foreach (string locationId in areaLocationIds)
                {
                    if (assignmentsByLocation.TryGetValue(locationId, out List<(string MarshalId, string MarshalName)>? locationMarshals))
                    {
                        foreach ((string marshalId, _) in locationMarshals)
                        {
                            marshalIdsInArea.Add(marshalId);
                        }
                    }
                }

                // Sort marshals alphabetically
                List<MarshalEntity> marshalsInArea = marshals
                    .Where(m => marshalIdsInArea.Contains(m.MarshalId))
                    .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                List<object> peopleStatuses = [];
                int totalTasksInArea = 0;
                int completedTasksInArea = 0;

                foreach (MarshalEntity marshal in marshalsInArea)
                {
                    ChecklistScopeHelper.MarshalContext context = marshalContexts[marshal.MarshalId];
                    List<object> taskStatuses = [];

                    foreach (ChecklistItemEntity item in items)
                    {
                        // Check if this task applies to this marshal
                        if (!ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, preloadedData.LocationsById))
                        {
                            continue;
                        }

                        // Check completion status
                        bool isCompleted = ChecklistScopeHelper.IsItemCompletedInContext(item, context, preloadedData.LocationsById, completions);
                        (string? actorName, _, DateTime? completedAt) = ChecklistScopeHelper.GetCompletionDetails(item, context, preloadedData.LocationsById, completions);

                        taskStatuses.Add(new
                        {
                            ItemId = item.ItemId,
                            Text = item.Text,
                            DisplayOrder = item.DisplayOrder,
                            IsRequired = item.IsRequired,
                            IsCompleted = isCompleted,
                            CompletedAt = completedAt,
                            CompletedBy = actorName
                        });
                    }

                    if (taskStatuses.Count > 0)
                    {
                        int personCompletedCount = taskStatuses.Count(t => ((dynamic)t).IsCompleted);
                        totalTasksInArea += taskStatuses.Count;
                        completedTasksInArea += personCompletedCount;

                        peopleStatuses.Add(new
                        {
                            MarshalId = marshal.MarshalId,
                            MarshalName = marshal.Name,
                            TotalTasks = taskStatuses.Count,
                            CompletedTasks = personCompletedCount,
                            Tasks = taskStatuses
                        });
                    }
                }

                if (peopleStatuses.Count > 0)
                {
                    detailedByArea.Add(new
                    {
                        AreaId = area.RowKey,
                        AreaName = area.Name,
                        TotalTasks = totalTasksInArea,
                        CompletedTasks = completedTasksInArea,
                        TotalPeople = peopleStatuses.Count,
                        People = peopleStatuses
                    });
                }
            }

            object report = new
            {
                TotalItems = items.Count,
                TotalMarshals = marshals.Count,
                TotalCheckpoints = sortedLocations.Count,
                TotalAreas = sortedAreas.Count,
                DetailedByPerson = detailedByPerson,
                DetailedByTask = detailedByTask,
                DetailedByCheckpoint = detailedByCheckpoint,
                DetailedByArea = detailedByArea
            };

            return new OkObjectResult(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting detailed checklist report");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051
}
