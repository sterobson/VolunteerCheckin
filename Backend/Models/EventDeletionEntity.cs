using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Represents a pending or completed event deletion request.
/// PartitionKey = "deletion", RowKey = eventId
/// </summary>
public class EventDeletionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "deletion";
    public string RowKey { get; set; } = string.Empty; // EventId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string RequestedByEmail { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string Status { get; set; } = EventDeletionStatus.Pending; // "pending", "in_progress", "completed", "failed"
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class EventDeletionStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
