using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class MarshalEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // EventId
    public string RowKey { get; set; } = string.Empty; // MarshalId (GUID)
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string MarshalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
