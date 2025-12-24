using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageUserEventMappingRepository : IUserEventMappingRepository
{
    private readonly TableClient _table;

    public TableStorageUserEventMappingRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetUserEventMappingsTable();
    }

    public async Task<UserEventMappingEntity> AddAsync(UserEventMappingEntity mapping)
    {
        await _table.AddEntityAsync(mapping);
        return mapping;
    }

    public async Task<IEnumerable<UserEventMappingEntity>> GetByEventAsync(string eventId)
    {
        List<UserEventMappingEntity> mappings = [];
        await foreach (UserEventMappingEntity mapping in _table.QueryAsync<UserEventMappingEntity>(
            m => m.PartitionKey == eventId))
        {
            mappings.Add(mapping);
        }
        return mappings;
    }

    public async Task<IEnumerable<UserEventMappingEntity>> GetByUserAsync(string userEmail)
    {
        List<UserEventMappingEntity> mappings = [];
        await foreach (UserEventMappingEntity mapping in _table.QueryAsync<UserEventMappingEntity>(
            m => m.RowKey == userEmail))
        {
            mappings.Add(mapping);
        }
        return mappings;
    }

    public async Task<UserEventMappingEntity?> GetAsync(string eventId, string userEmail)
    {
        try
        {
            Response<UserEventMappingEntity> response = await _table.GetEntityAsync<UserEventMappingEntity>(eventId, userEmail);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string eventId, string userEmail)
    {
        await _table.DeleteEntityAsync(eventId, userEmail);
    }
}
