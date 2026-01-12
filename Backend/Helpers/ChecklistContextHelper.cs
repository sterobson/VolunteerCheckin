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
            .SelectMany(l => JsonSerializer.Deserialize<List<string>>(l.AreaIdsJson) ?? [])
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
                return JsonSerializer.Deserialize<List<string>>(loc.AreaIdsJson) ?? [];
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
#pragma warning disable MA0051 // Method is too long - builds complex DTO with multiple fields
    public static ChecklistItemWithStatus BuildItemWithStatus(
        ChecklistItemEntity item,
        ChecklistScopeHelper.MarshalContext context,
        Dictionary<string, LocationEntity> checkpointLookup,
        List<ChecklistCompletionEntity> allCompletions,
        ChecklistScopeHelper.ScopeMatchResult? scopeMatchResult = null)
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

        if (ChecklistScopeHelper.IsPersonalScope(matchedScope))
        {
            ChecklistCompletionEntity? completion = allCompletions.FirstOrDefault(c =>
                c.ChecklistItemId == item.ItemId &&
                c.ContextOwnerMarshalId == context.MarshalId &&
                !c.IsDeleted);

            isCompleted = completion != null;
            actorName = completion?.ActorName;
            actorType = completion?.ActorType;
            actorId = completion?.ActorId;
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
            actorId = completion?.ActorId;
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
            actorId,
            completedAt,
            contextType,
            contextId,
            matchedScope,
            null,  // ContextOwnerName - will be set by caller if needed
            ChecklistScopeHelper.IsPersonalScope(matchedScope) ? context.MarshalId : null  // ContextOwnerMarshalId
        );
    }
}
