using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Represents a role assignment for a person in a specific event.
/// A person can have multiple roles across different events.
/// </summary>
public class EventRoleEntity : ITableEntity
{
    /// <summary>
    /// Partition key: PersonId (for efficient "what roles does this person have?" queries)
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Row key: {EventId}_{RoleId} (GUID for this specific role assignment)
    /// </summary>
    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// The person who has this role
    /// </summary>
    public string PersonId { get; set; } = string.Empty;

    /// <summary>
    /// The event this role applies to
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// The role type: "EventAdmin", "EventAreaAdmin", "EventAreaLead"
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of area IDs this role applies to (for area-specific roles)
    /// Empty array for event-wide roles
    /// </summary>
    public string AreaIdsJson { get; set; } = "[]";

    /// <summary>
    /// Who granted this role
    /// </summary>
    public string GrantedByPersonId { get; set; } = string.Empty;

    /// <summary>
    /// When this role was granted
    /// </summary>
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Helper to create partition key from person ID
    /// </summary>
    public static string CreatePartitionKey(string personId)
    {
        return personId;
    }

    /// <summary>
    /// Helper to create row key from event ID and role ID
    /// </summary>
    public static string CreateRowKey(string eventId, string roleId)
    {
        return $"{eventId}_{roleId}";
    }
}
