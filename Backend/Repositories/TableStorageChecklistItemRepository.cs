using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageChecklistItemRepository : IChecklistItemRepository
{
    private readonly TableClient _table;

    public TableStorageChecklistItemRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetChecklistItemsTable();
    }

    public async Task<ChecklistItemEntity> AddAsync(ChecklistItemEntity item)
    {
        await _table.AddEntityAsync(item);
        return item;
    }

    public async Task<ChecklistItemEntity?> GetAsync(string eventId, string itemId)
    {
        try
        {
            Response<ChecklistItemEntity> response = await _table.GetEntityAsync<ChecklistItemEntity>(eventId, itemId);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<ChecklistItemEntity>> GetByEventAsync(string eventId)
    {
        List<ChecklistItemEntity> items = [];
        await foreach (ChecklistItemEntity item in _table.QueryAsync<ChecklistItemEntity>(i => i.PartitionKey == eventId))
        {
            items.Add(item);
        }
        return items.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Text, StringComparer.Ordinal);
    }

    public async Task UpdateAsync(ChecklistItemEntity item)
    {
        await _table.UpdateEntityAsync(item, item.ETag);
    }

    public async Task DeleteAsync(string eventId, string itemId)
    {
        await _table.DeleteEntityAsync(eventId, itemId);
    }

    public async Task DeleteAllByEventAsync(string eventId)
    {
        await foreach (ChecklistItemEntity item in _table.QueryAsync<ChecklistItemEntity>(i => i.PartitionKey == eventId))
        {
            await _table.DeleteEntityAsync(eventId, item.RowKey);
        }
    }

    public async Task UpdateDisplayOrdersAsync(string eventId, Dictionary<string, int> itemDisplayOrders)
    {
        foreach (KeyValuePair<string, int> kvp in itemDisplayOrders)
        {
            try
            {
                Response<ChecklistItemEntity> response = await _table.GetEntityAsync<ChecklistItemEntity>(eventId, kvp.Key);
                ChecklistItemEntity item = response.Value;
                item.DisplayOrder = kvp.Value;
                await _table.UpdateEntityAsync(item, item.ETag);
            }
            catch (RequestFailedException)
            {
                // Skip items that don't exist
            }
        }
    }
}
