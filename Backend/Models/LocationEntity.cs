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
}
