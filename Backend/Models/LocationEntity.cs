using System.Text.Json;
using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class LocationEntity : ITableEntity
{
    public const int CurrentSchemaVersion = 2;

    // PartitionKey = EventId for efficient querying by event
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Schema version: 0 or 1 = legacy flat columns, 2 = PayloadJson
    public int SchemaVersion { get; set; } = 0;

    // V2 payload - contains style, terminology, associations, and dynamic settings
    public string PayloadJson { get; set; } = string.Empty;

    // Cached payload instance (not stored in table)
    private LocationPayload? _cachedPayload;

    // Core fields (remain as direct columns in V2)
    public string EventId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RequiredMarshals { get; set; } = 1;
    public int CheckedInCount { get; set; } = 0;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsDynamic { get; set; } = false;

    // Legacy fields (V1) - kept for reading old data, moved to PayloadJson in V2
    public string What3Words { get; set; } = string.Empty;
    public string AreaIdsJson { get; set; } = "[]"; // JSON array of area IDs this checkpoint belongs to
    public string? LayerIdsJson { get; set; } = null; // null = all layers, JSON array = specific layer IDs
    public string LayerAssignmentMode { get; set; } = "auto"; // "auto" | "all" | "specific"

    // Legacy style fields (V1) - moved to PayloadJson.Style in V2
    public string StyleType { get; set; } = "default";
    public string StyleColor { get; set; } = string.Empty;
    public string StyleBackgroundShape { get; set; } = string.Empty;
    public string StyleBackgroundColor { get; set; } = string.Empty;
    public string StyleBorderColor { get; set; } = string.Empty;
    public string StyleIconColor { get; set; } = string.Empty;
    public string StyleSize { get; set; } = string.Empty;
    public string StyleMapRotation { get; set; } = string.Empty;

    // Legacy terminology fields (V1) - moved to PayloadJson.Terminology in V2
    public string PeopleTerm { get; set; } = string.Empty;
    public string CheckpointTerm { get; set; } = string.Empty;

    // Legacy dynamic location fields (V1) - moved to PayloadJson.Dynamic in V2
    public string LocationUpdateScopeJson { get; set; } = "[]";
    public DateTime? LastLocationUpdate { get; set; }
    public string LastUpdatedByPersonId { get; set; } = string.Empty;

    /// <summary>
    /// Get the payload, deserializing from JSON if needed.
    /// For v1 entities, returns a payload built from flat properties.
    /// </summary>
    public LocationPayload GetPayload()
    {
        if (_cachedPayload != null)
        {
            return _cachedPayload;
        }

        if (SchemaVersion >= 2 && !string.IsNullOrEmpty(PayloadJson))
        {
            _cachedPayload = JsonSerializer.Deserialize<LocationPayload>(PayloadJson) ?? new LocationPayload();
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
    public void SetPayload(LocationPayload payload)
    {
        _cachedPayload = payload;
        PayloadJson = JsonSerializer.Serialize(payload);
        SchemaVersion = CurrentSchemaVersion;
    }

    /// <summary>
    /// Build a payload from v1 flat properties (for migration).
    /// </summary>
    private LocationPayload BuildPayloadFromV1Properties()
    {
        // Parse area IDs from JSON
        List<string> areaIds = new();
        if (!string.IsNullOrEmpty(AreaIdsJson))
        {
            try
            {
                areaIds = JsonSerializer.Deserialize<List<string>>(AreaIdsJson) ?? new List<string>();
            }
            catch
            {
                // Ignore parse errors, use empty list
            }
        }

        // Parse layer IDs from JSON
        List<string>? layerIds = null;
        if (!string.IsNullOrEmpty(LayerIdsJson))
        {
            try
            {
                layerIds = JsonSerializer.Deserialize<List<string>>(LayerIdsJson);
            }
            catch
            {
                // Ignore parse errors, use null
            }
        }

        // Parse location update scopes from JSON
        List<ScopeConfiguration> updateScopes = new();
        if (!string.IsNullOrEmpty(LocationUpdateScopeJson))
        {
            try
            {
                updateScopes = JsonSerializer.Deserialize<List<ScopeConfiguration>>(LocationUpdateScopeJson) ?? new List<ScopeConfiguration>();
            }
            catch
            {
                // Ignore parse errors, use empty list
            }
        }

        return new LocationPayload
        {
            What3Words = What3Words,
            AreaIds = areaIds,
            LayerIds = layerIds,
            LayerAssignmentMode = LayerAssignmentMode,
            Style = new LocationStylePayload
            {
                Type = StyleType,
                Color = StyleColor,
                BackgroundShape = StyleBackgroundShape,
                BackgroundColor = StyleBackgroundColor,
                BorderColor = StyleBorderColor,
                IconColor = StyleIconColor,
                Size = StyleSize,
                MapRotation = StyleMapRotation
            },
            Terminology = new LocationTerminologyPayload
            {
                PeopleTerm = PeopleTerm,
                CheckpointTerm = CheckpointTerm
            },
            Dynamic = new DynamicLocationPayload
            {
                UpdateScopes = updateScopes,
                LastUpdate = LastLocationUpdate,
                LastUpdatedByPersonId = LastUpdatedByPersonId
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
            LocationPayload payload = BuildPayloadFromV1Properties();
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
        LocationPayload payload = GetPayload();

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
