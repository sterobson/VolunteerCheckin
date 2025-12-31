using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Represents a person who can interact with the system in various roles.
/// This is the core identity - a person can be an admin, marshal, area lead, etc.
/// </summary>
public class PersonEntity : ITableEntity
{
    /// <summary>
    /// Partition key: "PERSON" (all people in one partition for now)
    /// </summary>
    public string PartitionKey { get; set; } = "PERSON";

    /// <summary>
    /// Row key: PersonId (GUID)
    /// </summary>
    public string RowKey { get; set; } = Guid.NewGuid().ToString();

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// Unique identifier for this person
    /// </summary>
    public string PersonId { get; set; } = string.Empty;

    /// <summary>
    /// Email address (unique across system)
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Person's full name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Phone number (optional)
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// System administrator flag - can access all events
    /// Requires SecureEmailLink authentication to use
    /// </summary>
    public bool IsSystemAdmin { get; set; } = false;

    /// <summary>
    /// When this person was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
