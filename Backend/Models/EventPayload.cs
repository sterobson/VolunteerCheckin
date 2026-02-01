namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Payload structure for EventEntity v2 schema.
/// Contains properties that don't need to be queried directly in Table Storage.
/// Note: Description, EventDate, and TimeZoneId remain as direct entity fields.
/// </summary>
public class EventPayload
{
    public TerminologyPayload Terminology { get; set; } = new();
    public StylingPayload Styling { get; set; } = new();
}

/// <summary>
/// Terminology settings for customizing UI wording.
/// </summary>
public class TerminologyPayload
{
    public string Person { get; set; } = "Marshals";
    public string Location { get; set; } = "Checkpoints";
    public string Area { get; set; } = "Areas";
    public string Task { get; set; } = "Checklists";
    public string Course { get; set; } = "Course";
}

/// <summary>
/// All styling-related settings.
/// </summary>
public class StylingPayload
{
    public LocationStylingPayload Locations { get; set; } = new();
    public BrandingPayload Branding { get; set; } = new();
}

/// <summary>
/// Default checkpoint/location styling settings.
/// </summary>
public class LocationStylingPayload
{
    public string DefaultType { get; set; } = "default";
    public string DefaultColor { get; set; } = string.Empty;
    public string DefaultBackgroundShape { get; set; } = string.Empty;
    public string DefaultBackgroundColor { get; set; } = string.Empty;
    public string DefaultBorderColor { get; set; } = string.Empty;
    public string DefaultIconColor { get; set; } = string.Empty;
    public string DefaultSize { get; set; } = string.Empty;
    public string DefaultMapRotation { get; set; } = string.Empty;
}

/// <summary>
/// Marshal mode branding settings.
/// </summary>
public class BrandingPayload
{
    public string AccentColour { get; set; } = string.Empty;
    public string HeaderGradientStart { get; set; } = string.Empty;
    public string HeaderGradientEnd { get; set; } = string.Empty;
    public string PageGradientStart { get; set; } = string.Empty;
    public string PageGradientEnd { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string LogoPosition { get; set; } = string.Empty;
}
