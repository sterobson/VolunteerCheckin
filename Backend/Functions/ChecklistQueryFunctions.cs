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
/// Functions for querying checklists by marshal, checkpoint, or area.
/// Separated from CRUD operations for better maintainability.
/// </summary>
public class ChecklistQueryFunctions
{
    private readonly ILogger<ChecklistQueryFunctions> _logger;
    private readonly IChecklistItemRepository _checklistItemRepository;
    private readonly IChecklistCompletionRepository _checklistCompletionRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly ChecklistContextHelper _contextHelper;

    public ChecklistQueryFunctions(
        ILogger<ChecklistQueryFunctions> logger,
        IChecklistItemRepository checklistItemRepository,
        IChecklistCompletionRepository checklistCompletionRepository,
        IMarshalRepository marshalRepository,
        ILocationRepository locationRepository,
        IAssignmentRepository assignmentRepository,
        IAreaRepository areaRepository,
        IEventRoleRepository eventRoleRepository)
    {
        _logger = logger;
        _checklistItemRepository = checklistItemRepository;
        _checklistCompletionRepository = checklistCompletionRepository;
        _marshalRepository = marshalRepository;
        _locationRepository = locationRepository;
        _contextHelper = new ChecklistContextHelper(assignmentRepository, locationRepository, areaRepository, eventRoleRepository, marshalRepository);
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
            ChecklistScopeHelper.MarshalContext context = await _contextHelper.BuildMarshalContextAsync(eventId, marshalId);

            // Build checkpoint lookup dictionary for area-based matching
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
            Dictionary<string, LocationEntity> checkpointLookup = allLocations.ToDictionary(l => l.RowKey);

            // Get all checklist items for event
            IEnumerable<ChecklistItemEntity> allItems = await _checklistItemRepository.GetByEventAsync(eventId);

            // Get all completions for event
            List<ChecklistCompletionEntity> allCompletions =
                [.. await _checklistCompletionRepository.GetByEventAsync(eventId)];

            // Build list of items with all their contexts
            List<ChecklistItemWithStatus> relevantItems = [];

            foreach (ChecklistItemEntity item in allItems.Where(ChecklistContextHelper.IsItemVisible).OrderBy(i => i.DisplayOrder))
            {
                // Get all contexts where this item is relevant to this marshal
                List<ChecklistScopeHelper.ScopeMatchResult> contexts =
                    ChecklistScopeHelper.GetAllRelevantContexts(item, context, checkpointLookup);

                // For each context, add an item instance
                foreach (ChecklistScopeHelper.ScopeMatchResult scopeContext in contexts)
                {
                    relevantItems.Add(ChecklistContextHelper.BuildItemWithStatus(item, context, checkpointLookup, allCompletions, scopeContext));
                }
            }

            return new OkObjectResult(ChecklistContextHelper.BuildNormalizedResponse(relevantItems));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting marshal checklist");
            return new StatusCodeResult(500);
        }
    }

#pragma warning disable MA0051
    [Function("GetCheckpointChecklist")]
    public async Task<IActionResult> GetCheckpointChecklist(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/locations/{locationId}/checklist")] HttpRequest req,
        string eventId,
        string locationId)
    {
        try
        {
            // Preload all event data in batch (3 DB calls instead of N*3)
            ChecklistContextHelper.PreloadedEventData preloaded = await _contextHelper.PreloadEventDataAsync(eventId);

            // Get the location to verify it exists and get its areas
            if (!preloaded.LocationsById.TryGetValue(locationId, out LocationEntity? location))
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorLocationNotFound });
            }

            List<string> locationAreaIds = JsonSerializer.Deserialize<List<string>>(location.AreaIdsJson) ?? [];

            // Get all marshals assigned to this checkpoint (from preloaded data)
            List<string> assignedMarshalIds = [.. preloaded.AssignmentsByMarshal
                .Where(kvp => kvp.Value.Any(a => a.LocationId == locationId))
                .Select(kvp => kvp.Key)
                .Distinct()];

            // Build checkpoint lookup dictionary from preloaded data
            Dictionary<string, LocationEntity> checkpointLookup = preloaded.LocationsById;

            // Get all checklist items for event
            IEnumerable<ChecklistItemEntity> allItems = await _checklistItemRepository.GetByEventAsync(eventId);

            // Get all completions for event
            List<ChecklistCompletionEntity> allCompletions =
                [.. await _checklistCompletionRepository.GetByEventAsync(eventId)];

            // Get area leads for checkpoint's areas (from preloaded EventRoles data)
            List<string> areaLeadIds = [.. preloaded.AreaLeadsByMarshalId
                .Where(kvp => kvp.Value.Any(areaId => locationAreaIds.Contains(areaId)))
                .Select(kvp => kvp.Key)
                .Distinct()];

            // Build set of all relevant marshal IDs (assigned + area leads)
            HashSet<string> relevantMarshalIds = [.. assignedMarshalIds, .. areaLeadIds];

            // Get all marshals to get their names
            IEnumerable<MarshalEntity> allMarshals = await _marshalRepository.GetByEventAsync(eventId);
            Dictionary<string, string> marshalNames = allMarshals.ToDictionary(m => m.RowKey, m => m.Name);

            // Build marshal contexts for all relevant marshals at once (no DB calls in loop)
            Dictionary<string, ChecklistScopeHelper.MarshalContext> marshalContexts =
                ChecklistContextHelper.BuildMarshalContextsFromPreloaded(relevantMarshalIds, preloaded);

            // Build list of relevant items - iterate through each marshal to get their personal items
            List<ChecklistItemWithStatus> relevantItems = [];

            foreach (ChecklistItemEntity item in allItems.Where(ChecklistContextHelper.IsItemVisible).OrderBy(i => i.DisplayOrder))
            {
                _logger.LogInformation("Processing item: {ItemText} ({ItemId})", item.Text, item.ItemId);

                // For each marshal at this checkpoint, get their view of this item
                foreach (string marshalId in relevantMarshalIds)
                {
                    // Get context from prebuilt dictionary (no DB call)
                    ChecklistScopeHelper.MarshalContext marshalContext = marshalContexts[marshalId];

                    _logger.LogInformation("  Marshal {MarshalName}: Areas={AssignedAreaIds}", marshalNames.GetValueOrDefault(marshalId, marshalId), string.Join(",", marshalContext.AssignedAreaIds));

                    // Get all contexts where this item is relevant to this marshal
                    List<ChecklistScopeHelper.ScopeMatchResult> contexts =
                        ChecklistScopeHelper.GetAllRelevantContexts(item, marshalContext, checkpointLookup);

                    _logger.LogInformation("    Found {ContextCount} context(s)", contexts.Count);

                    // Only include contexts that are for this checkpoint or its areas
                    foreach (ChecklistScopeHelper.ScopeMatchResult scopeContext in contexts)
                    {
                        bool shouldInclude = ShouldIncludeForCheckpoint(scopeContext, locationId, locationAreaIds, marshalContext);

                        if (shouldInclude)
                        {
                            _logger.LogInformation("        Checking deduplication: ItemId={ItemId}, ContextType={ContextType}, ContextId={ContextId}", item.ItemId, scopeContext.ContextType, scopeContext.ContextId);

                            // Check if we already have this exact item instance
                            bool alreadyAdded = relevantItems.Any(existing =>
                                existing.ItemId == item.ItemId &&
                                existing.CompletionContextType == scopeContext.ContextType &&
                                existing.CompletionContextId == scopeContext.ContextId);

                            _logger.LogInformation("        Already added: {AlreadyAdded}", alreadyAdded);

                            if (!alreadyAdded)
                            {
                                ChecklistItemWithStatus itemStatus = ChecklistContextHelper.BuildItemWithStatus(item, marshalContext, checkpointLookup, allCompletions, scopeContext);

                                // Set context owner name for personal items
                                if (scopeContext.ContextType == "Personal" && marshalNames.TryGetValue(marshalId, out string? marshalName))
                                {
                                    itemStatus = itemStatus with { ContextOwnerName = marshalName };
                                }

                                _logger.LogInformation("        ADDED item for {MarshalName}", marshalNames.GetValueOrDefault(marshalId, marshalId));
                                relevantItems.Add(itemStatus);
                            }
                            else
                            {
                                _logger.LogInformation("        SKIPPED (duplicate)");
                            }
                        }
                    }
                }
            }

            return new OkObjectResult(ChecklistContextHelper.BuildNormalizedResponse(relevantItems));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checkpoint checklist");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

#pragma warning disable MA0051
    [Function("GetAreaChecklist")]
    public async Task<IActionResult> GetAreaChecklist(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/areas/{areaId}/checklist")] HttpRequest req,
        string eventId,
        string areaId)
    {
        try
        {
            // Preload all event data in batch (3 DB calls instead of N*3)
            ChecklistContextHelper.PreloadedEventData preloaded = await _contextHelper.PreloadEventDataAsync(eventId);

            // Get the area to verify it exists
            AreaEntity? area = preloaded.Areas.FirstOrDefault(a => a.RowKey == areaId);
            if (area == null)
            {
                return new NotFoundObjectResult(new { message = "Area not found" });
            }

            // Get all checkpoints in this area (from preloaded data)
            List<LocationEntity> areaCheckpoints = [.. preloaded.LocationsById.Values
                .Where(l =>
                {
                    List<string> locationAreaIds = JsonSerializer.Deserialize<List<string>>(l.AreaIdsJson) ?? [];
                    return locationAreaIds.Contains(areaId);
                })];

            List<string> checkpointIds = [.. areaCheckpoints.Select(l => l.RowKey)];

            // Get all marshals assigned to checkpoints in this area (from preloaded data, no loop DB calls)
            List<string> assignedMarshalIds = [.. preloaded.AssignmentsByMarshal
                .Where(kvp => kvp.Value.Any(a => checkpointIds.Contains(a.LocationId)))
                .Select(kvp => kvp.Key)
                .Distinct()];

            // Build checkpoint lookup dictionary from preloaded data
            Dictionary<string, LocationEntity> checkpointLookup = preloaded.LocationsById;

            // Get all checklist items for event
            IEnumerable<ChecklistItemEntity> allItems = await _checklistItemRepository.GetByEventAsync(eventId);

            // Get all completions for event
            List<ChecklistCompletionEntity> allCompletions =
                [.. await _checklistCompletionRepository.GetByEventAsync(eventId)];

            // Get area leads for this area (from preloaded EventRoles data)
            List<string> areaLeadIds = [.. preloaded.AreaLeadsByMarshalId
                .Where(kvp => kvp.Value.Contains(areaId))
                .Select(kvp => kvp.Key)];

            // Build set of all relevant marshal IDs (assigned + area leads)
            HashSet<string> relevantMarshalIds = [.. assignedMarshalIds, .. areaLeadIds];

            // Get all marshals to get their names
            IEnumerable<MarshalEntity> allMarshals = await _marshalRepository.GetByEventAsync(eventId);
            Dictionary<string, string> marshalNames = allMarshals.ToDictionary(m => m.RowKey, m => m.Name);

            // Build marshal contexts for all relevant marshals at once (no DB calls in loop)
            Dictionary<string, ChecklistScopeHelper.MarshalContext> marshalContexts =
                ChecklistContextHelper.BuildMarshalContextsFromPreloaded(relevantMarshalIds, preloaded);

            // Build list of relevant items - iterate through each marshal to get their personal items
            List<ChecklistItemWithStatus> relevantItems = [];

            foreach (ChecklistItemEntity item in allItems.Where(ChecklistContextHelper.IsItemVisible).OrderBy(i => i.DisplayOrder))
            {
                // For each marshal in this area, get their view of this item
                foreach (string marshalId in relevantMarshalIds)
                {
                    // Get context from prebuilt dictionary (no DB call)
                    ChecklistScopeHelper.MarshalContext marshalContext = marshalContexts[marshalId];

                    // Get all contexts where this item is relevant to this marshal
                    List<ChecklistScopeHelper.ScopeMatchResult> contexts =
                        ChecklistScopeHelper.GetAllRelevantContexts(item, marshalContext, checkpointLookup);

                    foreach (ChecklistScopeHelper.ScopeMatchResult scopeContext in contexts)
                    {
                        if (scopeContext.IsRelevant && scopeContext.WinningConfig != null)
                        {
                            bool shouldInclude = ShouldIncludeForArea(scopeContext, areaId, checkpointIds);

                            if (shouldInclude)
                            {
                                // Check if we already have this exact item instance
                                // For personal scopes and shared scopes, include marshal ID in dedup check
                                // This gives us one row per marshal for shared tasks (so area lead sees each person)
                                string scope = scopeContext.WinningConfig?.Scope ?? "";
                                bool isPersonalScope = ChecklistScopeHelper.IsPersonalScope(scope);
                                bool isSharedScope = scope == Constants.ChecklistScopeOnePerCheckpoint ||
                                                     scope == Constants.ChecklistScopeOnePerArea ||
                                                     scope == Constants.ChecklistScopeOneLeadPerArea;

                                // For OnePerCheckpoint, only include marshals actually assigned to that checkpoint
                                if (scope == Constants.ChecklistScopeOnePerCheckpoint &&
                                    scopeContext.ContextType == Constants.ChecklistContextCheckpoint &&
                                    !marshalContext.AssignedLocationIds.Contains(scopeContext.ContextId))
                                {
                                    continue; // Skip - marshal not assigned to this checkpoint
                                }

                                // For OnePerArea, only include marshals actually in that area
                                if (scope == Constants.ChecklistScopeOnePerArea &&
                                    scopeContext.ContextType == Constants.ChecklistContextArea &&
                                    !marshalContext.AssignedAreaIds.Contains(scopeContext.ContextId))
                                {
                                    continue; // Skip - marshal not in this area
                                }

                                bool includesMarshalInKey = isPersonalScope || isSharedScope;

                                bool alreadyAdded = relevantItems.Any(existing =>
                                    existing.ItemId == item.ItemId &&
                                    existing.CompletionContextType == scopeContext.ContextType &&
                                    existing.CompletionContextId == scopeContext.ContextId &&
                                    (!includesMarshalInKey || existing.ContextOwnerMarshalId == marshalId));

                                if (!alreadyAdded)
                                {
                                    // For shared scopes, use per-marshal view to show individual completion status
                                    ChecklistItemWithStatus itemStatus = ChecklistContextHelper.BuildItemWithStatus(
                                        item, marshalContext, checkpointLookup, allCompletions, scopeContext,
                                        perMarshalView: isSharedScope);

                                    // Set context owner name for personal items, linked tasks, and shared scopes
                                    bool needsOwnerName = scopeContext.ContextType == "Personal" ||
                                        (item.LinksToCheckIn && scopeContext.ContextType == Constants.ChecklistContextCheckpoint) ||
                                        isSharedScope;
                                    if (needsOwnerName && marshalNames.TryGetValue(marshalId, out string? marshalName))
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

            return new OkObjectResult(ChecklistContextHelper.BuildNormalizedResponse(relevantItems));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting area checklist");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Determines if a scope context should be included for a checkpoint view.
    /// </summary>
    private static bool ShouldIncludeForCheckpoint(
        ChecklistScopeHelper.ScopeMatchResult scopeContext,
        string locationId,
        List<string> locationAreaIds,
        ChecklistScopeHelper.MarshalContext marshalContext)
    {
        if (scopeContext.ContextType == "Personal")
        {
            ScopeConfiguration? winningConfig = scopeContext.WinningConfig;
            if (winningConfig == null) return false;

            return winningConfig.Scope switch
            {
                "Everyone" => true,
                "SpecificPeople" when winningConfig.ItemType == "Marshal" => true,
                "EveryoneAtCheckpoints" => winningConfig.Ids.Contains(locationId) ||
                                          winningConfig.Ids.Contains(Constants.AllCheckpoints),
                "EveryoneInAreas" => winningConfig.Ids.Contains(Constants.AllAreas) ||
                                    marshalContext.AssignedAreaIds.Any(areaId => winningConfig.Ids.Contains(areaId)),
                "EveryAreaLead" => winningConfig.Ids.Contains(Constants.AllAreas) ||
                                  marshalContext.AreaLeadForAreaIds.Any(areaId => winningConfig.Ids.Contains(areaId)),
                _ => false
            };
        }
        else if (scopeContext.ContextType == "Checkpoint")
        {
            return scopeContext.ContextId == locationId;
        }
        else if (scopeContext.ContextType == "Area")
        {
            return locationAreaIds.Contains(scopeContext.ContextId);
        }

        return false;
    }

    /// <summary>
    /// Determines if a scope context should be included for an area view.
    /// </summary>
    private static bool ShouldIncludeForArea(
        ChecklistScopeHelper.ScopeMatchResult scopeContext,
        string areaId,
        List<string> checkpointIds)
    {
        if (scopeContext.ContextType == "Personal")
        {
            ScopeConfiguration winningConfig = scopeContext.WinningConfig!;

            return winningConfig.Scope switch
            {
                "Everyone" => true,
                "SpecificPeople" => true,
                "EveryoneAtCheckpoints" => winningConfig.Ids.Any(id => checkpointIds.Contains(id)) ||
                                          winningConfig.Ids.Contains(Constants.AllCheckpoints),
                "EveryoneInAreas" => winningConfig.Ids.Contains(areaId) ||
                                    winningConfig.Ids.Contains(Constants.AllAreas),
                "EveryAreaLead" => winningConfig.Ids.Contains(areaId) ||
                                  winningConfig.Ids.Contains(Constants.AllAreas),
                _ => false
            };
        }
        else if (scopeContext.ContextType == "Checkpoint")
        {
            return checkpointIds.Contains(scopeContext.ContextId);
        }
        else if (scopeContext.ContextType == "Area")
        {
            return scopeContext.ContextId == areaId;
        }

        return false;
    }
}
