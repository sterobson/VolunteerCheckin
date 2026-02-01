using System.Text.Json;
using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class EventEntity : ITableEntity
{
    public const int CurrentSchemaVersion = 2;

    public string PartitionKey { get; set; } = Constants.EventPartitionKey;
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Schema version: 0 or 1 = legacy flat columns, 2 = PayloadJson
    public int SchemaVersion { get; set; } = 0;

    // V2 payload - contains terminology and styling
    public string PayloadJson { get; set; } = string.Empty;

    // Cached payload instance (not stored in table)
    private EventPayload? _cachedPayload;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public string GpxRouteJson { get; set; } = "[]"; // Legacy - now on layers
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Sample event properties (queryable - not in payload)
    public bool IsSampleEvent { get; set; } = false;
    public DateTime? ExpiresAt { get; set; } = null;

    // Terminology settings - allows customizing the wording used in the UI
    // Default values match the original application terminology
    public string PeopleTerm { get; set; } = "Marshals";      // People, Marshals, Volunteers, Helpers, Staff
    public string CheckpointTerm { get; set; } = "Checkpoints"; // Checkpoints, Stations, Locations
    public string AreaTerm { get; set; } = "Areas";           // Areas, Zones
    public string ChecklistTerm { get; set; } = "Checklists"; // Checklists, Tasks
    public string CourseTerm { get; set; } = "Course";        // Course, Route, Trail

    // Default checkpoint style for this event (shapes only - no content icons at event level)
    // Type: default, circle, square, triangle, diamond, star, hexagon, pentagon, dot
    public string DefaultCheckpointStyleType { get; set; } = "default";
    public string DefaultCheckpointStyleColor { get; set; } = string.Empty; // Hex color for colorizable shapes (legacy, use DefaultCheckpointStyleBackgroundColor)
    public string DefaultCheckpointStyleBackgroundShape { get; set; } = string.Empty; // circle, square, hexagon, pentagon, diamond, star, dot, none
    public string DefaultCheckpointStyleBackgroundColor { get; set; } = string.Empty; // Hex color for background
    public string DefaultCheckpointStyleBorderColor { get; set; } = string.Empty; // Hex color for border (empty = white, "none" = no border)
    public string DefaultCheckpointStyleIconColor { get; set; } = string.Empty; // Hex color for icon content (empty = white)
    public string DefaultCheckpointStyleSize { get; set; } = string.Empty; // 33, 66, 100, 150 (percentage)
    public string DefaultCheckpointStyleMapRotation { get; set; } = string.Empty; // Rotation in degrees (-180 to 180)

    // Marshal mode branding - header gradient
    public string BrandingHeaderGradientStart { get; set; } = string.Empty; // Hex color, default #667eea
    public string BrandingHeaderGradientEnd { get; set; } = string.Empty;   // Hex color, default #764ba2

    // Marshal mode branding - logo
    public string BrandingLogoUrl { get; set; } = string.Empty; // URL to uploaded logo image
    public string BrandingLogoPosition { get; set; } = string.Empty; // "left", "right", or "cover" (empty = left)

    // Marshal mode branding - accent color for primary buttons (GPS Check-In, etc.)
    public string BrandingAccentColor { get; set; } = string.Empty; // Hex color, default #667eea

    // Marshal mode branding - page background gradient
    public string BrandingPageGradientStart { get; set; } = string.Empty; // Hex color, default #667eea
    public string BrandingPageGradientEnd { get; set; } = string.Empty;   // Hex color, default #764ba2

    // Route display settings
    public string RouteColor { get; set; } = string.Empty; // Hex color, default #3388ff (Leaflet blue)
    public string RouteStyle { get; set; } = string.Empty; // line, dash, dash-long, dash-short, dot, dot-sparse, dash-dot, dash-dot-dot, etc.
    public int? RouteWeight { get; set; } // Line thickness in pixels (default: 4)

    /// <summary>
    /// Apply updates from an UpdateEventRequest, only updating properties that are provided (non-null).
    /// Updates are applied to the v2 payload.
    /// </summary>
    public void ApplyUpdates(
        string name,
        string description,
        DateTime eventDateUtc,
        string timeZoneId,
        string? peopleTerm = null,
        string? checkpointTerm = null,
        string? areaTerm = null,
        string? checklistTerm = null,
        string? courseTerm = null,
        string? defaultCheckpointStyleType = null,
        string? defaultCheckpointStyleColor = null,
        string? defaultCheckpointStyleBackgroundShape = null,
        string? defaultCheckpointStyleBackgroundColor = null,
        string? defaultCheckpointStyleBorderColor = null,
        string? defaultCheckpointStyleIconColor = null,
        string? defaultCheckpointStyleSize = null,
        string? defaultCheckpointStyleMapRotation = null,
        string? brandingHeaderGradientStart = null,
        string? brandingHeaderGradientEnd = null,
        string? brandingLogoUrl = null,
        string? brandingLogoPosition = null,
        string? brandingAccentColor = null,
        string? brandingPageGradientStart = null,
        string? brandingPageGradientEnd = null,
        string? routeColor = null,
        string? routeStyle = null,
        int? routeWeight = null)
    {
        // Required fields (stored directly on entity)
        Name = name;
        Description = description;
        EventDate = eventDateUtc;
        TimeZoneId = timeZoneId;

        // Get current payload (works for both v1 and v2)
        EventPayload payload = GetPayload();

        // Apply terminology updates
        if (peopleTerm != null) payload.Terminology.Person = peopleTerm;
        if (checkpointTerm != null) payload.Terminology.Location = checkpointTerm;
        if (areaTerm != null) payload.Terminology.Area = areaTerm;
        if (checklistTerm != null) payload.Terminology.Task = checklistTerm;
        if (courseTerm != null) payload.Terminology.Course = courseTerm;

        // Apply default style updates
        if (defaultCheckpointStyleType != null) payload.Styling.Locations.DefaultType = defaultCheckpointStyleType;
        if (defaultCheckpointStyleColor != null) payload.Styling.Locations.DefaultColor = defaultCheckpointStyleColor;
        if (defaultCheckpointStyleBackgroundShape != null) payload.Styling.Locations.DefaultBackgroundShape = defaultCheckpointStyleBackgroundShape;
        if (defaultCheckpointStyleBackgroundColor != null) payload.Styling.Locations.DefaultBackgroundColor = defaultCheckpointStyleBackgroundColor;
        if (defaultCheckpointStyleBorderColor != null) payload.Styling.Locations.DefaultBorderColor = defaultCheckpointStyleBorderColor;
        if (defaultCheckpointStyleIconColor != null) payload.Styling.Locations.DefaultIconColor = defaultCheckpointStyleIconColor;
        if (defaultCheckpointStyleSize != null) payload.Styling.Locations.DefaultSize = defaultCheckpointStyleSize;
        if (defaultCheckpointStyleMapRotation != null) payload.Styling.Locations.DefaultMapRotation = defaultCheckpointStyleMapRotation;

        // Apply branding updates
        if (brandingHeaderGradientStart != null) payload.Styling.Branding.HeaderGradientStart = brandingHeaderGradientStart;
        if (brandingHeaderGradientEnd != null) payload.Styling.Branding.HeaderGradientEnd = brandingHeaderGradientEnd;
        if (brandingLogoUrl != null) payload.Styling.Branding.LogoUrl = brandingLogoUrl;
        if (brandingLogoPosition != null) payload.Styling.Branding.LogoPosition = brandingLogoPosition;
        if (brandingAccentColor != null) payload.Styling.Branding.AccentColour = brandingAccentColor;
        if (brandingPageGradientStart != null) payload.Styling.Branding.PageGradientStart = brandingPageGradientStart;
        if (brandingPageGradientEnd != null) payload.Styling.Branding.PageGradientEnd = brandingPageGradientEnd;

        // Route settings are now on layers, but keep for backward compat
        if (routeColor != null) RouteColor = routeColor;
        if (routeStyle != null) RouteStyle = routeStyle;
        if (routeWeight != null) RouteWeight = routeWeight;

        // Save the updated payload (this also sets SchemaVersion = 2)
        SetPayload(payload);
    }

    // Legacy methods kept for any code that might still use them directly
    // These now delegate to the payload

    /// <summary>
    /// Apply terminology updates (only non-null values).
    /// </summary>
    public void ApplyTerminologyUpdates(
        string? peopleTerm = null,
        string? checkpointTerm = null,
        string? areaTerm = null,
        string? checklistTerm = null,
        string? courseTerm = null)
    {
        EventPayload payload = GetPayload();
        if (peopleTerm != null) payload.Terminology.Person = peopleTerm;
        if (checkpointTerm != null) payload.Terminology.Location = checkpointTerm;
        if (areaTerm != null) payload.Terminology.Area = areaTerm;
        if (checklistTerm != null) payload.Terminology.Task = checklistTerm;
        if (courseTerm != null) payload.Terminology.Course = courseTerm;
        SetPayload(payload);
    }

    /// <summary>
    /// Apply default checkpoint style updates (only non-null values).
    /// </summary>
    public void ApplyDefaultStyleUpdates(
        string? styleType = null,
        string? styleColor = null,
        string? backgroundShape = null,
        string? backgroundColor = null,
        string? borderColor = null,
        string? iconColor = null,
        string? size = null,
        string? mapRotation = null)
    {
        EventPayload payload = GetPayload();
        if (styleType != null) payload.Styling.Locations.DefaultType = styleType;
        if (styleColor != null) payload.Styling.Locations.DefaultColor = styleColor;
        if (backgroundShape != null) payload.Styling.Locations.DefaultBackgroundShape = backgroundShape;
        if (backgroundColor != null) payload.Styling.Locations.DefaultBackgroundColor = backgroundColor;
        if (borderColor != null) payload.Styling.Locations.DefaultBorderColor = borderColor;
        if (iconColor != null) payload.Styling.Locations.DefaultIconColor = iconColor;
        if (size != null) payload.Styling.Locations.DefaultSize = size;
        if (mapRotation != null) payload.Styling.Locations.DefaultMapRotation = mapRotation;
        SetPayload(payload);
    }

    /// <summary>
    /// Apply branding updates (only non-null values).
    /// </summary>
    public void ApplyBrandingUpdates(
        string? headerGradientStart = null,
        string? headerGradientEnd = null,
        string? logoUrl = null,
        string? logoPosition = null,
        string? accentColor = null,
        string? pageGradientStart = null,
        string? pageGradientEnd = null)
    {
        EventPayload payload = GetPayload();
        if (headerGradientStart != null) payload.Styling.Branding.HeaderGradientStart = headerGradientStart;
        if (headerGradientEnd != null) payload.Styling.Branding.HeaderGradientEnd = headerGradientEnd;
        if (logoUrl != null) payload.Styling.Branding.LogoUrl = logoUrl;
        if (logoPosition != null) payload.Styling.Branding.LogoPosition = logoPosition;
        if (accentColor != null) payload.Styling.Branding.AccentColour = accentColor;
        if (pageGradientStart != null) payload.Styling.Branding.PageGradientStart = pageGradientStart;
        if (pageGradientEnd != null) payload.Styling.Branding.PageGradientEnd = pageGradientEnd;
        SetPayload(payload);
    }

    /// <summary>
    /// Apply route display updates (only non-null values).
    /// Note: Route settings are now stored on layers, but kept here for backward compatibility.
    /// </summary>
    public void ApplyRouteUpdates(
        string? routeColor = null,
        string? routeStyle = null,
        int? routeWeight = null)
    {
        if (routeColor != null) RouteColor = routeColor;
        if (routeStyle != null) RouteStyle = routeStyle;
        if (routeWeight != null) RouteWeight = routeWeight;
    }

    /// <summary>
    /// Get the payload, deserializing from JSON if needed.
    /// For v1 entities, returns a payload built from flat properties.
    /// </summary>
    public EventPayload GetPayload()
    {
        if (_cachedPayload != null)
        {
            return _cachedPayload;
        }

        if (SchemaVersion >= 2 && !string.IsNullOrEmpty(PayloadJson))
        {
            _cachedPayload = JsonSerializer.Deserialize<EventPayload>(PayloadJson) ?? new EventPayload();
        }
        else
        {
            // Build payload from v1 flat properties
            _cachedPayload = BuildPayloadFromV1Properties();
        }

        return _cachedPayload;
    }

    /// <summary>
    /// Set the payload and serialize to JSON.
    /// </summary>
    public void SetPayload(EventPayload payload)
    {
        _cachedPayload = payload;
        PayloadJson = JsonSerializer.Serialize(payload);
        SchemaVersion = CurrentSchemaVersion;
    }

    /// <summary>
    /// Build a payload from v1 flat properties (for migration).
    /// </summary>
    private EventPayload BuildPayloadFromV1Properties()
    {
        return new EventPayload
        {
            Terminology = new TerminologyPayload
            {
                Person = PeopleTerm,
                Location = CheckpointTerm,
                Area = AreaTerm,
                Task = ChecklistTerm,
                Course = CourseTerm
            },
            Styling = new StylingPayload
            {
                Locations = new LocationStylingPayload
                {
                    DefaultType = DefaultCheckpointStyleType,
                    DefaultColor = DefaultCheckpointStyleColor,
                    DefaultBackgroundShape = DefaultCheckpointStyleBackgroundShape,
                    DefaultBackgroundColor = DefaultCheckpointStyleBackgroundColor,
                    DefaultBorderColor = DefaultCheckpointStyleBorderColor,
                    DefaultIconColor = DefaultCheckpointStyleIconColor,
                    DefaultSize = DefaultCheckpointStyleSize,
                    DefaultMapRotation = DefaultCheckpointStyleMapRotation
                },
                Branding = new BrandingPayload
                {
                    AccentColour = BrandingAccentColor,
                    HeaderGradientStart = BrandingHeaderGradientStart,
                    HeaderGradientEnd = BrandingHeaderGradientEnd,
                    PageGradientStart = BrandingPageGradientStart,
                    PageGradientEnd = BrandingPageGradientEnd,
                    LogoUrl = BrandingLogoUrl,
                    LogoPosition = BrandingLogoPosition
                }
            }
        };
    }

    /// <summary>
    /// Upgrade the entity through each schema version to reach the current version.
    /// </summary>
    public void UpgradeSchema()
    {
        // Upgrade v0/v1 -> v2: Move flat properties to PayloadJson
        if (SchemaVersion < 2)
        {
            EventPayload payload = BuildPayloadFromV1Properties();
            SetPayload(payload);
        }

        // Note: GpxRouteJson is cleared by the layer migration in LayerFunctions
        // when the route is migrated to a layer entity.

        // Note: Legacy flat fields (terminology, styling, branding) are not explicitly cleared
        // because the repository saves as EventEntityV2 which doesn't include those properties.
    }
}
