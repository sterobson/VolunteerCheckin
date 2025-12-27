using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageEventRepository : IEventRepository
{
    private readonly TableClient _table;

    public TableStorageEventRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetEventsTable();
    }

    public async Task<EventEntity> AddAsync(EventEntity eventEntity)
    {
        await _table.AddEntityAsync(eventEntity);
        return eventEntity;
    }

    public async Task<EventEntity?> GetAsync(string eventId)
    {
        try
        {
            Response<EventEntity> response = await _table.GetEntityAsync<EventEntity>(Constants.EventPartitionKey, eventId);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<EventEntity>> GetAllAsync()
    {
        List<EventEntity> events = [];
        await foreach (EventEntity eventEntity in _table.QueryAsync<EventEntity>())
        {
            events.Add(eventEntity);
        }
        return events;
    }

    public async Task UpdateAsync(EventEntity eventEntity)
    {
        await _table.UpdateEntityAsync(eventEntity, eventEntity.ETag);
    }

    public async Task DeleteAsync(string eventId)
    {
        await _table.DeleteEntityAsync(Constants.EventPartitionKey, eventId);
    }
}
