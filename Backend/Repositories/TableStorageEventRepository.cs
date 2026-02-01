using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

// Note: We use Delete + Add for V2 entities to reliably remove deprecated columns.
// UpdateEntityAsync (even with Replace mode) doesn't remove columns in Azure Table Storage.

public class TableStorageEventRepository : IEventRepository
{
    private readonly TableClient _table;

    public TableStorageEventRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetEventsTable();
    }

    public async Task<EventEntity> AddAsync(EventEntity eventEntity)
    {
        // New entities should always be V2 schema
        if (eventEntity.SchemaVersion >= EventEntity.CurrentSchemaVersion)
        {
            // Save as V2 to avoid creating deprecated columns
            EventEntityV2 v2Entity = EventEntityV2.FromEventEntity(eventEntity);
            await _table.AddEntityAsync(v2Entity);
            eventEntity.ETag = v2Entity.ETag;
        }
        else
        {
            await _table.AddEntityAsync(eventEntity);
        }
        return eventEntity;
    }

    public async Task<EventEntity?> GetAsync(string eventId)
    {
        try
        {
            Response<EventEntity> response = await _table.GetEntityAsync<EventEntity>(Constants.EventPartitionKey, eventId);
            EventEntity entity = response.Value;

            // Auto-upgrade schema if needed
            if (entity.SchemaVersion < EventEntity.CurrentSchemaVersion)
            {
                entity.UpgradeSchema();
                try
                {
                    // Delete + Add to reliably remove deprecated columns
                    await DeleteAndAddAsV2Async(entity);
                }
                catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 404 || ex.Status == 409)
                {
                    // Another request already upgraded this entity - re-read the fresh version
                    // 412 = ETag mismatch (entity was modified), 404 = entity deleted, 409 = entity already exists (Add failed)
                    Response<EventEntity> freshResponse = await _table.GetEntityAsync<EventEntity>(Constants.EventPartitionKey, eventId);
                    entity = freshResponse.Value;
                }
            }

            return entity;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<EventEntity>> GetAllAsync()
    {
        List<EventEntity> events = [];
        List<Task> migrationTasks = [];

        await foreach (EventEntity eventEntity in _table.QueryAsync<EventEntity>())
        {
            // Auto-upgrade schema if needed
            if (eventEntity.SchemaVersion < EventEntity.CurrentSchemaVersion)
            {
                eventEntity.UpgradeSchema();
                // Capture the entity for the closure
                EventEntity entityToMigrate = eventEntity;
                migrationTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Delete + Add to reliably remove deprecated columns
                        await DeleteAndAddAsV2Async(entityToMigrate);
                    }
                    catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 404 || ex.Status == 409)
                    {
                        // Another request already upgraded this entity - re-read the fresh version
                        // 412 = ETag mismatch (entity was modified), 404 = entity deleted, 409 = entity already exists (Add failed)
                        Response<EventEntity> freshResponse = await _table.GetEntityAsync<EventEntity>(entityToMigrate.PartitionKey, entityToMigrate.RowKey);
                        EventEntity fresh = freshResponse.Value;
                        // Copy fresh data back to the entity in our list
                        entityToMigrate.ETag = fresh.ETag;
                        entityToMigrate.SchemaVersion = fresh.SchemaVersion;
                        entityToMigrate.PayloadJson = fresh.PayloadJson;
                    }
                }));
            }

            events.Add(eventEntity);
        }

        // Wait for all migrations to complete
        if (migrationTasks.Count > 0)
        {
            await Task.WhenAll(migrationTasks);
        }

        return events;
    }

    public async Task UpdateAsync(EventEntity eventEntity)
    {
        // For V2 entities, use Delete + Add to ensure deprecated columns are removed
        if (eventEntity.SchemaVersion >= EventEntity.CurrentSchemaVersion)
        {
            await DeleteAndAddAsV2Async(eventEntity);
        }
        else
        {
            await _table.UpdateEntityAsync(eventEntity, eventEntity.ETag);
        }
    }

    public async Task DeleteAsync(string eventId)
    {
        await _table.DeleteEntityAsync(Constants.EventPartitionKey, eventId);
    }

    /// <summary>
    /// Delete the entity and re-add as V2 to reliably remove deprecated columns.
    /// This is the only way to remove columns in Azure Table Storage.
    /// </summary>
    private async Task DeleteAndAddAsV2Async(EventEntity entity)
    {
        EventEntityV2 v2Entity = EventEntityV2.FromEventEntity(entity);

        // Delete with ETag check to ensure we have the latest version
        await _table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, entity.ETag);

        try
        {
            // Add the V2 entity (only has V2 columns)
            Response addResponse = await _table.AddEntityAsync(v2Entity);

            // Update the original entity's ETag
            entity.ETag = addResponse.Headers.ETag ?? default;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // 409 means another request already added the entity - just re-read to get fresh ETag
            Response<EventEntity> freshResponse = await _table.GetEntityAsync<EventEntity>(entity.PartitionKey, entity.RowKey);
            entity.ETag = freshResponse.Value.ETag;
            entity.SchemaVersion = freshResponse.Value.SchemaVersion;
            entity.PayloadJson = freshResponse.Value.PayloadJson;
        }
        catch
        {
            // Critical: Delete succeeded but Add failed - try to restore the entity
            // This uses the full EventEntity to preserve all data
            try
            {
                entity.ETag = default; // Clear ETag for new insert
                await _table.AddEntityAsync(entity);
            }
            catch
            {
                // Failed to restore - data may be lost
                // This should be extremely rare (only if Azure is having issues)
            }
            throw;
        }
    }
}
