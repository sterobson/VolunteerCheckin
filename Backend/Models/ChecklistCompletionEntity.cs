using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class ChecklistCompletionEntity : ITableEntity
{
    /// <summary>
    /// Partition key format: {EventId}
    /// This allows efficient querying of all completions for an event (critical for marshal/admin views)
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Row key format: {ChecklistItemId}_{CompletionId}
    /// This allows efficient filtering by item within the partition
    /// </summary>
    public string RowKey { get; set; } = Guid.NewGuid().ToString();

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string ChecklistItemId { get; set; } = string.Empty;

    /// <summary>
    /// Type of context this completion applies to.
    /// Values: "Personal" (for individual marshal), "Checkpoint" (for location-based), "Area" (for area-based)
    /// </summary>
    public string CompletionContextType { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the context (MarshalId for Personal, LocationId for Checkpoint, AreaId for Area)
    /// </summary>
    public string CompletionContextId { get; set; } = string.Empty;

    /// <summary>
    /// The marshal whose checklist context this completion is in (whose task list shows this)
    /// </summary>
    public string ContextOwnerMarshalId { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized name of the context owner marshal
    /// </summary>
    public string ContextOwnerMarshalName { get; set; } = string.Empty;

    /// <summary>
    /// Who actually performed this completion action.
    /// Values: "Marshal", "EventAdmin", "AreaLead"
    /// </summary>
    public string ActorType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the actor who performed the completion.
    /// - For "Marshal" or "AreaLead": MarshalId
    /// - For "EventAdmin": Admin email
    /// </summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized name of the actor for display purposes
    /// </summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>
    /// When this item was completed
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this completion has been undone (soft delete for audit trail)
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// When this completion was undone
    /// </summary>
    public DateTime? UncompletedAt { get; set; }

    /// <summary>
    /// Admin email who undid this completion
    /// </summary>
    public string UncompletedByAdminEmail { get; set; } = string.Empty;

    /// <summary>
    /// Helper to create partition key from event ID
    /// </summary>
    public static string CreatePartitionKey(string eventId)
    {
        return eventId;
    }

    /// <summary>
    /// Helper to create row key from item ID and completion ID
    /// </summary>
    public static string CreateRowKey(string itemId, string completionId)
    {
        return $"{itemId}_{completionId}";
    }
}
