using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Represents a pending event awaiting payment confirmation.
/// PartitionKey = "PENDING", RowKey = Stripe Checkout Session ID.
/// Cleaned up after 24 hours if payment is not completed.
/// </summary>
public class PendingEventEntity : ITableEntity
{
    public const string PendingPartitionKey = "PENDING";

    public string PartitionKey { get; set; } = PendingPartitionKey;
    public string RowKey { get; set; } = string.Empty; // Stripe Checkout Session ID
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string PersonId { get; set; } = string.Empty;
    public string EventDataJson { get; set; } = string.Empty; // Serialized CreateEventRequest
    public int MarshalTier { get; set; }
    public int AmountPence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
    public string Status { get; set; } = "Pending"; // "Pending", "Completed", "Expired"

    // For upgrade payments, store the event ID being upgraded
    public string? UpgradeEventId { get; set; }
}
