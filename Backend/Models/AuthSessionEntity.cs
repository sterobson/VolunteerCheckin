using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Represents an authenticated session for a person.
/// Sessions can be event-specific (marshals) or cross-event (admins/leads).
/// </summary>
public class AuthSessionEntity : ITableEntity
{
    /// <summary>
    /// Partition key: "SESSION" (all sessions in one partition)
    /// Alternative: Could partition by PersonId for faster "user's sessions" queries
    /// </summary>
    public string PartitionKey { get; set; } = "SESSION";

    /// <summary>
    /// Row key: SessionTokenHash (enables O(1) lookup by token)
    /// </summary>
    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>
    /// Unique identifier for this session
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Hashed version of the session token (what goes in cookie/header)
    /// Hashed for security - if database compromised, sessions can't be hijacked
    /// </summary>
    public string SessionTokenHash { get; set; } = string.Empty;

    /// <summary>
    /// The person who owns this session
    /// </summary>
    public string PersonId { get; set; } = string.Empty;

    /// <summary>
    /// Event this session is locked to (for marshal auth)
    /// Null for admin/lead sessions (they can access multiple events)
    /// </summary>
    public string? EventId { get; set; }

    /// <summary>
    /// Marshal ID for marshal sessions (direct lookup, avoids PersonId chain issues)
    /// Null for admin/lead sessions
    /// </summary>
    public string? MarshalId { get; set; }

    /// <summary>
    /// How this session was authenticated
    /// "MarshalMagicCode" - Can only act as marshal, locked to EventId
    /// "SecureEmailLink" - Can use elevated permissions (admin/lead)
    /// </summary>
    public string AuthMethod { get; set; } = string.Empty;

    /// <summary>
    /// When this session was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this session expires
    /// - Null for MarshalMagicCode (never expires)
    /// - CreatedAt + 24h for SecureEmailLink
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Last time this session was used (updated on each request)
    /// </summary>
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this session has been revoked (sign out)
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    /// When this session was revoked
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// IP address when session was created (for audit trail)
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Check if this session is still valid
    /// </summary>
    public bool IsValid()
    {
        if (IsRevoked) return false;
        if (ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value) return false;
        return true;
    }
}
