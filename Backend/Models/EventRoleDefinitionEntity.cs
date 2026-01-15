using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Represents a role definition for an event.
/// Role definitions allow admins to create custom roles with notes/descriptions
/// that can be assigned to marshals and contacts.
/// </summary>
public class EventRoleDefinitionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // EventId
    public string RowKey { get; set; } = string.Empty;       // RoleId (GUID)
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// The event this role definition belongs to
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for this role definition
    /// </summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// Display name for this role (e.g., "Medical Lead", "Safety Officer")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Free text notes/description for this role
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a default role created during event setup.
    /// Default roles can still be renamed, edited, or deleted.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Display order for sorting roles in the UI (lower = earlier)
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Whether people with this role can manage checkpoints in their assigned areas.
    /// When true, grants EventAreaLead permissions for the areas specified in the contact's visibility scope.
    /// </summary>
    public bool CanManageAreaCheckpoints { get; set; }

    /// <summary>
    /// Who created this role definition
    /// </summary>
    public string CreatedByPersonId { get; set; } = string.Empty;

    /// <summary>
    /// When this role definition was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this role definition was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; }
}
