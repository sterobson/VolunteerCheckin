using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Represents a one-time use authentication token (magic link).
/// These expire quickly (15 minutes) and can only be used once.
/// </summary>
public class AuthTokenEntity : ITableEntity
{
    /// <summary>
    /// Partition key: "AUTHTOKEN" (all tokens in one partition)
    /// </summary>
    public string PartitionKey { get; set; } = "AUTHTOKEN";

    /// <summary>
    /// Row key: TokenId (GUID)
    /// </summary>
    public string RowKey { get; set; } = Guid.NewGuid().ToString();

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// Unique identifier for this token
    /// </summary>
    public string TokenId { get; set; } = string.Empty;

    /// <summary>
    /// Hashed version of the token that appears in the magic link URL
    /// We hash it so if database is compromised, tokens can't be used
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// The person this token is for
    /// </summary>
    public string PersonId { get; set; } = string.Empty;

    /// <summary>
    /// When this token was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this token expires (typically CreatedAt + 15 minutes)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When this token was used (null if not yet used)
    /// </summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// IP address that requested this token (for audit trail)
    /// </summary>
    public string RequestIpAddress { get; set; } = string.Empty;

    /// <summary>
    /// IP address that used this token (for audit trail)
    /// </summary>
    public string UseIpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Check if this token is still valid
    /// </summary>
    public bool IsValid()
    {
        return UsedAt == null && DateTime.UtcNow < ExpiresAt;
    }
}
