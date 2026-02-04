using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Azure Table Storage implementation of IPendingEventRepository
/// </summary>
public class TableStoragePendingEventRepository : IPendingEventRepository
{
    private readonly TableClient _table;

    public TableStoragePendingEventRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetPendingEventsTable();
    }

    public async Task AddAsync(PendingEventEntity pendingEvent)
    {
        await _table.AddEntityAsync(pendingEvent);
    }

    public async Task<PendingEventEntity?> GetBySessionIdAsync(string sessionId)
    {
        try
        {
            Response<PendingEventEntity> response = await _table.GetEntityAsync<PendingEventEntity>(
                PendingEventEntity.PendingPartitionKey, sessionId);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task UpdateAsync(PendingEventEntity pendingEvent)
    {
        await _table.UpdateEntityAsync(pendingEvent, pendingEvent.ETag, TableUpdateMode.Replace);
    }

    public async Task DeleteAsync(string sessionId)
    {
        await _table.DeleteEntityAsync(PendingEventEntity.PendingPartitionKey, sessionId);
    }

    public async Task<IEnumerable<PendingEventEntity>> GetExpiredAsync()
    {
        DateTime now = DateTime.UtcNow;
        List<PendingEventEntity> expired = [];
        await foreach (PendingEventEntity entity in _table.QueryAsync<PendingEventEntity>(
            e => e.PartitionKey == PendingEventEntity.PendingPartitionKey && e.Status == "Pending"))
        {
            if (entity.ExpiresAt < now)
            {
                expired.Add(entity);
            }
        }
        return expired;
    }
}
