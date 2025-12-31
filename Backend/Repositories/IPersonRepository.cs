using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IPersonRepository
{
    Task<PersonEntity> AddAsync(PersonEntity person);
    Task<PersonEntity?> GetAsync(string personId);
    Task<PersonEntity?> GetByEmailAsync(string email);
    Task<IEnumerable<PersonEntity>> GetAllAsync();
    Task UpdateAsync(PersonEntity person);
    Task DeleteAsync(string personId);
}
