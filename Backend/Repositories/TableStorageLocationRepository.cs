using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

// Note: We use Delete + Add for V2 entities to reliably remove deprecated columns.
// UpdateEntityAsync (even with Replace mode) doesn't remove columns in Azure Table Storage.

public class TableStorageLocationRepository : ILocationRepository
{
    private readonly TableClient _table;

    public TableStorageLocationRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetLocationsTable();
    }

    public async Task<LocationEntity> AddAsync(LocationEntity location)
    {
        // New entities should always be saved as V2
        location.UpgradeSchema();
        LocationEntityV2 v2Entity = LocationEntityV2.FromLocationEntity(location);
        await _table.AddEntityAsync(v2Entity);
        location.ETag = v2Entity.ETag;
        return location;
    }

    public async Task<LocationEntity?> GetAsync(string eventId, string locationId)
    {
        try
        {
            Response<LocationEntity> response = await _table.GetEntityAsync<LocationEntity>(eventId, locationId);
            LocationEntity entity = response.Value;

            // Auto-upgrade schema if needed
            if (entity.SchemaVersion < LocationEntity.CurrentSchemaVersion)
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
                    Response<LocationEntity> freshResponse = await _table.GetEntityAsync<LocationEntity>(eventId, locationId);
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

    public async Task<IEnumerable<LocationEntity>> GetByEventAsync(string eventId)
    {
        List<LocationEntity> locations = [];
        List<Task> migrationTasks = [];

        await foreach (LocationEntity location in _table.QueryAsync<LocationEntity>(l => l.PartitionKey == eventId))
        {
            // Auto-upgrade schema if needed
            if (location.SchemaVersion < LocationEntity.CurrentSchemaVersion)
            {
                location.UpgradeSchema();
                // Capture the entity for the closure
                LocationEntity entityToMigrate = location;
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
                        Response<LocationEntity> freshResponse = await _table.GetEntityAsync<LocationEntity>(entityToMigrate.PartitionKey, entityToMigrate.RowKey);
                        LocationEntity fresh = freshResponse.Value;
                        // Copy fresh data back to the entity in our list
                        entityToMigrate.ETag = fresh.ETag;
                        entityToMigrate.SchemaVersion = fresh.SchemaVersion;
                        entityToMigrate.PayloadJson = fresh.PayloadJson;
                    }
                }));
            }

            locations.Add(location);
        }

        // Wait for all migrations to complete
        if (migrationTasks.Count > 0)
        {
            await Task.WhenAll(migrationTasks);
        }

        return locations;
    }

    public async Task<IEnumerable<LocationEntity>> GetByAreaAsync(string eventId, string areaId)
    {
        List<LocationEntity> locations = [];
        await foreach (LocationEntity location in _table.QueryAsync<LocationEntity>(
            l => l.PartitionKey == eventId))
        {
            // Auto-upgrade schema if needed
            if (location.SchemaVersion < LocationEntity.CurrentSchemaVersion)
            {
                location.UpgradeSchema();
                // Note: We don't wait for migration here to keep the method fast
                // Migration will happen on next save or explicit read
            }

            // Check if this checkpoint belongs to the specified area
            LocationPayload payload = location.GetPayload();
            if (payload.AreaIds.Contains(areaId, StringComparer.Ordinal))
            {
                locations.Add(location);
            }
        }
        return locations;
    }

    public async Task<int> CountByAreaAsync(string eventId, string areaId)
    {
        int count = 0;
        await foreach (LocationEntity location in _table.QueryAsync<LocationEntity>(
            l => l.PartitionKey == eventId))
        {
            // Check if this checkpoint belongs to the specified area
            LocationPayload payload = location.GetPayload();
            if (payload.AreaIds.Contains(areaId, StringComparer.Ordinal))
            {
                count++;
            }
        }
        return count;
    }

    public async Task UpdateAsync(LocationEntity location)
    {
        // For V2 entities, use Delete + Add to ensure deprecated columns are removed
        if (location.SchemaVersion >= LocationEntity.CurrentSchemaVersion)
        {
            await DeleteAndAddAsV2Async(location);
        }
        else
        {
            // Upgrade to V2 before saving
            location.UpgradeSchema();
            await DeleteAndAddAsV2Async(location);
        }
    }

    public async Task DeleteAsync(string eventId, string locationId)
    {
        await _table.DeleteEntityAsync(eventId, locationId);
    }

    public async Task DeleteAllByEventAsync(string eventId)
    {
        await foreach (LocationEntity location in _table.QueryAsync<LocationEntity>(l => l.PartitionKey == eventId))
        {
            await _table.DeleteEntityAsync(eventId, location.RowKey);
        }
    }

    /// <summary>
    /// Delete the entity and re-add as V2 to reliably remove deprecated columns.
    /// This is the only way to remove columns in Azure Table Storage.
    /// </summary>
    private async Task DeleteAndAddAsV2Async(LocationEntity entity)
    {
        LocationEntityV2 v2Entity = LocationEntityV2.FromLocationEntity(entity);

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
            Response<LocationEntity> freshResponse = await _table.GetEntityAsync<LocationEntity>(entity.PartitionKey, entity.RowKey);
            entity.ETag = freshResponse.Value.ETag;
            entity.SchemaVersion = freshResponse.Value.SchemaVersion;
            entity.PayloadJson = freshResponse.Value.PayloadJson;
        }
        catch
        {
            // Critical: Delete succeeded but Add failed - try to restore the entity
            // This uses the full LocationEntity to preserve all data
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
