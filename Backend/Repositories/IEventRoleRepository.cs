using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IEventRoleRepository
{
    Task<EventRoleEntity> AddAsync(EventRoleEntity eventRole);
    Task<EventRoleEntity?> GetAsync(string personId, string eventId, string roleId);
    Task<IEnumerable<EventRoleEntity>> GetByPersonAsync(string personId);
    Task<IEnumerable<EventRoleEntity>> GetByPersonAndEventAsync(string personId, string eventId);
    Task<IEnumerable<EventRoleEntity>> GetByEventAsync(string eventId);
    Task UpdateAsync(EventRoleEntity eventRole);
    Task DeleteAsync(string personId, string rowKey);
}
