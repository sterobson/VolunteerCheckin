using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Secondary index for looking up PersonId by email address.
/// Enables O(1) email lookups instead of O(n) table scans.
/// </summary>
public class PersonEmailIndexEntity : ITableEntity
{
    /// <summary>
    /// Partition key: "EMAIL_INDEX"
    /// </summary>
    public string PartitionKey { get; set; } = "EMAIL_INDEX";

    /// <summary>
    /// Row key: Normalized email (lowercase, trimmed)
    /// </summary>
    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// The PersonId this email belongs to
    /// </summary>
    public string PersonId { get; set; } = string.Empty;

    /// <summary>
    /// Create an index entity for a person
    /// </summary>
    public static PersonEmailIndexEntity Create(string email, string personId)
    {
        return new PersonEmailIndexEntity
        {
            RowKey = NormalizeEmail(email),
            PersonId = personId
        };
    }

    /// <summary>
    /// Normalize email for consistent lookups
    /// </summary>
    public static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
