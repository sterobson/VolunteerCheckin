using System.Text.Json;

namespace VolunteerCheckin.Functions.Models;

public static class EntityExtensions
{
    public static EventResponse ToResponse(this EventEntity entity)
    {
        List<EmergencyContact> emergencyContacts = JsonSerializer.Deserialize<List<EmergencyContact>>(entity.EmergencyContactsJson) ?? [];
        List<RoutePoint> route = JsonSerializer.Deserialize<List<RoutePoint>>(entity.GpxRouteJson) ?? [];

        return new EventResponse(
            entity.RowKey,
            entity.Name,
            entity.Description,
            entity.EventDate,
            entity.TimeZoneId,
            entity.AdminEmail,
            emergencyContacts,
            route,
            entity.IsActive,
            entity.CreatedDate,
            entity.PeopleTerm,
            entity.CheckpointTerm,
            entity.AreaTerm,
            entity.ChecklistTerm,
            entity.CourseTerm,
            entity.DefaultCheckpointStyleType,
            entity.DefaultCheckpointStyleColor
        );
    }

    /// <summary>
    /// Converts LocationEntity to LocationResponse without style resolution.
    /// Use the overload with eventEntity and areas for proper style resolution.
    /// </summary>
    public static LocationResponse ToResponse(this LocationEntity entity)
    {
        return entity.ToResponse(null, null, null);
    }

    /// <summary>
    /// Converts LocationEntity to LocationResponse with style resolution.
    /// </summary>
    /// <param name="entity">The location entity</param>
    /// <param name="eventEntity">The event entity for default style lookup</param>
    /// <param name="areas">All areas for the event (for style resolution)</param>
    /// <param name="areaCheckpointCounts">Optional dictionary of area ID to checkpoint count (for tie-breaking)</param>
    public static LocationResponse ToResponse(
        this LocationEntity entity,
        EventEntity? eventEntity,
        IEnumerable<AreaEntity>? areas,
        Dictionary<string, int>? areaCheckpointCounts = null)
    {
        List<string> areaIds = JsonSerializer.Deserialize<List<string>>(entity.AreaIdsJson) ?? [];

        // Resolve the style from checkpoint -> area -> event hierarchy
        (string resolvedType, string resolvedColor) = ResolveCheckpointStyle(
            entity, areaIds, eventEntity, areas, areaCheckpointCounts);

        return new LocationResponse(
            entity.RowKey,
            entity.EventId,
            entity.Name,
            entity.Description,
            entity.Latitude,
            entity.Longitude,
            entity.RequiredMarshals,
            entity.CheckedInCount,
            entity.What3Words,
            entity.StartTime,
            entity.EndTime,
            areaIds,
            entity.StyleType,
            entity.StyleColor,
            resolvedType,
            resolvedColor
        );
    }

    /// <summary>
    /// Resolves the effective checkpoint style from the hierarchy:
    /// 1. Checkpoint's own style (if not "default")
    /// 2. Area's style (if not "default") - prefers area with fewest checkpoints if multiple
    /// 3. Event's default style (if not "default")
    /// 4. Returns "default" to use status-based colored circles
    /// </summary>
    private static (string Type, string Color) ResolveCheckpointStyle(
        LocationEntity location,
        List<string> areaIds,
        EventEntity? eventEntity,
        IEnumerable<AreaEntity>? areas,
        Dictionary<string, int>? areaCheckpointCounts)
    {
        // 1. If checkpoint has its own style (not "default"), use it
        if (!string.IsNullOrEmpty(location.StyleType) && location.StyleType != "default")
        {
            return (location.StyleType, location.StyleColor ?? string.Empty);
        }

        // 2. Check areas - prefer area with fewest checkpoints that has a style
        if (areas != null && areaIds.Count > 0)
        {
            IEnumerable<AreaEntity> matchedAreas = areas
                .Where(a => areaIds.Contains(a.RowKey) &&
                           !string.IsNullOrEmpty(a.CheckpointStyleType) &&
                           a.CheckpointStyleType != "default");

            // If we have checkpoint counts, sort by count (ascending) to prefer smaller areas
            if (areaCheckpointCounts != null)
            {
                matchedAreas = matchedAreas.OrderBy(a =>
                    areaCheckpointCounts.TryGetValue(a.RowKey, out int count) ? count : int.MaxValue);
            }

            AreaEntity? styledArea = matchedAreas.FirstOrDefault();
            if (styledArea != null)
            {
                return (styledArea.CheckpointStyleType, styledArea.CheckpointStyleColor ?? string.Empty);
            }
        }

        // 3. Use event default if set
        if (eventEntity != null &&
            !string.IsNullOrEmpty(eventEntity.DefaultCheckpointStyleType) &&
            eventEntity.DefaultCheckpointStyleType != "default")
        {
            return (eventEntity.DefaultCheckpointStyleType, eventEntity.DefaultCheckpointStyleColor ?? string.Empty);
        }

        // 4. Return default (status-based rendering)
        return ("default", string.Empty);
    }

    public static AreaResponse ToResponse(this AreaEntity entity, int checkpointCount = 0)
    {
        List<AreaContact> contacts = JsonSerializer.Deserialize<List<AreaContact>>(entity.ContactsJson) ?? [];
        List<RoutePoint> polygon = JsonSerializer.Deserialize<List<RoutePoint>>(entity.PolygonJson) ?? [];

        return new AreaResponse(
            entity.RowKey,
            entity.EventId,
            entity.Name,
            entity.Description,
            entity.Color,
            contacts,
            polygon,
            entity.IsDefault,
            entity.DisplayOrder,
            checkpointCount,
            entity.CreatedDate,
            entity.CheckpointStyleType,
            entity.CheckpointStyleColor
        );
    }

    public static AssignmentResponse ToResponse(this AssignmentEntity entity)
    {
        return new AssignmentResponse(
            entity.RowKey,
            entity.EventId,
            entity.LocationId,
            entity.MarshalId,
            entity.MarshalName,
            entity.IsCheckedIn,
            entity.CheckInTime,
            entity.CheckInLatitude,
            entity.CheckInLongitude,
            entity.CheckInMethod
        );
    }

    public static UserEventMappingResponse ToResponse(this UserEventMappingEntity entity)
    {
        return new UserEventMappingResponse(
            entity.EventId,
            entity.UserEmail,
            entity.Role,
            entity.CreatedDate
        );
    }

    public static ChecklistItemResponse ToResponse(this ChecklistItemEntity entity)
    {
        List<ScopeConfiguration> scopeConfigurations =
            JsonSerializer.Deserialize<List<ScopeConfiguration>>(entity.ScopeConfigurationsJson) ?? [];

        return new ChecklistItemResponse(
            entity.ItemId,
            entity.EventId,
            entity.Text,
            scopeConfigurations,
            entity.DisplayOrder,
            entity.IsRequired,
            entity.VisibleFrom,
            entity.VisibleUntil,
            entity.MustCompleteBy,
            entity.CreatedByAdminEmail,
            entity.CreatedDate,
            entity.LastModifiedDate,
            entity.LastModifiedByAdminEmail
        );
    }

    public static ChecklistCompletionResponse ToResponse(this ChecklistCompletionEntity entity)
    {
        return new ChecklistCompletionResponse(
            entity.RowKey,
            entity.EventId,
            entity.ChecklistItemId,
            entity.CompletionContextType,
            entity.CompletionContextId,
            entity.ContextOwnerMarshalId,
            entity.ContextOwnerMarshalName,
            entity.ActorType,
            entity.ActorId,
            entity.ActorName,
            entity.CompletedAt,
            entity.IsDeleted
        );
    }
}
