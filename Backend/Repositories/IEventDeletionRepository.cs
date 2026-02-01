using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Repository for managing event deletion requests.
/// </summary>
public interface IEventDeletionRepository
{
    /// <summary>
    /// Get a deletion request by event ID.
    /// </summary>
    Task<EventDeletionEntity?> GetByEventIdAsync(string eventId);

    /// <summary>
    /// Get all pending deletion requests.
    /// </summary>
    Task<IEnumerable<EventDeletionEntity>> GetPendingAsync();

    /// <summary>
    /// Add a new deletion request.
    /// </summary>
    Task AddAsync(EventDeletionEntity deletion);

    /// <summary>
    /// Update a deletion request.
    /// </summary>
    Task UpdateAsync(EventDeletionEntity deletion);

    /// <summary>
    /// Check if an event has a pending or in-progress deletion request.
    /// </summary>
    Task<bool> IsDeletionPendingAsync(string eventId);
}
