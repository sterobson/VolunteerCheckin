using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageAdminUserRepository : IAdminUserRepository
{
    private readonly TableClient _table;

    public TableStorageAdminUserRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetAdminUsersTable();
    }

    public async Task<AdminUserEntity> AddAsync(AdminUserEntity adminUser)
    {
        await _table.AddEntityAsync(adminUser);
        return adminUser;
    }

    public async Task<AdminUserEntity?> GetByEmailAsync(string email)
    {
        try
        {
            Response<AdminUserEntity> response = await _table.GetEntityAsync<AdminUserEntity>("ADMIN", email);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
