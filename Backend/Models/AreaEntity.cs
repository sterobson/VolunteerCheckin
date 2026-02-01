using System.Text.Json;
using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class AreaEntity : ITableEntity
{
    public const int CurrentSchemaVersion = 2;

    public string PartitionKey { get; set; } = string.Empty; // EventId
    public string RowKey { get; set; } = Guid.NewGuid().ToString(); // AreaId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Schema version: 0 or 1 = legacy flat columns, 2 = PayloadJson
    public int SchemaVersion { get; set; } = 0;

    // V2 payload - contains style, terminology, contacts, and polygon
    public string PayloadJson { get; set; } = string.Empty;

    // Cached payload instance (not stored in table)
    private AreaPayload? _cachedPayload;

    // Core fields (remain as direct columns in V2)
    public string EventId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#667eea"; // Hex color code for UI display
    public bool IsDefault { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Legacy fields (V1) - kept for reading old data, moved to PayloadJson in V2
    public string ContactsJson { get; set; } = "[]"; // List<AreaContact>
    public string PolygonJson { get; set; } = "[]"; // List<RoutePoint> for map selection

    // Legacy style fields (V1) - moved to PayloadJson.Style in V2
    // Type: default, circle, square, triangle, diamond, star, hexagon, pentagon, water, food, medical, photo, music, start, finish, arrows, etc.
    public string CheckpointStyleType { get; set; } = "default";
    public string CheckpointStyleColor { get; set; } = string.Empty; // Hex color for colorizable shapes (legacy, use CheckpointStyleBackgroundColor)
    public string CheckpointStyleBackgroundShape { get; set; } = string.Empty; // circle, square, hexagon, pentagon, diamond, star, dot, none
    public string CheckpointStyleBackgroundColor { get; set; } = string.Empty; // Hex color for background
    public string CheckpointStyleBorderColor { get; set; } = string.Empty; // Hex color for border (empty = white, "none" = no border)
    public string CheckpointStyleIconColor { get; set; } = string.Empty; // Hex color for icon content (empty = white)
    public string CheckpointStyleSize { get; set; } = string.Empty; // 33, 66, 100, 150 (percentage)
    public string CheckpointStyleMapRotation { get; set; } = string.Empty; // Rotation in degrees (-180 to 180, empty = inherit)

    // Legacy terminology fields (V1) - moved to PayloadJson.Terminology in V2
    public string PeopleTerm { get; set; } = string.Empty;
    public string CheckpointTerm { get; set; } = string.Empty;

    /// <summary>
    /// Get the payload, deserializing from JSON if needed.
    /// For v1 entities, returns a payload built from flat properties.
    /// </summary>
    public AreaPayload GetPayload()
    {
        if (_cachedPayload != null)
        {
            return _cachedPayload;
        }

        if (SchemaVersion >= 2 && !string.IsNullOrEmpty(PayloadJson))
        {
            _cachedPayload = JsonSerializer.Deserialize<AreaPayload>(PayloadJson) ?? new AreaPayload();
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
    public void SetPayload(AreaPayload payload)
    {
        _cachedPayload = payload;
        PayloadJson = JsonSerializer.Serialize(payload);
        SchemaVersion = CurrentSchemaVersion;
    }

    /// <summary>
    /// Build a payload from v1 flat properties (for migration).
    /// </summary>
    private AreaPayload BuildPayloadFromV1Properties()
    {
        // Parse contacts from JSON
        List<AreaContact> contacts = new();
        if (!string.IsNullOrEmpty(ContactsJson))
        {
            try
            {
                contacts = JsonSerializer.Deserialize<List<AreaContact>>(ContactsJson) ?? new List<AreaContact>();
            }
            catch
            {
                // Ignore parse errors, use empty list
            }
        }

        // Parse polygon from JSON
        List<RoutePoint> polygon = new();
        if (!string.IsNullOrEmpty(PolygonJson))
        {
            try
            {
                polygon = JsonSerializer.Deserialize<List<RoutePoint>>(PolygonJson) ?? new List<RoutePoint>();
            }
            catch
            {
                // Ignore parse errors, use empty list
            }
        }

        return new AreaPayload
        {
            Contacts = contacts,
            Polygon = polygon,
            Style = new AreaStylePayload
            {
                Type = CheckpointStyleType,
                Color = CheckpointStyleColor,
                BackgroundShape = CheckpointStyleBackgroundShape,
                BackgroundColor = CheckpointStyleBackgroundColor,
                BorderColor = CheckpointStyleBorderColor,
                IconColor = CheckpointStyleIconColor,
                Size = CheckpointStyleSize,
                MapRotation = CheckpointStyleMapRotation
            },
            Terminology = new AreaTerminologyPayload
            {
                PeopleTerm = PeopleTerm,
                CheckpointTerm = CheckpointTerm
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
            AreaPayload payload = BuildPayloadFromV1Properties();
            SetPayload(payload);
        }
    }

    /// <summary>
    /// Apply style and terminology updates, only updating properties that are provided (non-null).
    /// Updates are applied to the v2 payload.
    /// </summary>
    public void ApplyStyleUpdates(
        string? styleType = null,
        string? styleColor = null,
        string? styleBackgroundShape = null,
        string? styleBackgroundColor = null,
        string? styleBorderColor = null,
        string? styleIconColor = null,
        string? styleSize = null,
        string? styleMapRotation = null,
        string? peopleTerm = null,
        string? checkpointTerm = null)
    {
        AreaPayload payload = GetPayload();

        if (styleType != null) payload.Style.Type = styleType;
        if (styleColor != null) payload.Style.Color = styleColor;
        if (styleBackgroundShape != null) payload.Style.BackgroundShape = styleBackgroundShape;
        if (styleBackgroundColor != null) payload.Style.BackgroundColor = styleBackgroundColor;
        if (styleBorderColor != null) payload.Style.BorderColor = styleBorderColor;
        if (styleIconColor != null) payload.Style.IconColor = styleIconColor;
        if (styleSize != null) payload.Style.Size = styleSize;
        if (styleMapRotation != null) payload.Style.MapRotation = styleMapRotation;
        if (peopleTerm != null) payload.Terminology.PeopleTerm = peopleTerm;
        if (checkpointTerm != null) payload.Terminology.CheckpointTerm = checkpointTerm;

        SetPayload(payload);
    }
}
