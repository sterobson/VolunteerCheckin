using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Repository interface for managing incident reports
/// </summary>
public interface IIncidentRepository
{
    /// <summary>
    /// Get an incident by its ID
    /// </summary>
    Task<IncidentEntity?> GetAsync(string eventId, string incidentId);

    /// <summary>
    /// Get all incidents for an event
    /// </summary>
    Task<IEnumerable<IncidentEntity>> GetByEventAsync(string eventId);

    /// <summary>
    /// Get incidents for a specific area (by area ID)
    /// </summary>
    Task<IEnumerable<IncidentEntity>> GetByAreaAsync(string eventId, string areaId);

    /// <summary>
    /// Add a new incident
    /// </summary>
    Task AddAsync(IncidentEntity incident);

    /// <summary>
    /// Update an existing incident
    /// </summary>
    Task UpdateAsync(IncidentEntity incident);
}
