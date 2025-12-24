using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IEventRepository
{
    Task<EventEntity> AddAsync(EventEntity eventEntity);
    Task<EventEntity?> GetAsync(string eventId);
    Task<IEnumerable<EventEntity>> GetAllAsync();
    Task UpdateAsync(EventEntity eventEntity);
    Task DeleteAsync(string eventId);
}
