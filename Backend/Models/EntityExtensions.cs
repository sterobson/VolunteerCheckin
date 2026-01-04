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
            entity.DefaultCheckpointStyleColor,
            entity.DefaultCheckpointStyleBackgroundShape,
            entity.DefaultCheckpointStyleBackgroundColor,
            entity.DefaultCheckpointStyleBorderColor,
            entity.DefaultCheckpointStyleIconColor,
            entity.DefaultCheckpointStyleSize
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
    /// Converts LocationEntity to LocationResponse with style and terminology resolution.
    /// </summary>
    /// <param name="entity">The location entity</param>
    /// <param name="eventEntity">The event entity for default style/terminology lookup</param>
    /// <param name="areas">All areas for the event (for style/terminology resolution)</param>
    /// <param name="areaCheckpointCounts">Optional dictionary of area ID to checkpoint count (for tie-breaking)</param>
    public static LocationResponse ToResponse(
        this LocationEntity entity,
        EventEntity? eventEntity,
        IEnumerable<AreaEntity>? areas,
        Dictionary<string, int>? areaCheckpointCounts = null)
    {
        List<string> areaIds = JsonSerializer.Deserialize<List<string>>(entity.AreaIdsJson) ?? [];

        // Resolve the style from checkpoint -> area -> event hierarchy
        CheckpointStyleResolution resolvedStyle = ResolveCheckpointStyle(
            entity, areaIds, eventEntity, areas, areaCheckpointCounts);

        // Resolve the terminology from checkpoint -> area -> event hierarchy
        string resolvedPeopleTerm = ResolveCheckpointPeopleTerm(
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
            entity.StyleBackgroundShape,
            entity.StyleBackgroundColor,
            entity.StyleBorderColor,
            entity.StyleIconColor,
            entity.StyleSize,
            resolvedStyle.Type,
            resolvedStyle.Color,
            resolvedStyle.BackgroundShape,
            resolvedStyle.BackgroundColor,
            resolvedStyle.BorderColor,
            resolvedStyle.IconColor,
            resolvedStyle.Size,
            entity.PeopleTerm,
            resolvedPeopleTerm
        );
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
            entity.CheckpointStyleColor,
            entity.CheckpointStyleBackgroundShape,
            entity.CheckpointStyleBackgroundColor,
            entity.CheckpointStyleBorderColor,
            entity.CheckpointStyleIconColor,
            entity.CheckpointStyleSize,
            entity.PeopleTerm,
            entity.CheckpointTerm
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

    /// <summary>
    /// Resolved checkpoint style containing all style properties
    /// </summary>
    private record CheckpointStyleResolution(
        string Type,
        string Color,
        string BackgroundShape,
        string BackgroundColor,
        string BorderColor,
        string IconColor,
        string Size
    );

    /// <summary>
    /// Resolves the effective checkpoint style from the hierarchy:
    /// 1. Checkpoint's own style (if not "default")
    /// 2. Area's style (if not "default") - prefers area with fewest checkpoints if multiple
    /// 3. Event's default style (if not "default")
    /// 4. Returns "default" to use status-based colored circles
    /// Each property is resolved independently through the hierarchy.
    /// </summary>
    private static CheckpointStyleResolution ResolveCheckpointStyle(
        LocationEntity location,
        List<string> areaIds,
        EventEntity? eventEntity,
        IEnumerable<AreaEntity>? areas,
        Dictionary<string, int>? areaCheckpointCounts)
    {
        // Start with defaults
        string type = "default";
        string color = string.Empty;
        string backgroundShape = string.Empty;
        string backgroundColor = string.Empty;
        string borderColor = string.Empty;
        string iconColor = string.Empty;
        string size = string.Empty;

        // Get matched areas sorted by checkpoint count for consistent resolution
        List<AreaEntity>? sortedMatchedAreas = null;
        if (areas != null && areaIds.Count > 0)
        {
            IEnumerable<AreaEntity> matchedAreas = areas.Where(a => areaIds.Contains(a.RowKey));
            if (areaCheckpointCounts != null)
            {
                matchedAreas = matchedAreas.OrderBy(a =>
                    areaCheckpointCounts.TryGetValue(a.RowKey, out int count) ? count : int.MaxValue);
            }
            sortedMatchedAreas = matchedAreas.ToList();
        }

        // Resolve Type: checkpoint -> area -> event
        if (!string.IsNullOrEmpty(location.StyleType) && location.StyleType != "default")
        {
            type = location.StyleType;
        }
        else if (sortedMatchedAreas != null)
        {
            AreaEntity? styledArea = sortedMatchedAreas.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.CheckpointStyleType) && a.CheckpointStyleType != "default");
            if (styledArea != null)
            {
                type = styledArea.CheckpointStyleType;
            }
            else if (eventEntity != null && !string.IsNullOrEmpty(eventEntity.DefaultCheckpointStyleType) &&
                     eventEntity.DefaultCheckpointStyleType != "default")
            {
                type = eventEntity.DefaultCheckpointStyleType;
            }
        }
        else if (eventEntity != null && !string.IsNullOrEmpty(eventEntity.DefaultCheckpointStyleType) &&
                 eventEntity.DefaultCheckpointStyleType != "default")
        {
            type = eventEntity.DefaultCheckpointStyleType;
        }

        // Helper to resolve a string property through hierarchy
        string ResolveProperty(
            string? checkpointValue,
            Func<AreaEntity, string?> areaGetter,
            string? eventValue)
        {
            if (!string.IsNullOrEmpty(checkpointValue)) return checkpointValue;
            if (sortedMatchedAreas != null)
            {
                AreaEntity? area = sortedMatchedAreas.FirstOrDefault(a => !string.IsNullOrEmpty(areaGetter(a)));
                if (area != null) return areaGetter(area)!;
            }
            if (!string.IsNullOrEmpty(eventValue)) return eventValue;
            return string.Empty;
        }

        // Resolve each property through the hierarchy
        // Color - use StyleColor for backward compatibility, then StyleBackgroundColor
        color = ResolveProperty(
            !string.IsNullOrEmpty(location.StyleBackgroundColor) ? location.StyleBackgroundColor : location.StyleColor,
            a => !string.IsNullOrEmpty(a.CheckpointStyleBackgroundColor) ? a.CheckpointStyleBackgroundColor : a.CheckpointStyleColor,
            !string.IsNullOrEmpty(eventEntity?.DefaultCheckpointStyleBackgroundColor) ? eventEntity.DefaultCheckpointStyleBackgroundColor : eventEntity?.DefaultCheckpointStyleColor);

        backgroundShape = ResolveProperty(
            location.StyleBackgroundShape,
            a => a.CheckpointStyleBackgroundShape,
            eventEntity?.DefaultCheckpointStyleBackgroundShape);

        backgroundColor = ResolveProperty(
            location.StyleBackgroundColor,
            a => a.CheckpointStyleBackgroundColor,
            eventEntity?.DefaultCheckpointStyleBackgroundColor);

        borderColor = ResolveProperty(
            location.StyleBorderColor,
            a => a.CheckpointStyleBorderColor,
            eventEntity?.DefaultCheckpointStyleBorderColor);

        iconColor = ResolveProperty(
            location.StyleIconColor,
            a => a.CheckpointStyleIconColor,
            eventEntity?.DefaultCheckpointStyleIconColor);

        size = ResolveProperty(
            location.StyleSize,
            a => a.CheckpointStyleSize,
            eventEntity?.DefaultCheckpointStyleSize);

        return new CheckpointStyleResolution(type, color, backgroundShape, backgroundColor, borderColor, iconColor, size);
    }

    /// <summary>
    /// Resolves the effective people terminology from the hierarchy:
    /// 1. Checkpoint's own term (if not empty)
    /// 2. Area's term (if not empty) - prefers area with fewest checkpoints if multiple
    /// 3. Event's default term (if not empty)
    /// 4. Returns "Marshals" as the default
    /// </summary>
    private static string ResolveCheckpointPeopleTerm(
        LocationEntity location,
        List<string> areaIds,
        EventEntity? eventEntity,
        IEnumerable<AreaEntity>? areas,
        Dictionary<string, int>? areaCheckpointCounts)
    {
        // 1. If checkpoint has its own terminology, use it
        if (!string.IsNullOrEmpty(location.PeopleTerm))
        {
            return location.PeopleTerm;
        }

        // 2. Check areas - prefer area with fewest checkpoints that has terminology
        if (areas != null && areaIds.Count > 0)
        {
            IEnumerable<AreaEntity> matchedAreas = areas
                .Where(a => areaIds.Contains(a.RowKey) &&
                           !string.IsNullOrEmpty(a.PeopleTerm));

            // If we have checkpoint counts, sort by count (ascending) to prefer smaller areas
            if (areaCheckpointCounts != null)
            {
                matchedAreas = matchedAreas.OrderBy(a =>
                    areaCheckpointCounts.TryGetValue(a.RowKey, out int count) ? count : int.MaxValue);
            }

            AreaEntity? termArea = matchedAreas.FirstOrDefault();
            if (termArea != null)
            {
                return termArea.PeopleTerm;
            }
        }

        // 3. Use event default if set
        if (eventEntity != null && !string.IsNullOrEmpty(eventEntity.PeopleTerm))
        {
            return eventEntity.PeopleTerm;
        }

        // 4. Return default
        return "Marshals";
    }
}
