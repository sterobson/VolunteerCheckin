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
}
