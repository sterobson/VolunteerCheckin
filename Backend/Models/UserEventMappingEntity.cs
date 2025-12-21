using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class UserEventMappingEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // EventId
    public string RowKey { get; set; } = string.Empty; // UserEmail
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
