using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IAdminUserRepository
{
    Task<AdminUserEntity> AddAsync(AdminUserEntity adminUser);
    Task<AdminUserEntity?> GetByEmailAsync(string email);
}
