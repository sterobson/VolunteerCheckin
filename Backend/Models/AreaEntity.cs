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

    /// <summary>
    /// JSON array of marshal IDs who are area leads for this area.
    /// Area leads have special permissions for checklist items in their area.
    /// </summary>
    public string AreaLeadMarshalIdsJson { get; set; } = "[]";
}
