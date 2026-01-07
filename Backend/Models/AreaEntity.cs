using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class AreaEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // EventId
    public string RowKey { get; set; } = Guid.NewGuid().ToString(); // AreaId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#667eea"; // Hex color code for UI display
    public string ContactsJson { get; set; } = "[]"; // List<AreaContact>
    public string PolygonJson { get; set; } = "[]"; // List<RoutePoint> for map selection
    public bool IsDefault { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Default checkpoint style for checkpoints in this area (overrides event default)
    // Type: default, circle, square, triangle, diamond, star, hexagon, pentagon, water, food, medical, photo, music, start, finish, arrows, etc.
    public string CheckpointStyleType { get; set; } = "default";
    public string CheckpointStyleColor { get; set; } = string.Empty; // Hex color for colorizable shapes (legacy, use CheckpointStyleBackgroundColor)
    public string CheckpointStyleBackgroundShape { get; set; } = string.Empty; // circle, square, hexagon, pentagon, diamond, star, dot, none
    public string CheckpointStyleBackgroundColor { get; set; } = string.Empty; // Hex color for background
    public string CheckpointStyleBorderColor { get; set; } = string.Empty; // Hex color for border (empty = white, "none" = no border)
    public string CheckpointStyleIconColor { get; set; } = string.Empty; // Hex color for icon content (empty = white)
    public string CheckpointStyleSize { get; set; } = string.Empty; // 33, 66, 100, 150 (percentage)
    public string CheckpointStyleMapRotation { get; set; } = string.Empty; // Rotation in degrees (-180 to 180, empty = inherit)

    // Terminology overrides for this area (empty = inherit from event)
    public string PeopleTerm { get; set; } = string.Empty;
    public string CheckpointTerm { get; set; } = string.Empty;
}
