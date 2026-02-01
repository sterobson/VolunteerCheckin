using System.Text.Json;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Helpers;

/// <summary>
/// Helper class for building checklist contexts and status.
/// Shared across ChecklistFunctions, ChecklistQueryFunctions, and ChecklistCompletionFunctions.
/// </summary>
public class ChecklistContextHelper
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IAreaRepository _areaRepository;
    private readonly IEventRoleRepository _eventRoleRepository;
    private readonly IMarshalRepository _marshalRepository;

    public ChecklistContextHelper(
        IAssignmentRepository assignmentRepository,
        ILocationRepository locationRepository,
        IAreaRepository areaRepository,
        IEventRoleRepository eventRoleRepository,
        IMarshalRepository marshalRepository)
    {
        _assignmentRepository = assignmentRepository;
        _locationRepository = locationRepository;
        _areaRepository = areaRepository;
        _eventRoleRepository = eventRoleRepository;
        _marshalRepository = marshalRepository;
    }

    /// <summary>
    /// Preloaded event data for building marshal contexts efficiently.
    /// Load once per request, then build multiple contexts without additional DB calls.
    /// </summary>
    public record PreloadedEventData(
        Dictionary<string, List<AssignmentEntity>> AssignmentsByMarshal,
        Dictionary<string, LocationEntity> LocationsById,
        List<AreaEntity> Areas,
        Dictionary<string, List<string>> AreaLeadsByMarshalId
    );

    /// <summary>
    /// Builds marshal context by loading data from repositories.
    /// </summary>
    public async Task<ChecklistScopeHelper.MarshalContext> BuildMarshalContextAsync(string eventId, string marshalId)
    {
        // Get marshal's assignments
        IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
        List<string> assignedLocationIds = assignments.Select(a => a.LocationId).ToList();

        // Get locations to determine areas
        IEnumerable<LocationEntity> locations = await _locationRepository.GetByEventAsync(eventId);
        List<string> assignedAreaIds = locations
            .Where(l => assignedLocationIds.Contains(l.RowKey))
            .SelectMany(l => l.GetPayload().AreaIds)
            .Distinct()
            .ToList();

        // Get area lead status from EventRoles
        // First get the marshal to find their PersonId
        MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, marshalId);
        List<string> areaLeadForAreaIds = [];

        if (marshal != null && !string.IsNullOrEmpty(marshal.PersonId))
        {
            // Get EventAreaLead roles for this person in this event
            IEnumerable<EventRoleEntity> roles = await _eventRoleRepository.GetByPersonAndEventAsync(marshal.PersonId, eventId);
            EventRoleEntity? areaLeadRole = roles.FirstOrDefault(r => r.Role == Constants.RoleEventAreaLead);

            if (areaLeadRole != null)
            {
                areaLeadForAreaIds = JsonSerializer.Deserialize<List<string>>(areaLeadRole.AreaIdsJson) ?? [];
            }
        }

        return new ChecklistScopeHelper.MarshalContext(
            marshalId,
            assignedAreaIds,
            assignedLocationIds,
            areaLeadForAreaIds
        );
    }

    /// <summary>
    /// Loads all data needed for building marshal contexts in a single batch.
    /// This avoids N+1 queries when processing multiple marshals.
    /// </summary>
    public async Task<PreloadedEventData> PreloadEventDataAsync(string eventId)
    {
        // Load all assignments for event (single DB call)
        IEnumerable<AssignmentEntity> allAssignments = await _assignmentRepository.GetByEventAsync(eventId);
        Dictionary<string, List<AssignmentEntity>> assignmentsByMarshal = allAssignments
            .GroupBy(a => a.MarshalId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load all locations for event (single DB call)
        IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
        Dictionary<string, LocationEntity> locationsById = allLocations.ToDictionary(l => l.RowKey);

        // Load all areas for event (single DB call)
        List<AreaEntity> areas = (await _areaRepository.GetByEventAsync(eventId)).ToList();

        // Load all marshals to build PersonId -> MarshalId lookup
        IEnumerable<MarshalEntity> allMarshals = await _marshalRepository.GetByEventAsync(eventId);
        Dictionary<string, string> marshalIdByPersonId = allMarshals
            .Where(m => !string.IsNullOrEmpty(m.PersonId))
            .ToDictionary(m => m.PersonId, m => m.MarshalId);

        // Load EventAreaLead roles and build MarshalId -> AreaIds lookup
        IEnumerable<EventRoleEntity> allRoles = await _eventRoleRepository.GetByEventAsync(eventId);
        Dictionary<string, List<string>> areaLeadsByMarshalId = allRoles
            .Where(r => r.Role == Constants.RoleEventAreaLead && marshalIdByPersonId.ContainsKey(r.PersonId))
            .ToDictionary(
                r => marshalIdByPersonId[r.PersonId],
                r => JsonSerializer.Deserialize<List<string>>(r.AreaIdsJson) ?? []
            );

        return new PreloadedEventData(assignmentsByMarshal, locationsById, areas, areaLeadsByMarshalId);
    }

    /// <summary>
    /// Builds marshal context using preloaded data (no DB calls).
    /// </summary>
    public static ChecklistScopeHelper.MarshalContext BuildMarshalContextFromPreloaded(
        string marshalId,
        PreloadedEventData preloaded)
    {
        // Get marshal's assignments from preloaded data
        List<AssignmentEntity> assignments = preloaded.AssignmentsByMarshal.GetValueOrDefault(marshalId, []);
        List<string> assignedLocationIds = assignments.Select(a => a.LocationId).ToList();

        // Determine areas from assigned locations
        List<string> assignedAreaIds = assignedLocationIds
            .Where(locId => preloaded.LocationsById.ContainsKey(locId))
            .SelectMany(locId =>
            {
                LocationEntity loc = preloaded.LocationsById[locId];
                return loc.GetPayload().AreaIds;
            })
            .Distinct()
            .ToList();

        // Get area lead status from preloaded EventRoles data
        List<string> areaLeadForAreaIds = preloaded.AreaLeadsByMarshalId.GetValueOrDefault(marshalId, []);

        return new ChecklistScopeHelper.MarshalContext(
            marshalId,
            assignedAreaIds,
            assignedLocationIds,
            areaLeadForAreaIds
        );
    }

    /// <summary>
    /// Builds marshal contexts for multiple marshals using preloaded data (no DB calls in loop).
    /// </summary>
    public static Dictionary<string, ChecklistScopeHelper.MarshalContext> BuildMarshalContextsFromPreloaded(
        IEnumerable<string> marshalIds,
        PreloadedEventData preloaded)
    {
        return marshalIds.ToDictionary(
            marshalId => marshalId,
            marshalId => BuildMarshalContextFromPreloaded(marshalId, preloaded)
        );
    }

    /// <summary>
    /// Checks if a checklist item is currently visible based on its time constraints.
    /// </summary>
    public static bool IsItemVisible(ChecklistItemEntity item)
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

    /// <summary>
    /// Builds a ChecklistItemWithStatus from an item and its context.
    /// </summary>
    /// <param name="item">The checklist item entity</param>
    /// <param name="context">The marshal context</param>
    /// <param name="checkpointLookup">Dictionary of checkpoints by ID</param>
    /// <param name="allCompletions">All completions for the event</param>
    /// <param name="scopeMatchResult">Optional pre-computed scope match result</param>
    /// <param name="perMarshalView">If true, for shared scopes show per-marshal completion status (for area lead view)</param>
#pragma warning disable MA0051 // Method is too long - builds complex DTO with multiple fields
    public static ChecklistItemWithStatus BuildItemWithStatus(
        ChecklistItemEntity item,
        ChecklistScopeHelper.MarshalContext context,
        Dictionary<string, LocationEntity> checkpointLookup,
        List<ChecklistCompletionEntity> allCompletions,
        ChecklistScopeHelper.ScopeMatchResult? scopeMatchResult = null,
        bool perMarshalView = false)
    {
        List<ScopeConfiguration> scopeConfigurations =
            JsonSerializer.Deserialize<List<ScopeConfiguration>>(item.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [];

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
        string? actorId = null;
        DateTime? completedAt = null;

        // Determine how to look up completion based on scope type:
        // - Personal scopes: look up by marshal ID (each marshal has their own completion)
        // - Linked tasks: look up by context + marshal ID (each marshal has their own linked task per checkpoint)
        // - Shared scopes (OnePerCheckpoint, OnePerArea): look up by context only (any completion counts)
        //   UNLESS perMarshalView is true, then show per-marshal status for area lead view
        bool isPersonalScope = ChecklistScopeHelper.IsPersonalScope(matchedScope);
        bool isSharedScope = contextType == Constants.ChecklistContextCheckpoint || contextType == Constants.ChecklistContextArea;
        bool isLinkedTask = item.LinksToCheckIn;

        ChecklistCompletionEntity? completion;
        ChecklistCompletionEntity? anyCompletionInContext = null;

        if (isLinkedTask)
        {
            // Linked tasks: each marshal has their own task per checkpoint, filter by marshal
            completion = allCompletions.FirstOrDefault(c =>
                c.ChecklistItemId == item.ItemId &&
                c.CompletionContextType == contextType &&
                c.CompletionContextId == contextId &&
                c.ContextOwnerMarshalId == context.MarshalId &&
                !c.IsDeleted);
        }
        else if (isSharedScope)
        {
            // For shared scopes, first find if anyone completed it
            anyCompletionInContext = allCompletions.FirstOrDefault(c =>
                c.ChecklistItemId == item.ItemId &&
                c.CompletionContextType == contextType &&
                c.CompletionContextId == contextId &&
                !c.IsDeleted);

            if (perMarshalView)
            {
                // Per-marshal view: check if THIS marshal completed it
                completion = (anyCompletionInContext?.ContextOwnerMarshalId == context.MarshalId)
                    ? anyCompletionInContext
                    : null;
            }
            else
            {
                // Normal view: any completion counts
                completion = anyCompletionInContext;
            }
        }
        else if (isPersonalScope)
        {
            // Personal scopes: look up by marshal ID
            completion = allCompletions.FirstOrDefault(c =>
                c.ChecklistItemId == item.ItemId &&
                c.ContextOwnerMarshalId == context.MarshalId &&
                !c.IsDeleted);
        }
        else
        {
            completion = null;
        }

        isCompleted = completion != null;
        actorName = completion?.ActorName;
        actorType = completion?.ActorType;
        actorId = completion?.ActorId;
        completedAt = completion?.CompletedAt;

        // Determine if this marshal can complete/toggle the item
        bool canComplete = ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup);

        if (isSharedScope)
        {
            bool taskIsCompletedByAnyone = anyCompletionInContext != null || completion != null;
            // Check if this marshal completed it (they can uncomplete their own completion)
            bool completedByThisMarshal = anyCompletionInContext?.ContextOwnerMarshalId == context.MarshalId;

            if (perMarshalView)
            {
                // In per-marshal view: only the person who completed it can toggle (uncomplete)
                // Others are disabled
                if (taskIsCompletedByAnyone && !isCompleted)
                {
                    canComplete = false;
                }
            }
            else if (taskIsCompletedByAnyone && !completedByThisMarshal)
            {
                // Normal view: if someone else completed, this marshal cannot complete/uncomplete
                // But if this marshal completed it, they can uncomplete it
                canComplete = false;
            }
        }

        // For linked tasks, determine the checkpoint ID and name
        string? linkedCheckpointId = null;
        string? linkedCheckpointName = null;

        if (item.LinksToCheckIn && contextType == Constants.ChecklistContextCheckpoint && !string.IsNullOrEmpty(contextId))
        {
            linkedCheckpointId = contextId;
            if (checkpointLookup.TryGetValue(contextId, out LocationEntity? checkpoint))
            {
                linkedCheckpointName = checkpoint.Name;
            }
        }

        // Set ContextOwnerMarshalId:
        // - For personal scopes: always set to marshal ID
        // - For shared scopes in per-marshal view: set to marshal ID (so frontend can distinguish marshals)
        // - For shared scopes in normal view: null (task belongs to context, not individual)
        string? contextOwnerMarshalId = null;
        if (ChecklistScopeHelper.IsPersonalScope(matchedScope) || (perMarshalView && isSharedScope))
        {
            contextOwnerMarshalId = context.MarshalId;
        }

        // Set ContextOwnerName from the completion record for shared scopes
        // This enables "on behalf of" display when someone completes a shared task for another person
        string? contextOwnerName = null;
        if (isSharedScope && anyCompletionInContext != null && !string.IsNullOrEmpty(anyCompletionInContext.ContextOwnerMarshalName))
        {
            contextOwnerName = anyCompletionInContext.ContextOwnerMarshalName;
        }

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
            actorId,
            completedAt,
            contextType,
            contextId,
            matchedScope,
            contextOwnerName,
            contextOwnerMarshalId,
            item.LinksToCheckIn,
            linkedCheckpointId,
            linkedCheckpointName
        );
    }

    /// <summary>
    /// Field mapping for debugging - maps short field names to full names.
    /// </summary>
    private static readonly Dictionary<string, string> FieldMap = new()
    {
        // Response level
        ["_d"] = "defaults (bool defaults for omitted properties)",
        ["g"] = "refs (GUIDs)",
        ["s"] = "scopes",
        ["at"] = "actorTypes",
        ["ct"] = "contextTypes",
        ["m"] = "marshals",
        ["c"] = "contexts",
        ["d"] = "items (definitions)",
        ["n"] = "instances",
        // Marshal/Context reference
        ["m.r"] = "refIndex",
        ["m.n"] = "name",
        ["c.r"] = "refIndex",
        ["c.t"] = "typeIndex",
        // Item definition
        ["d.r"] = "itemRefIndex",
        ["d.t"] = "text",
        ["d.sc"] = "scopeConfigurations",
        ["d.o"] = "displayOrder",
        ["d.r2"] = "isRequired",
        ["d.vf"] = "visibleFrom",
        ["d.vu"] = "visibleUntil",
        ["d.mb"] = "mustCompleteBy",
        ["d.l"] = "linksToCheckIn",
        ["d.lc"] = "linkedCheckpointRefIndex",
        ["d.ln"] = "linkedCheckpointName",
        // ScopeConfiguration (inside d.sc array)
        ["sc.s"] = "scopeIndex",
        ["sc.t"] = "itemType",
        ["sc.i"] = "refIndexes",
        // Instance
        ["n.i"] = "itemIndex",
        ["n.c"] = "isCompleted",
        ["n.m"] = "canBeCompletedByMe",
        ["n.a"] = "actorIndex",
        ["n.at"] = "actorTypeIndex",
        ["n.ca"] = "completedAt",
        ["n.x"] = "contextIndex",
        ["n.s"] = "scopeIndex",
        ["n.o"] = "ownerIndex"
    };

    /// <summary>
    /// Converts a list of ChecklistItemWithStatus to a normalized response with lookup tables.
    /// This significantly reduces payload size by deduplicating repeated strings and item definitions.
    /// All GUIDs are stored in a refs array and referenced by index.
    /// Boolean properties use computed defaults - omitted values use the default from _d.
    /// </summary>
    public static NormalizedChecklistResponse BuildNormalizedResponse(List<ChecklistItemWithStatus> items)
    {
        // Build lookup tables
        List<string> refs = [];          // All unique GUIDs
        List<string> scopes = [];
        List<string> actorTypes = [];
        List<string> contextTypes = [];
        List<MarshalReference> marshals = [];
        List<ContextReference> contexts = [];
        List<ChecklistItemDefinition> itemDefinitions = [];
        List<ChecklistInstance> instances = [];

        // Dictionaries for index lookups
        Dictionary<string, int> refIndex = [];      // GUID -> index in refs
        Dictionary<string, int> scopeIndex = [];
        Dictionary<string, int> actorTypeIndex = [];
        Dictionary<string, int> contextTypeIndex = [];
        Dictionary<string, int> marshalIndex = [];  // keyed by marshal ID
        Dictionary<string, int> contextIndex = [];  // keyed by "contextType:contextId"
        Dictionary<string, int> itemIndex = [];     // keyed by itemId

        // Compute defaults for boolean properties by counting occurrences
        // Instance booleans
        int isCompletedTrue = items.Count(i => i.IsCompleted);
        int canBeCompletedByMeTrue = items.Count(i => i.CanBeCompletedByMe);

        // Item definition booleans (count unique items only)
        HashSet<string> seenItemIds = [];
        int isRequiredTrue = 0;
        int linksToCheckInTrue = 0;
        int uniqueItemCount = 0;
        foreach (ChecklistItemWithStatus item in items)
        {
            if (seenItemIds.Add(item.ItemId))
            {
                uniqueItemCount++;
                if (item.IsRequired) isRequiredTrue++;
                if (item.LinksToCheckIn) linksToCheckInTrue++;
            }
        }

        // Defaults are the majority value (>= 50% means default is true)
        bool defaultIsCompleted = isCompletedTrue >= items.Count - isCompletedTrue;
        bool defaultCanBeCompletedByMe = canBeCompletedByMeTrue >= items.Count - canBeCompletedByMeTrue;
        bool defaultIsRequired = isRequiredTrue >= uniqueItemCount - isRequiredTrue;
        bool defaultLinksToCheckIn = linksToCheckInTrue >= uniqueItemCount - linksToCheckInTrue;

        Dictionary<string, bool> defaults = new()
        {
            ["n.c"] = defaultIsCompleted,
            ["n.m"] = defaultCanBeCompletedByMe,
            ["d.r2"] = defaultIsRequired,
            ["d.l"] = defaultLinksToCheckIn
        };

        int GetOrAddRef(string? guid)
        {
            if (string.IsNullOrEmpty(guid)) return -1;
            if (!refIndex.TryGetValue(guid, out int index))
            {
                index = refs.Count;
                refIndex[guid] = index;
                refs.Add(guid);
            }
            return index;
        }

        int GetOrAddScope(string scope)
        {
            if (string.IsNullOrEmpty(scope)) scope = "";
            if (!scopeIndex.TryGetValue(scope, out int index))
            {
                index = scopes.Count;
                scopeIndex[scope] = index;
                scopes.Add(scope);
            }
            return index;
        }

        int GetOrAddActorType(string? type)
        {
            if (string.IsNullOrEmpty(type)) return -1;
            if (!actorTypeIndex.TryGetValue(type, out int index))
            {
                index = actorTypes.Count;
                actorTypeIndex[type] = index;
                actorTypes.Add(type);
            }
            return index;
        }

        int GetOrAddContextType(string contextType)
        {
            if (string.IsNullOrEmpty(contextType)) contextType = "";
            if (!contextTypeIndex.TryGetValue(contextType, out int index))
            {
                index = contextTypes.Count;
                contextTypeIndex[contextType] = index;
                contextTypes.Add(contextType);
            }
            return index;
        }

        int GetOrAddMarshal(string? marshalId, string? marshalName)
        {
            if (string.IsNullOrEmpty(marshalId)) return -1;
            if (!marshalIndex.TryGetValue(marshalId, out int index))
            {
                index = marshals.Count;
                marshalIndex[marshalId] = index;
                int rIdx = GetOrAddRef(marshalId);
                marshals.Add(new MarshalReference(rIdx, marshalName ?? ""));
            }
            return index;
        }

        int GetOrAddContext(string contextType, string contextId)
        {
            string key = $"{contextType}:{contextId}";
            if (!contextIndex.TryGetValue(key, out int index))
            {
                int typeIdx = GetOrAddContextType(contextType);
                int rIdx = GetOrAddRef(contextId);
                index = contexts.Count;
                contextIndex[key] = index;
                contexts.Add(new ContextReference(rIdx, typeIdx));
            }
            return index;
        }

        // Track which items have been added to avoid duplicate definitions
        HashSet<string> addedItemIds = [];

        int GetOrAddItem(ChecklistItemWithStatus item)
        {
            if (!itemIndex.TryGetValue(item.ItemId, out int index))
            {
                index = itemDefinitions.Count;
                itemIndex[item.ItemId] = index;

                int itemRefIdx = GetOrAddRef(item.ItemId);

                // Convert ScopeConfiguration to CompactScopeConfiguration with ref indexes
                List<CompactScopeConfiguration> compactScopes = item.ScopeConfigurations
                    .Select(sc => new CompactScopeConfiguration(
                        GetOrAddScope(sc.Scope),
                        sc.ItemType,
                        sc.Ids.Select(id => GetOrAddRef(id)).ToList()
                    ))
                    .ToList();

                int? linkedCpRefIdx = string.IsNullOrEmpty(item.LinkedCheckpointId)
                    ? null
                    : GetOrAddRef(item.LinkedCheckpointId);

                // Use null for booleans that match their default (will be omitted from JSON)
                bool? isRequired = item.IsRequired == defaultIsRequired ? null : item.IsRequired;
                bool? linksToCheckIn = item.LinksToCheckIn == defaultLinksToCheckIn ? null : item.LinksToCheckIn;

                itemDefinitions.Add(new ChecklistItemDefinition(
                    itemRefIdx,
                    item.Text,
                    compactScopes,
                    item.DisplayOrder,
                    isRequired,
                    item.VisibleFrom,
                    item.VisibleUntil,
                    item.MustCompleteBy,
                    linksToCheckIn,
                    linkedCpRefIdx,
                    item.LinkedCheckpointName
                ));

                addedItemIds.Add(item.ItemId);
            }
            return index;
        }

        // Process all items
        foreach (ChecklistItemWithStatus item in items)
        {
            int itemIdx = GetOrAddItem(item);
            int scopeIdx = GetOrAddScope(item.MatchedScope);
            int ctxIdx = GetOrAddContext(item.CompletionContextType, item.CompletionContextId);

            // Actor info (only if completed)
            int? actorIdx = null;
            int? actorTypeIdx = null;
            if (item.IsCompleted && !string.IsNullOrEmpty(item.CompletedByActorId))
            {
                actorIdx = GetOrAddMarshal(item.CompletedByActorId, item.CompletedByActorName);
                int atIdx = GetOrAddActorType(item.CompletedByActorType);
                actorTypeIdx = atIdx >= 0 ? atIdx : null;
            }

            // Owner info
            int? ownerIdx = null;
            if (!string.IsNullOrEmpty(item.ContextOwnerMarshalId))
            {
                ownerIdx = GetOrAddMarshal(item.ContextOwnerMarshalId, item.ContextOwnerName);
            }

            // Use null for booleans that match their default (will be omitted from JSON)
            bool? isCompleted = item.IsCompleted == defaultIsCompleted ? null : item.IsCompleted;
            bool? canBeCompletedByMe = item.CanBeCompletedByMe == defaultCanBeCompletedByMe ? null : item.CanBeCompletedByMe;

            instances.Add(new ChecklistInstance(
                itemIdx,
                isCompleted,
                canBeCompletedByMe,
                actorIdx,
                actorTypeIdx,
                item.CompletedAt,
                ctxIdx,
                scopeIdx,
                ownerIdx
            ));
        }

        return new NormalizedChecklistResponse(
            FieldMap,
            defaults,
            refs,
            scopes,
            actorTypes,
            contextTypes,
            marshals,
            contexts,
            itemDefinitions,
            instances
        );
    }
}
