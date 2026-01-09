using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class EventEntity : ITableEntity
{
    public string PartitionKey { get; set; } = Constants.EventPartitionKey;
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public string AdminEmail { get; set; } = string.Empty;
    public string EmergencyContactsJson { get; set; } = "[]";
    public string GpxRouteJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

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

    /// <summary>
    /// Apply updates from an UpdateEventRequest, only updating properties that are provided (non-null).
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
        string? brandingPageGradientEnd = null)
    {
        // Required fields
        Name = name;
        Description = description;
        EventDate = eventDateUtc;
        TimeZoneId = timeZoneId;

        // Optional updates by category
        ApplyTerminologyUpdates(peopleTerm, checkpointTerm, areaTerm, checklistTerm, courseTerm);
        ApplyDefaultStyleUpdates(defaultCheckpointStyleType, defaultCheckpointStyleColor,
            defaultCheckpointStyleBackgroundShape, defaultCheckpointStyleBackgroundColor,
            defaultCheckpointStyleBorderColor, defaultCheckpointStyleIconColor,
            defaultCheckpointStyleSize, defaultCheckpointStyleMapRotation);
        ApplyBrandingUpdates(brandingHeaderGradientStart, brandingHeaderGradientEnd,
            brandingLogoUrl, brandingLogoPosition, brandingAccentColor,
            brandingPageGradientStart, brandingPageGradientEnd);
    }

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
        if (peopleTerm != null) PeopleTerm = peopleTerm;
        if (checkpointTerm != null) CheckpointTerm = checkpointTerm;
        if (areaTerm != null) AreaTerm = areaTerm;
        if (checklistTerm != null) ChecklistTerm = checklistTerm;
        if (courseTerm != null) CourseTerm = courseTerm;
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
        if (styleType != null) DefaultCheckpointStyleType = styleType;
        if (styleColor != null) DefaultCheckpointStyleColor = styleColor;
        if (backgroundShape != null) DefaultCheckpointStyleBackgroundShape = backgroundShape;
        if (backgroundColor != null) DefaultCheckpointStyleBackgroundColor = backgroundColor;
        if (borderColor != null) DefaultCheckpointStyleBorderColor = borderColor;
        if (iconColor != null) DefaultCheckpointStyleIconColor = iconColor;
        if (size != null) DefaultCheckpointStyleSize = size;
        if (mapRotation != null) DefaultCheckpointStyleMapRotation = mapRotation;
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
        if (headerGradientStart != null) BrandingHeaderGradientStart = headerGradientStart;
        if (headerGradientEnd != null) BrandingHeaderGradientEnd = headerGradientEnd;
        if (logoUrl != null) BrandingLogoUrl = logoUrl;
        if (logoPosition != null) BrandingLogoPosition = logoPosition;
        if (accentColor != null) BrandingAccentColor = accentColor;
        if (pageGradientStart != null) BrandingPageGradientStart = pageGradientStart;
        if (pageGradientEnd != null) BrandingPageGradientEnd = pageGradientEnd;
    }
}
