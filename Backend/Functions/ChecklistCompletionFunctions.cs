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
    private readonly ChecklistContextHelper _contextHelper;

    public ChecklistCompletionFunctions(
        ILogger<ChecklistCompletionFunctions> logger,
        IChecklistItemRepository checklistItemRepository,
        IChecklistCompletionRepository checklistCompletionRepository,
        IMarshalRepository marshalRepository,
        ILocationRepository locationRepository,
        IAdminUserRepository adminUserRepository,
        IAssignmentRepository assignmentRepository,
        IAreaRepository areaRepository)
    {
        _logger = logger;
        _checklistItemRepository = checklistItemRepository;
        _checklistCompletionRepository = checklistCompletionRepository;
        _marshalRepository = marshalRepository;
        _locationRepository = locationRepository;
        _adminUserRepository = adminUserRepository;
        _contextHelper = new ChecklistContextHelper(assignmentRepository, locationRepository, areaRepository);
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
            ChecklistScopeHelper.MarshalContext context = await _contextHelper.BuildMarshalContextAsync(eventId, request.MarshalId);

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

            // Get marshal details for denormalization (context owner)
            MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, request.MarshalId);
            if (marshal == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorMarshalNotFound });
            }

            // Determine context for completion
            (string contextType, string contextId, string matchedScope) = request.ContextType != null && request.ContextId != null
                ? (request.ContextType, request.ContextId, string.Empty)
                : ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

            // Determine actor (who is actually performing this action)
            string actorType;
            string actorId;
            string actorName;
            string adminEmail = req.Headers[Constants.AdminEmailHeader].ToString();

            if (!string.IsNullOrWhiteSpace(adminEmail))
            {
                // Admin is completing this on behalf of the marshal
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

            _logger.LogInformation($"Checklist item {itemId} completed by {actorType} {actorName} (context owner: {marshal.Name}) at {completion.CompletedAt}");

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
            ChecklistScopeHelper.MarshalContext context = await _contextHelper.BuildMarshalContextAsync(eventId, request.MarshalId);

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
}
