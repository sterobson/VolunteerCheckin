using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Repository interface for managing event role definitions
/// </summary>
public interface IEventRoleDefinitionRepository
{
    /// <summary>
    /// Get a role definition by its ID
    /// </summary>
    Task<EventRoleDefinitionEntity?> GetAsync(string eventId, string roleId);

    /// <summary>
    /// Get all role definitions for an event (excluding deleted)
    /// </summary>
    Task<IEnumerable<EventRoleDefinitionEntity>> GetByEventAsync(string eventId);

    /// <summary>
    /// Get a role definition by name (for uniqueness check)
    /// </summary>
    Task<EventRoleDefinitionEntity?> GetByNameAsync(string eventId, string name);

    /// <summary>
    /// Add a new role definition
    /// </summary>
    Task AddAsync(EventRoleDefinitionEntity roleDefinition);

    /// <summary>
    /// Update an existing role definition
    /// </summary>
    Task UpdateAsync(EventRoleDefinitionEntity roleDefinition);

    /// <summary>
    /// Delete a role definition (soft delete)
    /// </summary>
    Task DeleteAsync(string eventId, string roleId);

    /// <summary>
    /// Update display order for multiple role definitions
    /// </summary>
    Task UpdateDisplayOrdersAsync(string eventId, Dictionary<string, int> roleDisplayOrders);

    /// <summary>
    /// Hard delete all role definitions for an event
    /// </summary>
    Task DeleteAllByEventAsync(string eventId);
}
