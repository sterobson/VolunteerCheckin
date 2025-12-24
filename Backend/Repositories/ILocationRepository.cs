using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface ILocationRepository
{
    Task<LocationEntity> AddAsync(LocationEntity location);
    Task<LocationEntity?> GetAsync(string eventId, string locationId);
    Task<IEnumerable<LocationEntity>> GetByEventAsync(string eventId);
    Task UpdateAsync(LocationEntity location);
    Task DeleteAsync(string eventId, string locationId);
    Task DeleteAllByEventAsync(string eventId);
}
