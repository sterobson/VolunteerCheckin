using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Represents a note that can be displayed to marshals based on scope configurations.
/// Notes are read-only for marshals; only admins and area leads can create/edit them.
/// Unlike checklists, notes don't track completion - they're purely informational.
/// </summary>
public class NoteEntity : ITableEntity
{
    public string PartitionKey { get; set; } = Constants.EventPartitionKey;
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// The event this note belongs to
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for the note
    /// </summary>
    public string NoteId { get; set; } = string.Empty;

    /// <summary>
    /// The title of the note (displayed prominently)
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The content/body of the note (supports markdown)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of scope configurations that determine visibility.
    /// Uses the same scope system as checklists for consistency.
    /// Note: Notes don't support "One per Area/Checkpoint" scopes since there's no completion.
    /// Valid scopes for notes:
    ///   - EveryoneInAreas: Everyone in specified areas sees the note
    ///   - EveryoneAtCheckpoints: Everyone at specified checkpoints sees the note
    ///   - SpecificPeople: Specific marshals see the note
    ///   - EveryAreaLead: Area leads in specified areas see the note
    /// Example: [{"scope":"EveryoneInAreas","itemType":"Area","ids":["area-1","area-2"]}]
    /// </summary>
    public string ScopeConfigurationsJson { get; set; } = "[]";

    /// <summary>
    /// Display order for sorting notes in the UI (lower = earlier)
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Visual priority/importance level
    /// </summary>
    public string Priority { get; set; } = Constants.NotePriorityNormal;

    /// <summary>
    /// Optional category for grouping notes
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Whether this note is pinned (displayed at top)
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Who created this note
    /// </summary>
    public string CreatedByPersonId { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;

    /// <summary>
    /// When this note was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Who last updated this note
    /// </summary>
    public string? UpdatedByPersonId { get; set; }
    public string? UpdatedByName { get; set; }

    /// <summary>
    /// When this note was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; }
}
