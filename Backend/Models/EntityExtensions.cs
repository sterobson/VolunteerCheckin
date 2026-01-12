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
            entity.DefaultCheckpointStyleSize,
            entity.DefaultCheckpointStyleMapRotation,
            entity.BrandingHeaderGradientStart,
            entity.BrandingHeaderGradientEnd,
            entity.BrandingLogoUrl,
            entity.BrandingLogoPosition,
            entity.BrandingAccentColor,
            entity.BrandingPageGradientStart,
            entity.BrandingPageGradientEnd
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
        string resolvedCheckpointTerm = ResolveCheckpointTerm(
            entity, areaIds, eventEntity, areas, areaCheckpointCounts);

        // Parse location update scope configurations
        List<ScopeConfiguration> locationUpdateScopeConfigs =
            JsonSerializer.Deserialize<List<ScopeConfiguration>>(entity.LocationUpdateScopeJson, Helpers.FunctionHelpers.JsonOptions) ?? [];

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
            entity.StyleMapRotation,
            resolvedStyle.Type,
            resolvedStyle.Color,
            resolvedStyle.BackgroundShape,
            resolvedStyle.BackgroundColor,
            resolvedStyle.BorderColor,
            resolvedStyle.IconColor,
            resolvedStyle.Size,
            resolvedStyle.MapRotation,
            entity.PeopleTerm,
            entity.CheckpointTerm,
            resolvedPeopleTerm,
            resolvedCheckpointTerm,
            entity.IsDynamic,
            locationUpdateScopeConfigs,
            entity.LastLocationUpdate,
            entity.LastUpdatedByPersonId
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
            entity.CheckpointStyleMapRotation,
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
            entity.CheckInMethod,
            entity.CheckedInBy
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
            JsonSerializer.Deserialize<List<ScopeConfiguration>>(entity.ScopeConfigurationsJson, Helpers.FunctionHelpers.JsonOptions) ?? [];

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
    private sealed record CheckpointStyleResolution(
        string Type,
        string Color,
        string BackgroundShape,
        string BackgroundColor,
        string BorderColor,
        string IconColor,
        string Size,
        string MapRotation
    );

    /// <summary>
    /// Checks if a style type represents a valid icon type (not default or custom).
    /// </summary>
    private static bool IsValidIconType(string? styleType) =>
        !string.IsNullOrEmpty(styleType) && styleType != "default" && styleType != "custom";

    /// <summary>
    /// Gets matched areas sorted by checkpoint count for consistent style resolution.
    /// </summary>
    private static List<AreaEntity>? GetSortedMatchedAreas(
        List<string> areaIds,
        IEnumerable<AreaEntity>? areas,
        Dictionary<string, int>? areaCheckpointCounts)
    {
        if (areas == null || areaIds.Count == 0) return null;

        IEnumerable<AreaEntity> matchedAreas = areas.Where(a => areaIds.Contains(a.RowKey));
        if (areaCheckpointCounts != null)
        {
            matchedAreas = matchedAreas.OrderBy(a =>
                areaCheckpointCounts.TryGetValue(a.RowKey, out int count) ? count : int.MaxValue);
        }
        return matchedAreas.ToList();
    }

    /// <summary>
    /// Resolves a style property through the hierarchy: checkpoint -> area -> event.
    /// </summary>
    private static string ResolveStyleProperty(
        string? checkpointValue,
        List<AreaEntity>? sortedMatchedAreas,
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

    /// <summary>
    /// Resolves the style type through the hierarchy: checkpoint -> area -> event.
    /// </summary>
    private static string ResolveStyleType(
        LocationEntity location,
        List<AreaEntity>? sortedMatchedAreas,
        EventEntity? eventEntity)
    {
        if (IsValidIconType(location.StyleType)) return location.StyleType;

        if (sortedMatchedAreas != null)
        {
            AreaEntity? styledArea = sortedMatchedAreas.FirstOrDefault(a => IsValidIconType(a.CheckpointStyleType));
            if (styledArea != null) return styledArea.CheckpointStyleType;
        }

        if (eventEntity != null && IsValidIconType(eventEntity.DefaultCheckpointStyleType))
            return eventEntity.DefaultCheckpointStyleType;

        return "default";
    }

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
        List<AreaEntity>? sortedMatchedAreas = GetSortedMatchedAreas(areaIds, areas, areaCheckpointCounts);
        string type = ResolveStyleType(location, sortedMatchedAreas, eventEntity);

        // Color - use StyleColor for backward compatibility, then StyleBackgroundColor
        string color = ResolveStyleProperty(
            !string.IsNullOrEmpty(location.StyleBackgroundColor) ? location.StyleBackgroundColor : location.StyleColor,
            sortedMatchedAreas,
            a => !string.IsNullOrEmpty(a.CheckpointStyleBackgroundColor) ? a.CheckpointStyleBackgroundColor : a.CheckpointStyleColor,
            !string.IsNullOrEmpty(eventEntity?.DefaultCheckpointStyleBackgroundColor) ? eventEntity.DefaultCheckpointStyleBackgroundColor : eventEntity?.DefaultCheckpointStyleColor);

        string backgroundShape = ResolveStyleProperty(location.StyleBackgroundShape, sortedMatchedAreas,
            a => a.CheckpointStyleBackgroundShape, eventEntity?.DefaultCheckpointStyleBackgroundShape);

        string backgroundColor = ResolveStyleProperty(location.StyleBackgroundColor, sortedMatchedAreas,
            a => a.CheckpointStyleBackgroundColor, eventEntity?.DefaultCheckpointStyleBackgroundColor);

        string borderColor = ResolveStyleProperty(location.StyleBorderColor, sortedMatchedAreas,
            a => a.CheckpointStyleBorderColor, eventEntity?.DefaultCheckpointStyleBorderColor);

        string iconColor = ResolveStyleProperty(location.StyleIconColor, sortedMatchedAreas,
            a => a.CheckpointStyleIconColor, eventEntity?.DefaultCheckpointStyleIconColor);

        string size = ResolveStyleProperty(location.StyleSize, sortedMatchedAreas,
            a => a.CheckpointStyleSize, eventEntity?.DefaultCheckpointStyleSize);

        string mapRotation = ResolveStyleProperty(location.StyleMapRotation, sortedMatchedAreas,
            a => a.CheckpointStyleMapRotation, eventEntity?.DefaultCheckpointStyleMapRotation);

        return new CheckpointStyleResolution(type, color, backgroundShape, backgroundColor, borderColor, iconColor, size, mapRotation);
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

    /// <summary>
    /// Resolves the effective checkpoint terminology from the hierarchy:
    /// 1. Checkpoint's own term (if not empty)
    /// 2. Area's term (if not empty) - prefers area with fewest checkpoints if multiple
    /// 3. Event's default term (if not empty)
    /// 4. Returns "Checkpoint" as the default
    /// </summary>
    private static string ResolveCheckpointTerm(
        LocationEntity location,
        List<string> areaIds,
        EventEntity? eventEntity,
        IEnumerable<AreaEntity>? areas,
        Dictionary<string, int>? areaCheckpointCounts)
    {
        // 1. If checkpoint has its own terminology, use it
        if (!string.IsNullOrEmpty(location.CheckpointTerm))
        {
            return location.CheckpointTerm;
        }

        // 2. Check areas - prefer area with fewest checkpoints that has terminology
        if (areas != null && areaIds.Count > 0)
        {
            IEnumerable<AreaEntity> matchedAreas = areas
                .Where(a => areaIds.Contains(a.RowKey) &&
                           !string.IsNullOrEmpty(a.CheckpointTerm));

            // If we have checkpoint counts, sort by count (ascending) to prefer smaller areas
            if (areaCheckpointCounts != null)
            {
                matchedAreas = matchedAreas.OrderBy(a =>
                    areaCheckpointCounts.TryGetValue(a.RowKey, out int count) ? count : int.MaxValue);
            }

            AreaEntity? termArea = matchedAreas.FirstOrDefault();
            if (termArea != null)
            {
                return termArea.CheckpointTerm;
            }
        }

        // 3. Use event default if set (singular form)
        if (eventEntity != null && !string.IsNullOrEmpty(eventEntity.CheckpointTerm))
        {
            // Event stores plural form, convert to singular for individual checkpoint
            string term = eventEntity.CheckpointTerm;
            if (term.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                return term[..^1]; // Remove trailing 's' for singular
            }
            return term;
        }

        // 4. Return default
        return "Checkpoint";
    }
}
