using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class LocationEntity : ITableEntity
{
    // PartitionKey = EventId for efficient querying by event
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RequiredMarshals { get; set; } = 1;
    public int CheckedInCount { get; set; } = 0;
    public string What3Words { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string AreaIdsJson { get; set; } = "[]"; // JSON array of area IDs this checkpoint belongs to

    // Checkpoint style (overrides area and event defaults)
    // Type: default, circle, square, triangle, diamond, star, hexagon, pentagon, water, food, medical, photo, music, start, finish, arrows, etc.
    public string StyleType { get; set; } = "default";
    public string StyleColor { get; set; } = string.Empty; // Hex color for colorizable shapes (legacy, use StyleBackgroundColor)
    public string StyleBackgroundShape { get; set; } = string.Empty; // circle, square, hexagon, pentagon, diamond, star, dot, none
    public string StyleBackgroundColor { get; set; } = string.Empty; // Hex color for background (empty = inherit or use StyleColor)
    public string StyleBorderColor { get; set; } = string.Empty; // Hex color for border (empty = white, "none" = no border)
    public string StyleIconColor { get; set; } = string.Empty; // Hex color for icon content (empty = white)
    public string StyleSize { get; set; } = string.Empty; // 33, 66, 100, 150 (percentage, empty = 100)
    public string StyleMapRotation { get; set; } = string.Empty; // Rotation in degrees (-180 to 180, empty = inherit)

    // Terminology override for this checkpoint (empty = inherit from area -> event)
    public string PeopleTerm { get; set; } = string.Empty;
    public string CheckpointTerm { get; set; } = string.Empty; // Vehicle, Car, Bike, or empty to inherit

    // Dynamic checkpoint settings (for lead car/sweep vehicle tracking)
    public bool IsDynamic { get; set; } = false;
    public string LocationUpdateScopeJson { get; set; } = "[]"; // JSON array of ScopeConfiguration - who can update location
    public DateTime? LastLocationUpdate { get; set; }
    public string LastUpdatedByPersonId { get; set; } = string.Empty;
}
