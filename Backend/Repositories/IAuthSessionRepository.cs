using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IAuthSessionRepository
{
    Task<AuthSessionEntity> AddAsync(AuthSessionEntity session);
    /// <summary>
    /// Get session by token hash (O(1) lookup).
    /// The sessionTokenHash is used as the RowKey for efficient lookups.
    /// </summary>
    Task<AuthSessionEntity?> GetBySessionTokenHashAsync(string sessionTokenHash);
    Task<IEnumerable<AuthSessionEntity>> GetByPersonAsync(string personId);
    Task<IEnumerable<AuthSessionEntity>> GetByPersonAndEventAsync(string personId, string eventId);
    Task UpdateAsync(AuthSessionEntity session);
    /// <summary>
    /// Update session without optimistic concurrency check.
    /// Use for non-critical updates like LastAccessedAt where race conditions are acceptable.
    /// </summary>
    Task UpdateUnconditionalAsync(AuthSessionEntity session);
    Task DeleteAsync(string sessionTokenHash);
    Task RevokeAsync(string sessionTokenHash);
    Task RevokeAllForPersonAsync(string personId);
    Task DeleteAllByPersonAsync(string personId);
    Task DeleteAllByEventAsync(string eventId);
    Task DeleteExpiredSessionsAsync();
}
