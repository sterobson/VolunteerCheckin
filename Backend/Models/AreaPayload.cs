namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Payload structure for AreaEntity v2 schema.
/// Contains properties that don't need to be queried directly in Table Storage.
/// </summary>
public class AreaPayload
{
    // Contacts for this area
    public List<AreaContact> Contacts { get; set; } = new();

    // Polygon boundary for map display
    public List<RoutePoint> Polygon { get; set; } = new();

    // Default checkpoint styling for checkpoints in this area
    public AreaStylePayload Style { get; set; } = new();

    // Terminology overrides for this area
    public AreaTerminologyPayload Terminology { get; set; } = new();
}

/// <summary>
/// Default checkpoint styling for checkpoints in this area (overrides event default).
/// </summary>
public class AreaStylePayload
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
/// Terminology overrides for this area.
/// </summary>
public class AreaTerminologyPayload
{
    public string PeopleTerm { get; set; } = string.Empty;
    public string CheckpointTerm { get; set; } = string.Empty;
}
