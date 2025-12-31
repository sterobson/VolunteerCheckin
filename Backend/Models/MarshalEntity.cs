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

    /// <summary>
    /// Reference to the PersonEntity for this marshal
    /// Links marshal role to their person record (for cross-event identity)
    /// </summary>
    public string PersonId { get; set; } = string.Empty;

    /// <summary>
    /// 6-digit alphanumeric magic code for passwordless marshal login
    /// Format: Uppercase A-Z, 0-9 (36^6 = 2.2 billion combinations)
    /// Regenerate to revoke access
    /// </summary>
    public string MagicCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
