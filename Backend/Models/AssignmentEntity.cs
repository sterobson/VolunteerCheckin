using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class AssignmentEntity : ITableEntity
{
    // PartitionKey = EventId for efficient querying by event
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string MarshalId { get; set; } = string.Empty; // Reference to MarshalEntity
    public string MarshalName { get; set; } = string.Empty; // Denormalized for performance
    public bool IsCheckedIn { get; set; } = false;
    public DateTime? CheckInTime { get; set; }
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public string CheckInMethod { get; set; } = string.Empty; // "GPS", "Manual", "Admin"
}
