namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Payload structure for LocationEntity v2 schema.
/// Contains properties that don't need to be queried directly in Table Storage.
/// </summary>
public class LocationPayload
{
    public string What3Words { get; set; } = string.Empty;

    // Associations
    public List<string> AreaIds { get; set; } = new();
    public List<string>? LayerIds { get; set; }
    public string LayerAssignmentMode { get; set; } = "auto";

    // Styling
    public LocationStylePayload Style { get; set; } = new();

    // Terminology overrides
    public LocationTerminologyPayload Terminology { get; set; } = new();

    // Dynamic location settings
    public DynamicLocationPayload Dynamic { get; set; } = new();
}

/// <summary>
/// Checkpoint/location styling settings.
/// </summary>
public class LocationStylePayload
{
    public string Type { get; set; } = "default";
    public string Color { get; set; } = string.Empty;
    public string BackgroundShape { get; set; } = string.Empty;
    public string BackgroundColor { get; set; } = string.Empty;
    public string BorderColor { get; set; } = string.Empty;
    public string IconColor { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string MapRotation { get; set; } = string.Empty;
}

/// <summary>
/// Terminology overrides for this checkpoint.
/// </summary>
public class LocationTerminologyPayload
{
    public string PeopleTerm { get; set; } = string.Empty;
    public string CheckpointTerm { get; set; } = string.Empty;
}

/// <summary>
/// Dynamic location tracking settings.
/// </summary>
public class DynamicLocationPayload
{
    public List<ScopeConfiguration> UpdateScopes { get; set; } = new();
    public DateTime? LastUpdate { get; set; }
    public string LastUpdatedByPersonId { get; set; } = string.Empty;
}
