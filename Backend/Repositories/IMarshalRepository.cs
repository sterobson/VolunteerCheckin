using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IMarshalRepository
{
    Task<MarshalEntity> AddAsync(MarshalEntity marshal);
    Task<MarshalEntity?> GetAsync(string eventId, string marshalId);
    Task<IEnumerable<MarshalEntity>> GetByEventAsync(string eventId);
    Task<MarshalEntity?> FindByNameAsync(string eventId, string name);
    Task UpdateAsync(MarshalEntity marshal);
    Task DeleteAsync(string eventId, string marshalId);
}
