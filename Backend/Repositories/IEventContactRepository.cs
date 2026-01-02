using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Repository interface for managing event contacts
/// </summary>
public interface IEventContactRepository
{
    /// <summary>
    /// Get a contact by its ID
    /// </summary>
    Task<EventContactEntity?> GetAsync(string eventId, string contactId);

    /// <summary>
    /// Get all contacts for an event (excluding deleted)
    /// </summary>
    Task<IEnumerable<EventContactEntity>> GetByEventAsync(string eventId);

    /// <summary>
    /// Get all contacts for an event by role
    /// </summary>
    Task<IEnumerable<EventContactEntity>> GetByRoleAsync(string eventId, string role);

    /// <summary>
    /// Get all contacts linked to a specific marshal
    /// </summary>
    Task<IEnumerable<EventContactEntity>> GetByMarshalAsync(string eventId, string marshalId);

    /// <summary>
    /// Add a new contact
    /// </summary>
    Task AddAsync(EventContactEntity contact);

    /// <summary>
    /// Update an existing contact
    /// </summary>
    Task UpdateAsync(EventContactEntity contact);

    /// <summary>
    /// Delete a contact (soft delete)
    /// </summary>
    Task DeleteAsync(string eventId, string contactId);

    /// <summary>
    /// Permanently delete a contact (hard delete)
    /// </summary>
    Task HardDeleteAsync(string eventId, string contactId);
}
