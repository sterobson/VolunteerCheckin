using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VolunteerCheckin.Functions.Helpers;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Functions;

public class ChecklistFunctions
{
    private readonly ILogger<ChecklistFunctions> _logger;
    private readonly IChecklistItemRepository _checklistItemRepository;
    private readonly IChecklistCompletionRepository _checklistCompletionRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IAreaRepository _areaRepository;

    public ChecklistFunctions(
        ILogger<ChecklistFunctions> logger,
        IChecklistItemRepository checklistItemRepository,
        IChecklistCompletionRepository checklistCompletionRepository,
        IMarshalRepository marshalRepository,
        IAssignmentRepository assignmentRepository,
        ILocationRepository locationRepository,
        IAreaRepository areaRepository)
    {
        _logger = logger;
        _checklistItemRepository = checklistItemRepository;
        _checklistCompletionRepository = checklistCompletionRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _locationRepository = locationRepository;
        _areaRepository = areaRepository;
    }

    /// <summary>
    /// Creates one or more checklist items for an event.
    /// </summary>
    /// <param name="eventId">The event ID</param>
    /// <param name="request">The checklist item(s) to create</param>
    /// <returns>
    /// If CreateSeparateItems is false: Returns a single ChecklistItemResponse
    /// If CreateSeparateItems is true: Returns { items: ChecklistItemResponse[], count: number }
    /// </returns>
    /// <remarks>
    /// When CreateSeparateItems is true, the Text field is split by line breaks, and each
    /// non-empty line creates a separate checklist item with the same scope, areas, checkpoints,
    /// marshals, and other settings. DisplayOrder is incremented for each item created.
    /// </remarks>
    [Function("CreateChecklistItem")]
    public async Task<IActionResult> CreateChecklistItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/checklist-items")] HttpRequest req,
        string eventId)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CreateChecklistItemRequest? request = JsonSerializer.Deserialize<CreateChecklistItemRequest>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Text))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidRequest });
            }

            string adminEmail = req.Headers[Constants.AdminEmailHeader].ToString();
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorEmailRequired });
            }

            // Validate scope configurations
            if (request.ScopeConfigurations == null || request.ScopeConfigurations.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "At least one scope configuration is required" });
            }

            // Sanitize input
            string sanitizedText = InputSanitizer.SanitizeDescription(request.Text);

            // Serialize scope configurations once
            string scopeConfigurationsJson = JsonSerializer.Serialize(request.ScopeConfigurations);

            // Check if we should create separate items for each line
            if (request.CreateSeparateItems)
            {
                List<string> lines = sanitizedText
                    .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();

                if (lines.Count == 0)
                {
                    return new BadRequestObjectResult(new { message = "No valid lines found in text" });
                }

                List<ChecklistItemResponse> createdItems = new();
                int currentDisplayOrder = request.DisplayOrder;

                foreach (string line in lines)
                {
                    string itemId = Guid.NewGuid().ToString();
                    ChecklistItemEntity entity = new()
                    {
                        PartitionKey = eventId,
                        RowKey = itemId,
                        EventId = eventId,
                        ItemId = itemId,
                        Text = line,
                        ScopeConfigurationsJson = scopeConfigurationsJson,
                        DisplayOrder = currentDisplayOrder++,
                        IsRequired = request.IsRequired,
                        VisibleFrom = request.VisibleFrom,
                        VisibleUntil = request.VisibleUntil,
                        MustCompleteBy = request.MustCompleteBy,
                        CreatedByAdminEmail = adminEmail,
                        CreatedDate = DateTime.UtcNow
                    };

                    await _checklistItemRepository.AddAsync(entity);
                    createdItems.Add(entity.ToResponse());

                    _logger.LogInformation($"Checklist item created: {itemId} by {adminEmail}");
                }

                return new OkObjectResult(new { items = createdItems, count = createdItems.Count });
            }
            else
            {
                // Original behavior: create a single item
                string itemId = Guid.NewGuid().ToString();
                ChecklistItemEntity entity = new()
                {
                    PartitionKey = eventId,
                    RowKey = itemId,
                    EventId = eventId,
                    ItemId = itemId,
                    Text = sanitizedText,
                    ScopeConfigurationsJson = scopeConfigurationsJson,
                    DisplayOrder = request.DisplayOrder,
                    IsRequired = request.IsRequired,
                    VisibleFrom = request.VisibleFrom,
                    VisibleUntil = request.VisibleUntil,
                    MustCompleteBy = request.MustCompleteBy,
                    CreatedByAdminEmail = adminEmail,
                    CreatedDate = DateTime.UtcNow
                };

                await _checklistItemRepository.AddAsync(entity);

                _logger.LogInformation($"Checklist item created: {itemId} by {adminEmail}");

                return new OkObjectResult(entity.ToResponse());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checklist item");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetChecklistItems")]
    public async Task<IActionResult> GetChecklistItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/checklist-items")] HttpRequest req,
        string eventId)
    {
        try
        {
            IEnumerable<ChecklistItemEntity> items = await _checklistItemRepository.GetByEventAsync(eventId);
            List<ChecklistItemResponse> responses = items.Select(i => i.ToResponse()).ToList();

            return new OkObjectResult(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checklist items");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetChecklistItem")]
    public async Task<IActionResult> GetChecklistItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "checklist-items/{eventId}/{itemId}")] HttpRequest req,
        string eventId,
        string itemId)
    {
        try
        {
            ChecklistItemEntity? item = await _checklistItemRepository.GetAsync(eventId, itemId);

            if (item == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorChecklistItemNotFound });
            }

            return new OkObjectResult(item.ToResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checklist item");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateChecklistItem")]
    public async Task<IActionResult> UpdateChecklistItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "checklist-items/{eventId}/{itemId}")] HttpRequest req,
        string eventId,
        string itemId)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            UpdateChecklistItemRequest? request = JsonSerializer.Deserialize<UpdateChecklistItemRequest>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidRequest });
            }

            string adminEmail = req.Headers[Constants.AdminEmailHeader].ToString();
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorEmailRequired });
            }

            ChecklistItemEntity? item = await _checklistItemRepository.GetAsync(eventId, itemId);

            if (item == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorChecklistItemNotFound });
            }

            // Validate scope configurations
            if (request.ScopeConfigurations == null || request.ScopeConfigurations.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "At least one scope configuration is required" });
            }

            // Sanitize input
            string sanitizedText = InputSanitizer.SanitizeDescription(request.Text);

            // Update fields
            item.Text = sanitizedText;
            item.ScopeConfigurationsJson = JsonSerializer.Serialize(request.ScopeConfigurations);
            item.DisplayOrder = request.DisplayOrder;
            item.IsRequired = request.IsRequired;
            item.VisibleFrom = request.VisibleFrom;
            item.VisibleUntil = request.VisibleUntil;
            item.MustCompleteBy = request.MustCompleteBy;
            item.LastModifiedDate = DateTime.UtcNow;
            item.LastModifiedByAdminEmail = adminEmail;

            await _checklistItemRepository.UpdateAsync(item);

            _logger.LogInformation($"Checklist item updated: {itemId} by {adminEmail}");

            return new OkObjectResult(item.ToResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating checklist item");
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteChecklistItem")]
    public async Task<IActionResult> DeleteChecklistItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "checklist-items/{eventId}/{itemId}")] HttpRequest req,
        string eventId,
        string itemId)
    {
        try
        {
            ChecklistItemEntity? item = await _checklistItemRepository.GetAsync(eventId, itemId);

            if (item == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorChecklistItemNotFound });
            }

            // Delete all completions for this item
            await _checklistCompletionRepository.DeleteAllByItemAsync(eventId, itemId);

            // Delete the item
            await _checklistItemRepository.DeleteAsync(eventId, itemId);

            _logger.LogInformation($"Checklist item deleted: {itemId}");

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting checklist item");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetMarshalChecklist")]
    public async Task<IActionResult> GetMarshalChecklist(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/marshals/{marshalId}/checklist")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            // Build marshal context
            ChecklistScopeHelper.MarshalContext context = await BuildMarshalContext(eventId, marshalId);

            // Build checkpoint lookup dictionary for area-based matching
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
            Dictionary<string, LocationEntity> checkpointLookup = allLocations.ToDictionary(l => l.RowKey);

            // Get all checklist items for event
            IEnumerable<ChecklistItemEntity> allItems = await _checklistItemRepository.GetByEventAsync(eventId);

            // Get all completions for event
            List<ChecklistCompletionEntity> allCompletions =
                (await _checklistCompletionRepository.GetByEventAsync(eventId)).ToList();

            // Filter items relevant to this marshal
            List<ChecklistItemWithStatus> relevantItems = allItems
                .Where(item => ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup))
                .Where(item => IsItemVisible(item))
                .OrderBy(item => item.DisplayOrder)
                .Select(item => BuildItemWithStatus(item, context, checkpointLookup, allCompletions))
                .ToList();

            return new OkObjectResult(relevantItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting marshal checklist");
            return new StatusCodeResult(500);
        }
    }

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
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
            ChecklistScopeHelper.MarshalContext context = await BuildMarshalContext(eventId, request.MarshalId);

            // Build checkpoint lookup dictionary for area-based matching
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
            Dictionary<string, LocationEntity> checkpointLookup = allLocations.ToDictionary(l => l.RowKey);

            // Verify permission to complete
            if (!ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup))
            {
                _logger.LogWarning($"Marshal {request.MarshalId} attempted to complete checklist item {itemId} without permission");
                return new ForbidResult();
            }

            // Get all completions for this item
            List<ChecklistCompletionEntity> itemCompletions =
                (await _checklistCompletionRepository.GetByItemAsync(eventId, itemId)).ToList();

            // Check if already completed in this context
            if (ChecklistScopeHelper.IsItemCompletedInContext(item, context, checkpointLookup, itemCompletions))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorChecklistAlreadyCompleted });
            }

            // Get marshal details for denormalization
            MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, request.MarshalId);
            if (marshal == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorMarshalNotFound });
            }

            // Determine context for completion
            (string contextType, string contextId, string matchedScope) = request.ContextType != null && request.ContextId != null
                ? (request.ContextType, request.ContextId, string.Empty)
                : ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

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
                CompletedByMarshalId = request.MarshalId,
                CompletedByMarshalName = marshal.Name,
                CompletionContextType = contextType,
                CompletionContextId = contextId,
                CompletedAt = DateTime.UtcNow
            };

            await _checklistCompletionRepository.AddAsync(completion);

            _logger.LogInformation($"Checklist item {itemId} completed by marshal {request.MarshalId} at {completion.CompletedAt}");

            return new OkObjectResult(completion.ToResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing checklist item");
            return new StatusCodeResult(500);
        }
    }

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
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.MarshalId))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidRequest });
            }

            string adminEmail = req.Headers[Constants.AdminEmailHeader].ToString();
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorEmailRequired });
            }

            // Get the checklist item
            ChecklistItemEntity? item = await _checklistItemRepository.GetAsync(eventId, itemId);
            if (item == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorChecklistItemNotFound });
            }

            // Build marshal context
            ChecklistScopeHelper.MarshalContext context = await BuildMarshalContext(eventId, request.MarshalId);

            // Build checkpoint lookup dictionary for area-based matching
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
            Dictionary<string, LocationEntity> checkpointLookup = allLocations.ToDictionary(l => l.RowKey);

            // Determine context for completion
            (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

            // Get all completions for this item
            List<ChecklistCompletionEntity> itemCompletions =
                (await _checklistCompletionRepository.GetByItemAsync(eventId, itemId)).ToList();

            // Find the completion to uncomplete
            ChecklistCompletionEntity? completion;

            if (ChecklistScopeHelper.IsPersonalScope(matchedScope))
            {
                completion = itemCompletions.FirstOrDefault(c =>
                    c.CompletedByMarshalId == request.MarshalId &&
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

            // Soft delete the completion
            completion.IsDeleted = true;
            completion.UncompletedAt = DateTime.UtcNow;
            completion.UncompletedByAdminEmail = adminEmail;

            await _checklistCompletionRepository.UpdateAsync(completion);

            _logger.LogInformation($"Checklist item {itemId} uncompleted for marshal {request.MarshalId} by admin {adminEmail}");

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uncompleting checklist item");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetChecklistCompletionReport")]
    public async Task<IActionResult> GetChecklistCompletionReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/checklist-report")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Get all items
            List<ChecklistItemEntity> items = (await _checklistItemRepository.GetByEventAsync(eventId)).ToList();

            // Get all completions
            List<ChecklistCompletionEntity> completions = (await _checklistCompletionRepository.GetByEventAsync(eventId)).ToList();

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
                    ScopeConfigurations = JsonSerializer.Deserialize<List<ScopeConfiguration>>(item.ScopeConfigurationsJson) ?? [],
                    CompletionCount = completions.Count(c => c.ChecklistItemId == item.ItemId),
                    IsRequired = item.IsRequired
                }),
                CompletionsByMarshal = marshals.Select(marshal => new
                {
                    MarshalId = marshal.MarshalId,
                    MarshalName = marshal.Name,
                    CompletionCount = completions.Count(c => c.CompletedByMarshalId == marshal.MarshalId)
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

    // Helper methods

    private async Task<ChecklistScopeHelper.MarshalContext> BuildMarshalContext(string eventId, string marshalId)
    {
        // Get marshal's assignments
        IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
        List<string> assignedLocationIds = assignments.Select(a => a.LocationId).ToList();

        // Get locations to determine areas
        IEnumerable<LocationEntity> locations = await _locationRepository.GetByEventAsync(eventId);
        List<string> assignedAreaIds = locations
            .Where(l => assignedLocationIds.Contains(l.RowKey))
            .SelectMany(l => JsonSerializer.Deserialize<List<string>>(l.AreaIdsJson) ?? [])
            .Distinct()
            .ToList();

        // Get areas to check if marshal is an area lead
        IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(eventId);
        List<string> areaLeadForAreaIds = areas
            .Where(a =>
            {
                List<string> areaLeadIds = JsonSerializer.Deserialize<List<string>>(a.AreaLeadMarshalIdsJson) ?? [];
                return areaLeadIds.Contains(marshalId);
            })
            .Select(a => a.RowKey)
            .ToList();

        return new ChecklistScopeHelper.MarshalContext(
            marshalId,
            assignedAreaIds,
            assignedLocationIds,
            areaLeadForAreaIds
        );
    }

    private static bool IsItemVisible(ChecklistItemEntity item)
    {
        DateTime now = DateTime.UtcNow;

        if (item.VisibleFrom.HasValue && now < item.VisibleFrom.Value)
        {
            return false;
        }

        if (item.VisibleUntil.HasValue && now > item.VisibleUntil.Value)
        {
            return false;
        }

        return true;
    }

    private static ChecklistItemWithStatus BuildItemWithStatus(
        ChecklistItemEntity item,
        ChecklistScopeHelper.MarshalContext context,
        Dictionary<string, LocationEntity> checkpointLookup,
        List<ChecklistCompletionEntity> allCompletions)
    {
        List<ScopeConfiguration> scopeConfigurations =
            JsonSerializer.Deserialize<List<ScopeConfiguration>>(item.ScopeConfigurationsJson) ?? [];

        bool isCompleted = ChecklistScopeHelper.IsItemCompletedInContext(item, context, checkpointLookup, allCompletions);
        bool canComplete = ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup);
        (string? completedByName, DateTime? completedAt) = ChecklistScopeHelper.GetCompletionDetails(item, context, checkpointLookup, allCompletions);
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        return new ChecklistItemWithStatus(
            item.ItemId,
            item.EventId,
            item.Text,
            scopeConfigurations,
            item.DisplayOrder,
            item.IsRequired,
            item.VisibleFrom,
            item.VisibleUntil,
            item.MustCompleteBy,
            isCompleted,
            canComplete,
            completedByName,
            completedAt,
            contextType,
            contextId,
            matchedScope
        );
    }
}
