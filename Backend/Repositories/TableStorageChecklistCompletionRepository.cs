using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageChecklistCompletionRepository : IChecklistCompletionRepository
{
    private readonly TableClient _table;

    public TableStorageChecklistCompletionRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetChecklistCompletionsTable();
    }

    public async Task<ChecklistCompletionEntity> AddAsync(ChecklistCompletionEntity completion)
    {
        await _table.AddEntityAsync(completion);
        return completion;
    }

    public async Task<ChecklistCompletionEntity?> GetAsync(string eventId, string itemId, string completionId)
    {
        try
        {
            string partitionKey = ChecklistCompletionEntity.CreatePartitionKey(eventId);
            string rowKey = ChecklistCompletionEntity.CreateRowKey(itemId, completionId);
            Response<ChecklistCompletionEntity> response = await _table.GetEntityAsync<ChecklistCompletionEntity>(partitionKey, rowKey);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<ChecklistCompletionEntity>> GetByEventAsync(string eventId)
    {
        List<ChecklistCompletionEntity> completions = [];
        string partitionKey = ChecklistCompletionEntity.CreatePartitionKey(eventId);

        // EFFICIENT: Single partition scan to get all completions for event
        await foreach (ChecklistCompletionEntity completion in _table.QueryAsync<ChecklistCompletionEntity>(
            c => c.PartitionKey == partitionKey && !c.IsDeleted))
        {
            completions.Add(completion);
        }

        return completions.OrderBy(c => c.CompletedAt);
    }

    public async Task<IEnumerable<ChecklistCompletionEntity>> GetByItemAsync(string eventId, string itemId)
    {
        List<ChecklistCompletionEntity> completions = [];
        string partitionKey = ChecklistCompletionEntity.CreatePartitionKey(eventId);

        // Filter by RowKey prefix within the partition (RowKey starts with itemId#)
        await foreach (ChecklistCompletionEntity completion in _table.QueryAsync<ChecklistCompletionEntity>(
            c => c.PartitionKey == partitionKey && c.ChecklistItemId == itemId && !c.IsDeleted))
        {
            completions.Add(completion);
        }

        return completions.OrderBy(c => c.CompletedAt);
    }

    public async Task<IEnumerable<ChecklistCompletionEntity>> GetByMarshalAsync(string eventId, string marshalId)
    {
        List<ChecklistCompletionEntity> completions = [];
        string partitionKey = ChecklistCompletionEntity.CreatePartitionKey(eventId);

        // EFFICIENT: Single partition scan filtered by marshal
        await foreach (ChecklistCompletionEntity completion in _table.QueryAsync<ChecklistCompletionEntity>(
            c => c.PartitionKey == partitionKey && c.ContextOwnerMarshalId == marshalId && !c.IsDeleted))
        {
            completions.Add(completion);
        }

        return completions.OrderBy(c => c.CompletedAt);
    }

    public async Task UpdateAsync(ChecklistCompletionEntity completion)
    {
        await _table.UpdateEntityAsync(completion, completion.ETag);
    }

    public async Task DeleteAsync(string eventId, string itemId, string completionId)
    {
        string partitionKey = ChecklistCompletionEntity.CreatePartitionKey(eventId);
        string rowKey = ChecklistCompletionEntity.CreateRowKey(itemId, completionId);
        await _table.DeleteEntityAsync(partitionKey, rowKey);
    }

    public async Task DeleteAllByEventAsync(string eventId)
    {
        string partitionKey = ChecklistCompletionEntity.CreatePartitionKey(eventId);

        await foreach (ChecklistCompletionEntity completion in _table.QueryAsync<ChecklistCompletionEntity>(
            c => c.PartitionKey == partitionKey))
        {
            await _table.DeleteEntityAsync(partitionKey, completion.RowKey);
        }
    }

    public async Task DeleteAllByItemAsync(string eventId, string itemId)
    {
        string partitionKey = ChecklistCompletionEntity.CreatePartitionKey(eventId);

        // Delete all completions where RowKey starts with itemId#
        await foreach (ChecklistCompletionEntity completion in _table.QueryAsync<ChecklistCompletionEntity>(
            c => c.PartitionKey == partitionKey && c.ChecklistItemId == itemId))
        {
            await _table.DeleteEntityAsync(partitionKey, completion.RowKey);
        }
    }
}
