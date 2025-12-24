using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IUserEventMappingRepository
{
    Task<UserEventMappingEntity> AddAsync(UserEventMappingEntity mapping);
    Task<IEnumerable<UserEventMappingEntity>> GetByEventAsync(string eventId);
    Task<IEnumerable<UserEventMappingEntity>> GetByUserAsync(string userEmail);
    Task<UserEventMappingEntity?> GetAsync(string eventId, string userEmail);
    Task DeleteAsync(string eventId, string userEmail);
}
