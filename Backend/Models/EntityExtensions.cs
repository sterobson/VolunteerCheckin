using System.Text.Json;

namespace VolunteerCheckin.Functions.Models;

public static class EntityExtensions
{
    public static EventSummaryResponse ToSummaryResponse(this EventEntity entity)
    {
        return new EventSummaryResponse(
            entity.RowKey,
            entity.Name,
            entity.EventDate,
            entity.PaidMarshalTier,
            entity.IsFreeEvent
        );
    }

    public static EventResponse ToResponse(this EventEntity entity)
    {
        // Get payload (works for both v1 and v2 schemas)
        EventPayload payload = entity.GetPayload();

        return new EventResponse(
            entity.RowKey,
            entity.Name,
            entity.Description,
            entity.EventDate,
            entity.TimeZoneId,
            entity.IsActive,
            entity.CreatedDate,
            // Terminology from payload
            payload.Terminology.Person,
            payload.Terminology.Location,
            payload.Terminology.Area,
            payload.Terminology.Task,
            payload.Terminology.Course,
            // Default checkpoint styles from payload
            payload.Styling.Locations.DefaultType,
            payload.Styling.Locations.DefaultColor,
            payload.Styling.Locations.DefaultBackgroundShape,
            payload.Styling.Locations.DefaultBackgroundColor,
            payload.Styling.Locations.DefaultBorderColor,
            payload.Styling.Locations.DefaultIconColor,
            payload.Styling.Locations.DefaultSize,
            payload.Styling.Locations.DefaultMapRotation,
            // Branding from payload
            payload.Styling.Branding.HeaderGradientStart,
            payload.Styling.Branding.HeaderGradientEnd,
            payload.Styling.Branding.LogoUrl,
            payload.Styling.Branding.LogoPosition,
            payload.Styling.Branding.AccentColour,
            payload.Styling.Branding.PageGradientStart,
            payload.Styling.Branding.PageGradientEnd,
            // Route settings (legacy, now on layers - keep for backward compat)
            entity.RouteColor,
            entity.RouteStyle,
            entity.RouteWeight,
            // Sample event fields
            entity.IsSampleEvent,
            entity.ExpiresAt,
            // Payment fields
            entity.PaidMarshalTier,
            entity.IsFreeEvent
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
        // Get payload (works for both v1 and v2 schemas)
        LocationPayload payload = entity.GetPayload();

        List<string> areaIds = payload.AreaIds;

        // Resolve the style from checkpoint -> area -> event hierarchy
        CheckpointStyleResolution resolvedStyle = ResolveCheckpointStyle(
            payload, areaIds, eventEntity, areas, areaCheckpointCounts);

        // Resolve the terminology from checkpoint -> area -> event hierarchy
        string resolvedPeopleTerm = ResolveCheckpointPeopleTerm(
            payload, areaIds, eventEntity, areas, areaCheckpointCounts);
        string resolvedCheckpointTerm = ResolveCheckpointTerm(
            payload, areaIds, eventEntity, areas, areaCheckpointCounts);

        // Get location update scope configurations from payload
        List<ScopeConfiguration> locationUpdateScopeConfigs = payload.Dynamic.UpdateScopes;

        // Get layer IDs (null = all layers for mode "all")
        List<string>? layerIds = payload.LayerIds;

        // Determine layer assignment mode (migration: infer from existing data)
        string layerAssignmentMode = payload.LayerAssignmentMode;
        if (string.IsNullOrEmpty(layerAssignmentMode))
        {
            // Migration logic: infer mode from existing data
            if (layerIds == null)
            {
                // null LayerIds = was "all layers"
                layerAssignmentMode = "all";
            }
            else
            {
                // Has specific layer IDs = was "specific"
                layerAssignmentMode = "specific";
            }
        }

        // HasNearbyRoutes: true if not in auto mode, or if auto mode found layers
        bool hasNearbyRoutes = layerAssignmentMode != "auto" ||
            (layerIds?.Count ?? 0) > 0;

        return new LocationResponse(
            entity.RowKey,
            entity.EventId,
            entity.Name,
            entity.Description,
            entity.Latitude,
            entity.Longitude,
            entity.RequiredMarshals,
            entity.CheckedInCount,
            payload.What3Words,
            entity.StartTime,
            entity.EndTime,
            areaIds,
            payload.Style.Type,
            payload.Style.Color,
            payload.Style.BackgroundShape,
            payload.Style.BackgroundColor,
            payload.Style.BorderColor,
            payload.Style.IconColor,
            payload.Style.Size,
            payload.Style.MapRotation,
            resolvedStyle.Type,
            resolvedStyle.Color,
            resolvedStyle.BackgroundShape,
            resolvedStyle.BackgroundColor,
            resolvedStyle.BorderColor,
            resolvedStyle.IconColor,
            resolvedStyle.Size,
            resolvedStyle.MapRotation,
            payload.Terminology.PeopleTerm,
            payload.Terminology.CheckpointTerm,
            resolvedPeopleTerm,
            resolvedCheckpointTerm,
            entity.IsDynamic,
            locationUpdateScopeConfigs,
            payload.Dynamic.LastUpdate,
            payload.Dynamic.LastUpdatedByPersonId,
            layerAssignmentMode,
            layerIds,
            hasNearbyRoutes
        );
    }

    public static LayerResponse ToResponse(this LayerEntity entity, int checkpointCount = 0)
    {
        List<RoutePoint> route = JsonSerializer.Deserialize<List<RoutePoint>>(entity.GpxRouteJson) ?? [];

        return new LayerResponse(
            entity.RowKey,
            entity.EventId,
            entity.Name,
            entity.DisplayOrder,
            route,
            entity.RouteColor,
            entity.RouteStyle,
            entity.RouteWeight,
            checkpointCount,
            entity.CreatedDate
        );
    }

    public static AreaResponse ToResponse(this AreaEntity entity, int checkpointCount = 0)
    {
        AreaPayload payload = entity.GetPayload();

        return new AreaResponse(
            entity.RowKey,
            entity.EventId,
            entity.Name,
            entity.Description,
            entity.Color,
            payload.Contacts,
            payload.Polygon,
            entity.IsDefault,
            entity.DisplayOrder,
            checkpointCount,
            entity.CreatedDate,
            payload.Style.Type,
            payload.Style.Color,
            payload.Style.BackgroundShape,
            payload.Style.BackgroundColor,
            payload.Style.BorderColor,
            payload.Style.IconColor,
            payload.Style.Size,
            payload.Style.MapRotation,
            payload.Terminology.PeopleTerm,
            payload.Terminology.CheckpointTerm
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
            entity.LastModifiedByAdminEmail,
            entity.LinksToCheckIn
        );
    }

    public static ChecklistCompletionResponse ToResponse(this ChecklistCompletionEntity entity, LinkedCheckInInfo? linkedCheckIn = null)
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
            entity.IsDeleted,
            linkedCheckIn
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
        LocationPayload payload,
        List<AreaEntity>? sortedMatchedAreas,
        LocationStylingPayload? eventStyle)
    {
        if (IsValidIconType(payload.Style.Type)) return payload.Style.Type;

        if (sortedMatchedAreas != null)
        {
            AreaEntity? styledArea = sortedMatchedAreas.FirstOrDefault(a => IsValidIconType(a.GetPayload().Style.Type));
            if (styledArea != null) return styledArea.GetPayload().Style.Type;
        }

        if (eventStyle != null && IsValidIconType(eventStyle.DefaultType))
            return eventStyle.DefaultType;

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
        LocationPayload payload,
        List<string> areaIds,
        EventEntity? eventEntity,
        IEnumerable<AreaEntity>? areas,
        Dictionary<string, int>? areaCheckpointCounts)
    {
        List<AreaEntity>? sortedMatchedAreas = GetSortedMatchedAreas(areaIds, areas, areaCheckpointCounts);

        // Get event default styles from payload (not flat properties) for v2 support
        LocationStylingPayload? eventStyle = eventEntity?.GetPayload().Styling.Locations;

        string type = ResolveStyleType(payload, sortedMatchedAreas, eventStyle);

        // Color - use StyleColor for backward compatibility, then StyleBackgroundColor
        string color = ResolveStyleProperty(
            !string.IsNullOrEmpty(payload.Style.BackgroundColor) ? payload.Style.BackgroundColor : payload.Style.Color,
            sortedMatchedAreas,
            a => { AreaStylePayload s = a.GetPayload().Style; return !string.IsNullOrEmpty(s.BackgroundColor) ? s.BackgroundColor : s.Color; },
            !string.IsNullOrEmpty(eventStyle?.DefaultBackgroundColor) ? eventStyle.DefaultBackgroundColor : eventStyle?.DefaultColor);

        string backgroundShape = ResolveStyleProperty(payload.Style.BackgroundShape, sortedMatchedAreas,
            a => a.GetPayload().Style.BackgroundShape, eventStyle?.DefaultBackgroundShape);

        string backgroundColor = ResolveStyleProperty(payload.Style.BackgroundColor, sortedMatchedAreas,
            a => a.GetPayload().Style.BackgroundColor, eventStyle?.DefaultBackgroundColor);

        string borderColor = ResolveStyleProperty(payload.Style.BorderColor, sortedMatchedAreas,
            a => a.GetPayload().Style.BorderColor, eventStyle?.DefaultBorderColor);

        string iconColor = ResolveStyleProperty(payload.Style.IconColor, sortedMatchedAreas,
            a => a.GetPayload().Style.IconColor, eventStyle?.DefaultIconColor);

        string size = ResolveStyleProperty(payload.Style.Size, sortedMatchedAreas,
            a => a.GetPayload().Style.Size, eventStyle?.DefaultSize);

        string mapRotation = ResolveStyleProperty(payload.Style.MapRotation, sortedMatchedAreas,
            a => a.GetPayload().Style.MapRotation, eventStyle?.DefaultMapRotation);

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
        LocationPayload payload,
        List<string> areaIds,
        EventEntity? eventEntity,
        IEnumerable<AreaEntity>? areas,
        Dictionary<string, int>? areaCheckpointCounts)
    {
        // 1. If checkpoint has its own terminology, use it
        if (!string.IsNullOrEmpty(payload.Terminology.PeopleTerm))
        {
            return payload.Terminology.PeopleTerm;
        }

        // 2. Check areas - prefer area with fewest checkpoints that has terminology
        if (areas != null && areaIds.Count > 0)
        {
            IEnumerable<AreaEntity> matchedAreas = areas
                .Where(a => areaIds.Contains(a.RowKey) &&
                           !string.IsNullOrEmpty(a.GetPayload().Terminology.PeopleTerm));

            // If we have checkpoint counts, sort by count (ascending) to prefer smaller areas
            if (areaCheckpointCounts != null)
            {
                matchedAreas = matchedAreas.OrderBy(a =>
                    areaCheckpointCounts.TryGetValue(a.RowKey, out int count) ? count : int.MaxValue);
            }

            AreaEntity? termArea = matchedAreas.FirstOrDefault();
            if (termArea != null)
            {
                return termArea.GetPayload().Terminology.PeopleTerm;
            }
        }

        // 3. Use event default if set (from payload for v2 support)
        if (eventEntity != null)
        {
            string eventPeopleTerm = eventEntity.GetPayload().Terminology.Person;
            if (!string.IsNullOrEmpty(eventPeopleTerm))
            {
                return eventPeopleTerm;
            }
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
        LocationPayload payload,
        List<string> areaIds,
        EventEntity? eventEntity,
        IEnumerable<AreaEntity>? areas,
        Dictionary<string, int>? areaCheckpointCounts)
    {
        // 1. If checkpoint has its own terminology, use it
        if (!string.IsNullOrEmpty(payload.Terminology.CheckpointTerm))
        {
            return payload.Terminology.CheckpointTerm;
        }

        // 2. Check areas - prefer area with fewest checkpoints that has terminology
        if (areas != null && areaIds.Count > 0)
        {
            IEnumerable<AreaEntity> matchedAreas = areas
                .Where(a => areaIds.Contains(a.RowKey) &&
                           !string.IsNullOrEmpty(a.GetPayload().Terminology.CheckpointTerm));

            // If we have checkpoint counts, sort by count (ascending) to prefer smaller areas
            if (areaCheckpointCounts != null)
            {
                matchedAreas = matchedAreas.OrderBy(a =>
                    areaCheckpointCounts.TryGetValue(a.RowKey, out int count) ? count : int.MaxValue);
            }

            AreaEntity? termArea = matchedAreas.FirstOrDefault();
            if (termArea != null)
            {
                return termArea.GetPayload().Terminology.CheckpointTerm;
            }
        }

        // 3. Use event default if set (from payload for v2 support, singular form)
        if (eventEntity != null)
        {
            // Event stores plural form in payload.Terminology.Location
            string term = eventEntity.GetPayload().Terminology.Location;
            if (!string.IsNullOrEmpty(term))
            {
                // Convert to singular for individual checkpoint
                if (term.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                {
                    return term[..^1]; // Remove trailing 's' for singular
                }
                return term;
            }
        }

        // 4. Return default
        return "Checkpoint";
    }
}
