using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IAreaRepository
{
    Task<AreaEntity> AddAsync(AreaEntity area);
    Task<AreaEntity?> GetAsync(string eventId, string areaId);
    Task<IEnumerable<AreaEntity>> GetByEventAsync(string eventId);
    Task<AreaEntity?> GetDefaultAreaAsync(string eventId);
    Task UpdateAsync(AreaEntity area);
    Task DeleteAsync(string eventId, string areaId);
    Task DeleteAllByEventAsync(string eventId);
}
