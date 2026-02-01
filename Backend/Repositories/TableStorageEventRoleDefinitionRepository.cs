using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Azure Table Storage implementation of IEventRoleDefinitionRepository
/// </summary>
public class TableStorageEventRoleDefinitionRepository : IEventRoleDefinitionRepository
{
    private readonly TableClient _table;

    public TableStorageEventRoleDefinitionRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetEventRoleDefinitionsTable();
    }

    public async Task<EventRoleDefinitionEntity?> GetAsync(string eventId, string roleId)
    {
        try
        {
            Response<EventRoleDefinitionEntity> response = await _table.GetEntityAsync<EventRoleDefinitionEntity>(eventId, roleId);
            EventRoleDefinitionEntity roleDefinition = response.Value;

            // Return null for deleted role definitions
            if (roleDefinition.IsDeleted)
            {
                return null;
            }

            return roleDefinition;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<EventRoleDefinitionEntity>> GetByEventAsync(string eventId)
    {
        List<EventRoleDefinitionEntity> roleDefinitions = [];
        await foreach (EventRoleDefinitionEntity roleDefinition in _table.QueryAsync<EventRoleDefinitionEntity>(r => r.PartitionKey == eventId && !r.IsDeleted))
        {
            roleDefinitions.Add(roleDefinition);
        }
        return roleDefinitions
            .OrderBy(r => r.DisplayOrder)
            .ThenBy(r => r.Name, StringComparer.Create(System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.IgnoreCase));
    }

    public async Task<EventRoleDefinitionEntity?> GetByNameAsync(string eventId, string name)
    {
        await foreach (EventRoleDefinitionEntity roleDefinition in _table.QueryAsync<EventRoleDefinitionEntity>(r => r.PartitionKey == eventId && !r.IsDeleted))
        {
            if (string.Equals(roleDefinition.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return roleDefinition;
            }
        }
        return null;
    }

    public async Task AddAsync(EventRoleDefinitionEntity roleDefinition)
    {
        await _table.AddEntityAsync(roleDefinition);
    }

    public async Task UpdateAsync(EventRoleDefinitionEntity roleDefinition)
    {
        await _table.UpdateEntityAsync(roleDefinition, roleDefinition.ETag, TableUpdateMode.Replace);
    }

    public async Task DeleteAsync(string eventId, string roleId)
    {
        EventRoleDefinitionEntity? roleDefinition = await GetAsync(eventId, roleId);
        if (roleDefinition != null)
        {
            roleDefinition.IsDeleted = true;
            await _table.UpdateEntityAsync(roleDefinition, roleDefinition.ETag);
        }
    }

    public async Task UpdateDisplayOrdersAsync(string eventId, Dictionary<string, int> roleDisplayOrders)
    {
        foreach (KeyValuePair<string, int> kvp in roleDisplayOrders)
        {
            try
            {
                Response<EventRoleDefinitionEntity> response = await _table.GetEntityAsync<EventRoleDefinitionEntity>(eventId, kvp.Key);
                EventRoleDefinitionEntity roleDefinition = response.Value;
                roleDefinition.DisplayOrder = kvp.Value;
                await _table.UpdateEntityAsync(roleDefinition, roleDefinition.ETag, TableUpdateMode.Replace);
            }
            catch (RequestFailedException)
            {
                // Skip role definitions that don't exist
            }
        }
    }

    public async Task DeleteAllByEventAsync(string eventId)
    {
        // PartitionKey is EventId, so we can efficiently query and delete all
        List<EventRoleDefinitionEntity> roleDefinitionsToDelete = [];
        await foreach (EventRoleDefinitionEntity roleDefinition in _table.QueryAsync<EventRoleDefinitionEntity>(r => r.PartitionKey == eventId))
        {
            roleDefinitionsToDelete.Add(roleDefinition);
        }

        foreach (EventRoleDefinitionEntity roleDefinition in roleDefinitionsToDelete)
        {
            await _table.DeleteEntityAsync(roleDefinition.PartitionKey, roleDefinition.RowKey);
        }
    }
}
