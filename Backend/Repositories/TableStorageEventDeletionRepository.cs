using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Azure Table Storage implementation of IEventDeletionRepository.
/// </summary>
public class TableStorageEventDeletionRepository : IEventDeletionRepository
{
    private readonly TableClient _table;

    public TableStorageEventDeletionRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetEventDeletionsTable();
    }

    public async Task<EventDeletionEntity?> GetByEventIdAsync(string eventId)
    {
        try
        {
            Response<EventDeletionEntity> response = await _table.GetEntityAsync<EventDeletionEntity>("deletion", eventId);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<EventDeletionEntity>> GetPendingAsync()
    {
        List<EventDeletionEntity> pending = [];
        await foreach (EventDeletionEntity entity in _table.QueryAsync<EventDeletionEntity>(
            e => e.PartitionKey == "deletion" &&
                 (e.Status == EventDeletionStatus.Pending || e.Status == EventDeletionStatus.InProgress)))
        {
            pending.Add(entity);
        }
        return pending;
    }

    public async Task AddAsync(EventDeletionEntity deletion)
    {
        deletion.PartitionKey = "deletion";
        deletion.RowKey = deletion.EventId;
        await _table.AddEntityAsync(deletion);
    }

    public async Task UpdateAsync(EventDeletionEntity deletion)
    {
        // Use ETag.All for unconditional updates since this is used by background cleanup jobs
        // where optimistic concurrency isn't needed
        await _table.UpdateEntityAsync(deletion, ETag.All, TableUpdateMode.Replace);
    }

    public async Task<bool> IsDeletionPendingAsync(string eventId)
    {
        EventDeletionEntity? deletion = await GetByEventIdAsync(eventId);
        return deletion != null &&
               (deletion.Status == EventDeletionStatus.Pending ||
                deletion.Status == EventDeletionStatus.InProgress);
    }
}
