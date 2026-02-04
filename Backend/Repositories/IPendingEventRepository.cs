using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Repository interface for managing pending event records (awaiting payment)
/// </summary>
public interface IPendingEventRepository
{
    /// <summary>
    /// Add a new pending event
    /// </summary>
    Task AddAsync(PendingEventEntity pendingEvent);

    /// <summary>
    /// Get a pending event by its Stripe Checkout Session ID
    /// </summary>
    Task<PendingEventEntity?> GetBySessionIdAsync(string sessionId);

    /// <summary>
    /// Update an existing pending event
    /// </summary>
    Task UpdateAsync(PendingEventEntity pendingEvent);

    /// <summary>
    /// Delete a pending event
    /// </summary>
    Task DeleteAsync(string sessionId);

    /// <summary>
    /// Get all expired pending events (for cleanup)
    /// </summary>
    Task<IEnumerable<PendingEventEntity>> GetExpiredAsync();
}
