using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Represents an event contact that can be displayed to marshals based on scope configurations.
/// Contacts can be linked to existing marshals or be external contacts (not in the system).
/// Uses the same scope system as notes/checklists for visibility control.
/// </summary>
public class EventContactEntity : ITableEntity
{
    public string PartitionKey { get; set; } = Constants.EventPartitionKey;
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// The event this contact belongs to
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for the contact
    /// </summary>
    public string ContactId { get; set; } = string.Empty;

    /// <summary>
    /// [DEPRECATED] Single role field - use RolesJson instead.
    /// Kept for backwards compatibility during migration.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of roles for this contact (e.g., ["EmergencyContact", "MedicalLead"]).
    /// Supports multiple roles including custom ones.
    /// </summary>
    public string RolesJson { get; set; } = "[]";

    /// <summary>
    /// Display name of the contact
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Phone number for the contact
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Optional email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Optional notes/description about this contact
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// If linked to a marshal, the marshal's ID. Null for external contacts.
    /// When linked, name/phone/email can sync from the marshal record.
    /// </summary>
    public string? MarshalId { get; set; }

    /// <summary>
    /// JSON array of scope configurations that determine visibility.
    /// Uses the same scope system as notes/checklists for consistency.
    /// Valid scopes for contacts:
    ///   - EveryoneInAreas: Everyone in specified areas sees the contact
    ///   - EveryoneAtCheckpoints: Everyone at specified checkpoints sees the contact
    ///   - SpecificPeople: Specific marshals see the contact
    ///   - EveryAreaLead: Area leads in specified areas see the contact
    /// Example: [{"scope":"EveryoneInAreas","itemType":"Area","ids":["ALL_AREAS"]}]
    /// </summary>
    public string ScopeConfigurationsJson { get; set; } = "[]";

    /// <summary>
    /// Display order for sorting contacts in the UI (lower = earlier)
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Whether this contact should be pinned to the top of lists
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Whether this contact should appear in the emergency info section.
    /// Defaults to true for EmergencyContact role.
    /// </summary>
    public bool ShowInEmergencyInfo { get; set; }

    /// <summary>
    /// Who created this contact
    /// </summary>
    public string CreatedByPersonId { get; set; } = string.Empty;

    /// <summary>
    /// When this contact was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this contact was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; }
}
