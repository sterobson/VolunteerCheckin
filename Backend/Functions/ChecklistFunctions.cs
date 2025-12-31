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
    private readonly IAdminUserRepository _adminUserRepository;

    public ChecklistFunctions(
        ILogger<ChecklistFunctions> logger,
        IChecklistItemRepository checklistItemRepository,
        IChecklistCompletionRepository checklistCompletionRepository,
        IMarshalRepository marshalRepository,
        IAssignmentRepository assignmentRepository,
        ILocationRepository locationRepository,
        IAreaRepository areaRepository,
        IAdminUserRepository adminUserRepository)
    {
        _logger = logger;
        _checklistItemRepository = checklistItemRepository;
        _checklistCompletionRepository = checklistCompletionRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _locationRepository = locationRepository;
        _areaRepository = areaRepository;
        _adminUserRepository = adminUserRepository;
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

                List<ChecklistItemResponse> createdItems = [];
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

            // Build list of items with all their contexts
            List<ChecklistItemWithStatus> relevantItems = [];

            foreach (ChecklistItemEntity item in allItems.Where(IsItemVisible).OrderBy(i => i.DisplayOrder))
            {
                // Get all contexts where this item is relevant to this marshal
                List<ChecklistScopeHelper.ScopeMatchResult> contexts =
                    ChecklistScopeHelper.GetAllRelevantContexts(item, context, checkpointLookup);

                // For each context, add an item instance
                foreach (ChecklistScopeHelper.ScopeMatchResult scopeContext in contexts)
                {
                    relevantItems.Add(BuildItemWithStatus(item, context, checkpointLookup, allCompletions, scopeContext));
                }
            }

            return new OkObjectResult(relevantItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting marshal checklist");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetCheckpointChecklist")]
    public async Task<IActionResult> GetCheckpointChecklist(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/locations/{locationId}/checklist")] HttpRequest req,
        string eventId,
        string locationId)
    {
        try
        {
            // Get the location to verify it exists and get its areas
            LocationEntity? location = await _locationRepository.GetAsync(eventId, locationId);
            if (location == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorLocationNotFound });
            }

            List<string> locationAreaIds = JsonSerializer.Deserialize<List<string>>(location.AreaIdsJson) ?? [];

            // Get all marshals assigned to this checkpoint
            IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByLocationAsync(eventId, locationId);
            List<string> assignedMarshalIds = assignments.Select(a => a.MarshalId).Distinct().ToList();

            // Build checkpoint lookup dictionary
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
            Dictionary<string, LocationEntity> checkpointLookup = allLocations.ToDictionary(l => l.RowKey);

            // Get all checklist items for event
            IEnumerable<ChecklistItemEntity> allItems = await _checklistItemRepository.GetByEventAsync(eventId);

            // Get all completions for event
            List<ChecklistCompletionEntity> allCompletions =
                (await _checklistCompletionRepository.GetByEventAsync(eventId)).ToList();

            // Get area leads for checkpoint's areas
            IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(eventId);
            List<string> areaLeadIds = areas
                .Where(a => locationAreaIds.Contains(a.RowKey))
                .SelectMany(a => JsonSerializer.Deserialize<List<string>>(a.AreaLeadMarshalIdsJson) ?? [])
                .Distinct()
                .ToList();

            // Build set of all relevant marshal IDs (assigned + area leads)
            HashSet<string> relevantMarshalIds = [.. assignedMarshalIds, .. areaLeadIds];

            // Get all marshals to get their names
            IEnumerable<MarshalEntity> allMarshals = await _marshalRepository.GetByEventAsync(eventId);
            Dictionary<string, string> marshalNames = allMarshals.ToDictionary(m => m.RowKey, m => m.Name);

            // Build list of relevant items - iterate through each marshal to get their personal items
            List<ChecklistItemWithStatus> relevantItems = [];

            foreach (ChecklistItemEntity item in allItems.Where(IsItemVisible).OrderBy(i => i.DisplayOrder))
            {
                _logger.LogInformation($"Processing item: {item.Text} ({item.ItemId})");

                // For each marshal at this checkpoint, get their view of this item
                foreach (string marshalId in relevantMarshalIds)
                {
                    // Build context for this specific marshal
                    ChecklistScopeHelper.MarshalContext marshalContext = await BuildMarshalContext(eventId, marshalId);

                    _logger.LogInformation($"  Marshal {marshalNames.GetValueOrDefault(marshalId, marshalId)}: Areas={string.Join(",", marshalContext.AssignedAreaIds)}");

                    // Get all contexts where this item is relevant to this marshal
                    List<ChecklistScopeHelper.ScopeMatchResult> contexts =
                        ChecklistScopeHelper.GetAllRelevantContexts(item, marshalContext, checkpointLookup);

                    _logger.LogInformation($"    Found {contexts.Count} context(s)");

                    // Only include contexts that are for this checkpoint or its areas
                    foreach (ChecklistScopeHelper.ScopeMatchResult scopeContext in contexts)
                    {
                        // Include if:
                        // 1. Personal item that's scoped to this checkpoint or its areas
                        // 2. Shared item for this checkpoint
                        // 3. Shared item for this checkpoint's area
                        bool shouldInclude = false;

                        if (scopeContext.ContextType == "Personal")
                        {
                            // Personal items - always include if the marshal matches the scope
                            // The marshal already matched the scope (that's why we got this context),
                            // so we just need to verify it's relevant to THIS checkpoint
                            ScopeConfiguration? winningConfig = scopeContext.WinningConfig;

                            _logger.LogInformation($"      Personal context: Scope={winningConfig?.Scope}, ItemType={winningConfig?.ItemType}, IDs={string.Join(",", winningConfig?.Ids ?? [])}");

                            if (winningConfig != null)
                            {
                                // Include if:
                                // - Scoped to Everyone (always relevant)
                                // - Scoped to specific marshals (always relevant if this marshal matched)
                                // - Scoped to checkpoints that include THIS checkpoint
                                // - Scoped to areas where THIS marshal is in those areas
                                if (winningConfig.Scope == "Everyone")
                                {
                                    shouldInclude = true;
                                    _logger.LogInformation($"        Everyone scope - INCLUDING");
                                }
                                else if (winningConfig.Scope == "SpecificPeople" && winningConfig.ItemType == "Marshal")
                                {
                                    shouldInclude = true;
                                    _logger.LogInformation($"        SpecificPeople scope - INCLUDING");
                                }
                                else if (winningConfig.Scope == "EveryoneAtCheckpoints")
                                {
                                    // Include if scoped to this checkpoint or ALL checkpoints
                                    shouldInclude = winningConfig.Ids.Contains(locationId) ||
                                                   winningConfig.Ids.Contains(Constants.AllCheckpoints);
                                    _logger.LogInformation($"        EveryoneAtCheckpoints - {(shouldInclude ? "INCLUDING" : "EXCLUDING")}");
                                }
                                else if (winningConfig.Scope == "EveryoneInAreas")
                                {
                                    // Include if the marshal is in any of the scoped areas AND those areas overlap with this checkpoint's areas
                                    // OR if it's ALL_AREAS
                                    if (winningConfig.Ids.Contains(Constants.AllAreas))
                                    {
                                        // ALL_AREAS - include for any marshal at this checkpoint
                                        shouldInclude = true;
                                        _logger.LogInformation($"        EveryoneInAreas (ALL_AREAS) - INCLUDING");
                                    }
                                    else
                                    {
                                        // Specific areas - include if marshal is in any of those areas
                                        // Check if the marshal's assigned areas overlap with the scoped areas
                                        shouldInclude = marshalContext.AssignedAreaIds.Any(areaId => winningConfig.Ids.Contains(areaId));
                                        _logger.LogInformation($"        EveryoneInAreas (specific) - {(shouldInclude ? "INCLUDING" : "EXCLUDING")}");
                                    }
                                }
                                else if (winningConfig.Scope == "EveryAreaLead")
                                {
                                    // Area leads - include if marshal is a lead for any of the scoped areas
                                    if (winningConfig.Ids.Contains(Constants.AllAreas))
                                    {
                                        shouldInclude = true;
                                        _logger.LogInformation($"        EveryAreaLead (ALL_AREAS) - INCLUDING");
                                    }
                                    else
                                    {
                                        shouldInclude = marshalContext.AreaLeadForAreaIds.Any(areaId => winningConfig.Ids.Contains(areaId));
                                        _logger.LogInformation($"        EveryAreaLead (specific) - {(shouldInclude ? "INCLUDING" : "EXCLUDING")}");
                                    }
                                }
                                else
                                {
                                    _logger.LogInformation($"        Unknown scope {winningConfig.Scope} - EXCLUDING");
                                }
                            }
                        }
                        else if (scopeContext.ContextType == "Checkpoint")
                        {
                            // Shared checkpoint items - include if it's THIS checkpoint
                            if (scopeContext.ContextId == locationId)
                            {
                                shouldInclude = true;
                            }
                        }
                        else if (scopeContext.ContextType == "Area")
                        {
                            // Shared area items - include if it's one of this checkpoint's areas
                            if (locationAreaIds.Contains(scopeContext.ContextId))
                            {
                                shouldInclude = true;
                            }
                        }

                        if (shouldInclude)
                        {
                            _logger.LogInformation($"        Checking deduplication: ItemId={item.ItemId}, ContextType={scopeContext.ContextType}, ContextId={scopeContext.ContextId}");

                            // Check if we already have this exact item instance
                            bool alreadyAdded = relevantItems.Any(existing =>
                                existing.ItemId == item.ItemId &&
                                existing.CompletionContextType == scopeContext.ContextType &&
                                existing.CompletionContextId == scopeContext.ContextId);

                            _logger.LogInformation($"        Already added: {alreadyAdded}");

                            if (!alreadyAdded)
                            {
                                ChecklistItemWithStatus itemStatus = BuildItemWithStatus(item, marshalContext, checkpointLookup, allCompletions, scopeContext);

                                // Set context owner name for personal items
                                if (scopeContext.ContextType == "Personal" && marshalNames.TryGetValue(marshalId, out string? marshalName))
                                {
                                    itemStatus = itemStatus with { ContextOwnerName = marshalName };
                                }

                                _logger.LogInformation($"        ADDED item for {marshalNames.GetValueOrDefault(marshalId, marshalId)}");
                                relevantItems.Add(itemStatus);
                            }
                            else
                            {
                                _logger.LogInformation($"        SKIPPED (duplicate)");
                            }
                        }
                    }
                }
            }

            return new OkObjectResult(relevantItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checkpoint checklist");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetAreaChecklist")]
    public async Task<IActionResult> GetAreaChecklist(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/areas/{areaId}/checklist")] HttpRequest req,
        string eventId,
        string areaId)
    {
        try
        {
            // Get the area to verify it exists
            AreaEntity? area = await _areaRepository.GetAsync(eventId, areaId);
            if (area == null)
            {
                return new NotFoundObjectResult(new { message = "Area not found" });
            }

            // Get all checkpoints in this area
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
            List<LocationEntity> areaCheckpoints = allLocations
                .Where(l => {
                    List<string> locationAreaIds = JsonSerializer.Deserialize<List<string>>(l.AreaIdsJson) ?? [];
                    return locationAreaIds.Contains(areaId);
                })
                .ToList();

            List<string> checkpointIds = areaCheckpoints.Select(l => l.RowKey).ToList();

            // Get all marshals assigned to checkpoints in this area
            List<string> assignedMarshalIds = [];
            foreach (string checkpointId in checkpointIds)
            {
                IEnumerable<AssignmentEntity> checkpointAssignments =
                    await _assignmentRepository.GetByLocationAsync(eventId, checkpointId);
                assignedMarshalIds.AddRange(checkpointAssignments.Select(a => a.MarshalId));
            }
            assignedMarshalIds = assignedMarshalIds.Distinct().ToList();

            // Build checkpoint lookup dictionary
            Dictionary<string, LocationEntity> checkpointLookup = allLocations.ToDictionary(l => l.RowKey);

            // Get all checklist items for event
            IEnumerable<ChecklistItemEntity> allItems = await _checklistItemRepository.GetByEventAsync(eventId);

            // Get all completions for event
            List<ChecklistCompletionEntity> allCompletions =
                (await _checklistCompletionRepository.GetByEventAsync(eventId)).ToList();

            // Get area leads for this area
            List<string> areaLeadIds = JsonSerializer.Deserialize<List<string>>(area.AreaLeadMarshalIdsJson) ?? [];

            // Build set of all relevant marshal IDs (assigned + area leads)
            HashSet<string> relevantMarshalIds = [.. assignedMarshalIds, .. areaLeadIds];

            // Get all marshals to get their names
            IEnumerable<MarshalEntity> allMarshals = await _marshalRepository.GetByEventAsync(eventId);
            Dictionary<string, string> marshalNames = allMarshals.ToDictionary(m => m.RowKey, m => m.Name);

            // Build list of relevant items - iterate through each marshal to get their personal items
            List<ChecklistItemWithStatus> relevantItems = [];

            foreach (ChecklistItemEntity item in allItems.Where(IsItemVisible).OrderBy(i => i.DisplayOrder))
            {
                // For each marshal in this area, get their view of this item
                foreach (string marshalId in relevantMarshalIds)
                {
                    // Build context for this specific marshal
                    ChecklistScopeHelper.MarshalContext marshalContext = await BuildMarshalContext(eventId, marshalId);

                    // Get all contexts where this item is relevant to this marshal
                    List<ChecklistScopeHelper.ScopeMatchResult> contexts =
                        ChecklistScopeHelper.GetAllRelevantContexts(item, marshalContext, checkpointLookup);

                    foreach (ChecklistScopeHelper.ScopeMatchResult scopeContext in contexts)
                    {
                        if (scopeContext.IsRelevant && scopeContext.WinningConfig != null)
                        {
                            bool shouldInclude = false;

                            // Check if this context is relevant to this area
                            if (scopeContext.ContextType == "Personal")
                            {
                                // Personal items - include if marshal is in this area
                                ScopeConfiguration winningConfig = scopeContext.WinningConfig;

                                if (winningConfig.Scope == "Everyone")
                                {
                                    shouldInclude = true;
                                }
                                else if (winningConfig.Scope == "SpecificPeople")
                                {
                                    shouldInclude = true;
                                }
                                else if (winningConfig.Scope == "EveryoneAtCheckpoints")
                                {
                                    // Include if scoped to checkpoints in this area
                                    shouldInclude = winningConfig.Ids.Any(id => checkpointIds.Contains(id)) ||
                                                   winningConfig.Ids.Contains(Constants.AllCheckpoints);
                                }
                                else if (winningConfig.Scope == "EveryoneInAreas")
                                {
                                    // Include if scoped to this area or ALL areas
                                    shouldInclude = winningConfig.Ids.Contains(areaId) ||
                                                   winningConfig.Ids.Contains(Constants.AllAreas);
                                }
                                else if (winningConfig.Scope == "EveryAreaLead")
                                {
                                    // Include if scoped to this area or ALL areas
                                    shouldInclude = winningConfig.Ids.Contains(areaId) ||
                                                   winningConfig.Ids.Contains(Constants.AllAreas);
                                }
                            }
                            else if (scopeContext.ContextType == "Checkpoint")
                            {
                                // Shared checkpoint items - include if checkpoint is in this area
                                if (checkpointIds.Contains(scopeContext.ContextId))
                                {
                                    shouldInclude = true;
                                }
                            }
                            else if (scopeContext.ContextType == "Area")
                            {
                                // Shared area items - include if it's this area
                                if (scopeContext.ContextId == areaId)
                                {
                                    shouldInclude = true;
                                }
                            }

                            if (shouldInclude)
                            {
                                // Check if we already have this exact item instance
                                bool alreadyAdded = relevantItems.Any(existing =>
                                    existing.ItemId == item.ItemId &&
                                    existing.CompletionContextType == scopeContext.ContextType &&
                                    existing.CompletionContextId == scopeContext.ContextId);

                                if (!alreadyAdded)
                                {
                                    ChecklistItemWithStatus itemStatus = BuildItemWithStatus(item, marshalContext, checkpointLookup, allCompletions, scopeContext);

                                    // Set context owner name for personal items
                                    if (scopeContext.ContextType == "Personal" && marshalNames.TryGetValue(marshalId, out string? marshalName))
                                    {
                                        itemStatus = itemStatus with { ContextOwnerName = marshalName };
                                    }

                                    relevantItems.Add(itemStatus);
                                }
                            }
                        }
                    }
                }
            }

            return new OkObjectResult(relevantItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting area checklist");
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
        List<ChecklistCompletionEntity> allCompletions,
        ChecklistScopeHelper.ScopeMatchResult? scopeMatchResult = null)
    {
        List<ScopeConfiguration> scopeConfigurations =
            JsonSerializer.Deserialize<List<ScopeConfiguration>>(item.ScopeConfigurationsJson) ?? [];

        // Use provided scope match result if available, otherwise determine it
        string contextType;
        string contextId;
        string matchedScope;

        if (scopeMatchResult != null && scopeMatchResult.IsRelevant)
        {
            contextType = scopeMatchResult.ContextType;
            contextId = scopeMatchResult.ContextId;
            matchedScope = scopeMatchResult.WinningConfig?.Scope ?? string.Empty;
        }
        else
        {
            (contextType, contextId, matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);
        }

        // Check completion status for this specific context
        bool isCompleted;
        string? actorName = null;
        string? actorType = null;
        DateTime? completedAt = null;

        if (ChecklistScopeHelper.IsPersonalScope(matchedScope))
        {
            ChecklistCompletionEntity? completion = allCompletions.FirstOrDefault(c =>
                c.ChecklistItemId == item.ItemId &&
                c.ContextOwnerMarshalId == context.MarshalId &&
                !c.IsDeleted);

            isCompleted = completion != null;
            actorName = completion?.ActorName;
            actorType = completion?.ActorType;
            completedAt = completion?.CompletedAt;
        }
        else
        {
            // For shared scopes, check completion in this specific context
            ChecklistCompletionEntity? completion = allCompletions.FirstOrDefault(c =>
                c.ChecklistItemId == item.ItemId &&
                c.CompletionContextType == contextType &&
                c.CompletionContextId == contextId &&
                !c.IsDeleted);

            isCompleted = completion != null;
            actorName = completion?.ActorName;
            actorType = completion?.ActorType;
            completedAt = completion?.CompletedAt;
        }

        bool canComplete = ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup);

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
            actorName,
            actorType,
            completedAt,
            contextType,
            contextId,
            matchedScope,
            null,  // ContextOwnerName - will be set by caller if needed
            ChecklistScopeHelper.IsPersonalScope(matchedScope) ? context.MarshalId : null  // ContextOwnerMarshalId
        );
    }
}
