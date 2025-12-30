using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class ChecklistItemEntity : ITableEntity
{
    public string PartitionKey { get; set; } = Constants.EventPartitionKey;
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of scope configurations that determine visibility and completion semantics.
    /// Multiple configurations can be combined using "Most Specific Wins" logic.
    /// Example: [{"scope":"OnePerCheckpoint","itemType":"Area","ids":["area-1"]},
    ///           {"scope":"OnePerCheckpoint","itemType":"Checkpoint","ids":["checkpoint-C"]}]
    /// When multiple configs match a marshal, the most specific one determines the completion context:
    ///   1. Marshal ID (most specific)
    ///   2. Checkpoint ID
    ///   3. Area ID
    ///   4. Everyone (least specific)
    /// </summary>
    public string ScopeConfigurationsJson { get; set; } = "[]";

    /// <summary>
    /// Display order for sorting items in the UI
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Whether this item must be completed
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Optional: When this item becomes visible to marshals
    /// </summary>
    public DateTime? VisibleFrom { get; set; }

    /// <summary>
    /// Optional: When this item is no longer visible to marshals
    /// </summary>
    public DateTime? VisibleUntil { get; set; }

    /// <summary>
    /// Optional: Deadline for completing this item
    /// </summary>
    public DateTime? MustCompleteBy { get; set; }

    /// <summary>
    /// Admin email who created this item
    /// </summary>
    public string CreatedByAdminEmail { get; set; } = string.Empty;

    /// <summary>
    /// When this item was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this item was last modified
    /// </summary>
    public DateTime? LastModifiedDate { get; set; }

    /// <summary>
    /// Admin email who last modified this item
    /// </summary>
    public string LastModifiedByAdminEmail { get; set; } = string.Empty;

    /// <summary>
    /// Future extensibility: Complex filter rules in JSON format
    /// </summary>
    public string AdvancedFilterJson { get; set; } = "null";
}
