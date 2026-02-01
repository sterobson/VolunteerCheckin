using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

// Note: We use Delete + Add for V2 entities to reliably remove deprecated columns.
// UpdateEntityAsync (even with Replace mode) doesn't remove columns in Azure Table Storage.

public class TableStorageAreaRepository : IAreaRepository
{
    private readonly TableClient _table;

    public TableStorageAreaRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetAreasTable();
    }

    public async Task<AreaEntity> AddAsync(AreaEntity area)
    {
        // New entities should always be saved as V2
        area.UpgradeSchema();
        AreaEntityV2 v2Entity = AreaEntityV2.FromAreaEntity(area);
        await _table.AddEntityAsync(v2Entity);
        area.ETag = v2Entity.ETag;
        return area;
    }

    public async Task<AreaEntity?> GetAsync(string eventId, string areaId)
    {
        try
        {
            Response<AreaEntity> response = await _table.GetEntityAsync<AreaEntity>(eventId, areaId);
            AreaEntity entity = response.Value;

            // Auto-upgrade schema if needed
            if (entity.SchemaVersion < AreaEntity.CurrentSchemaVersion)
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
                    Response<AreaEntity> freshResponse = await _table.GetEntityAsync<AreaEntity>(eventId, areaId);
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

    public async Task<IEnumerable<AreaEntity>> GetByEventAsync(string eventId)
    {
        List<AreaEntity> areas = [];
        List<Task> migrationTasks = [];

        await foreach (AreaEntity area in _table.QueryAsync<AreaEntity>(a => a.PartitionKey == eventId))
        {
            // Auto-upgrade schema if needed
            if (area.SchemaVersion < AreaEntity.CurrentSchemaVersion)
            {
                area.UpgradeSchema();
                // Capture the entity for the closure
                AreaEntity entityToMigrate = area;
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
                        Response<AreaEntity> freshResponse = await _table.GetEntityAsync<AreaEntity>(entityToMigrate.PartitionKey, entityToMigrate.RowKey);
                        AreaEntity fresh = freshResponse.Value;
                        // Copy fresh data back to the entity in our list
                        entityToMigrate.ETag = fresh.ETag;
                        entityToMigrate.SchemaVersion = fresh.SchemaVersion;
                        entityToMigrate.PayloadJson = fresh.PayloadJson;
                    }
                }));
            }

            areas.Add(area);
        }

        // Wait for all migrations to complete
        if (migrationTasks.Count > 0)
        {
            await Task.WhenAll(migrationTasks);
        }

        return areas.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Name, StringComparer.Ordinal);
    }

    public async Task<AreaEntity?> GetDefaultAreaAsync(string eventId)
    {
        // Return first match - there should only be one default area per event
#pragma warning disable S1751 // Loop intentionally returns on first iteration
        await foreach (AreaEntity area in _table.QueryAsync<AreaEntity>(
            a => a.PartitionKey == eventId && a.IsDefault))
        {
            // Auto-upgrade schema if needed
            if (area.SchemaVersion < AreaEntity.CurrentSchemaVersion)
            {
                area.UpgradeSchema();
                try
                {
                    // Delete + Add to reliably remove deprecated columns
                    await DeleteAndAddAsV2Async(area);
                }
                catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 404 || ex.Status == 409)
                {
                    // Another request already upgraded this entity - re-read the fresh version
                    Response<AreaEntity> freshResponse = await _table.GetEntityAsync<AreaEntity>(area.PartitionKey, area.RowKey);
                    return freshResponse.Value;
                }
            }
            return area;
        }
#pragma warning restore S1751
        return null;
    }

    public async Task UpdateAsync(AreaEntity area)
    {
        // For V2 entities, use Delete + Add to ensure deprecated columns are removed
        if (area.SchemaVersion >= AreaEntity.CurrentSchemaVersion)
        {
            await DeleteAndAddAsV2Async(area);
        }
        else
        {
            // Upgrade to V2 before saving
            area.UpgradeSchema();
            await DeleteAndAddAsV2Async(area);
        }
    }

    public async Task DeleteAsync(string eventId, string areaId)
    {
        await _table.DeleteEntityAsync(eventId, areaId);
    }

    public async Task DeleteAllByEventAsync(string eventId)
    {
        await foreach (AreaEntity area in _table.QueryAsync<AreaEntity>(a => a.PartitionKey == eventId))
        {
            await _table.DeleteEntityAsync(eventId, area.RowKey);
        }
    }

    /// <summary>
    /// Delete the entity and re-add as V2 to reliably remove deprecated columns.
    /// This is the only way to remove columns in Azure Table Storage.
    /// </summary>
    private async Task DeleteAndAddAsV2Async(AreaEntity entity)
    {
        AreaEntityV2 v2Entity = AreaEntityV2.FromAreaEntity(entity);

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
            Response<AreaEntity> freshResponse = await _table.GetEntityAsync<AreaEntity>(entity.PartitionKey, entity.RowKey);
            entity.ETag = freshResponse.Value.ETag;
            entity.SchemaVersion = freshResponse.Value.SchemaVersion;
            entity.PayloadJson = freshResponse.Value.PayloadJson;
        }
        catch
        {
            // Critical: Delete succeeded but Add failed - try to restore the entity
            // This uses the full AreaEntity to preserve all data
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
