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
}
