using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageEventRoleRepository : IEventRoleRepository
{
    private readonly TableClient _table;

    public TableStorageEventRoleRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetEventRolesTable();
    }

    public async Task<EventRoleEntity> AddAsync(EventRoleEntity eventRole)
    {
        await _table.AddEntityAsync(eventRole);
        return eventRole;
    }

    public async Task<EventRoleEntity?> GetAsync(string personId, string eventId, string roleId)
    {
        try
        {
            string rowKey = EventRoleEntity.CreateRowKey(eventId, roleId);
            Response<EventRoleEntity> response = await _table.GetEntityAsync<EventRoleEntity>(personId, rowKey);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<EventRoleEntity>> GetByPersonAsync(string personId)
    {
        List<EventRoleEntity> roles = [];
        await foreach (EventRoleEntity role in _table.QueryAsync<EventRoleEntity>(r => r.PartitionKey == personId))
        {
            roles.Add(role);
        }
        return roles;
    }

    public async Task<IEnumerable<EventRoleEntity>> GetByPersonAndEventAsync(string personId, string eventId)
    {
        List<EventRoleEntity> roles = [];
        await foreach (EventRoleEntity role in _table.QueryAsync<EventRoleEntity>(r => r.PartitionKey == personId))
        {
            if (role.EventId == eventId)
            {
                roles.Add(role);
            }
        }
        return roles;
    }

    public async Task<IEnumerable<EventRoleEntity>> GetByEventAsync(string eventId)
    {
        List<EventRoleEntity> roles = [];
        // This is less efficient as we need to scan all partitions
        // Consider adding a secondary index if this becomes a performance issue
        await foreach (EventRoleEntity role in _table.QueryAsync<EventRoleEntity>())
        {
            if (role.EventId == eventId)
            {
                roles.Add(role);
            }
        }
        return roles;
    }

    public async Task UpdateAsync(EventRoleEntity eventRole)
    {
        await _table.UpdateEntityAsync(eventRole, eventRole.ETag);
    }

    public async Task DeleteAsync(string personId, string rowKey)
    {
        await _table.DeleteEntityAsync(personId, rowKey);
    }
}
