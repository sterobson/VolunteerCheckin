using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageLocationRepository : ILocationRepository
{
    private readonly TableClient _table;

    public TableStorageLocationRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetLocationsTable();
    }

    public async Task<LocationEntity> AddAsync(LocationEntity location)
    {
        await _table.AddEntityAsync(location);
        return location;
    }

    public async Task<LocationEntity?> GetAsync(string eventId, string locationId)
    {
        try
        {
            Response<LocationEntity> response = await _table.GetEntityAsync<LocationEntity>(eventId, locationId);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<LocationEntity>> GetByEventAsync(string eventId)
    {
        List<LocationEntity> locations = [];
        await foreach (LocationEntity location in _table.QueryAsync<LocationEntity>(l => l.PartitionKey == eventId))
        {
            locations.Add(location);
        }
        return locations;
    }

    public async Task<IEnumerable<LocationEntity>> GetByAreaAsync(string eventId, string areaId)
    {
        List<LocationEntity> locations = [];
        await foreach (LocationEntity location in _table.QueryAsync<LocationEntity>(
            l => l.PartitionKey == eventId))
        {
            // Parse the area IDs JSON and check if this checkpoint belongs to the specified area
            List<string>? areaIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(location.AreaIdsJson);
            if (areaIds != null && areaIds.Contains(areaId))
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
            // Parse the area IDs JSON and check if this checkpoint belongs to the specified area
            List<string>? areaIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(location.AreaIdsJson);
            if (areaIds != null && areaIds.Contains(areaId))
            {
                count++;
            }
        }
        return count;
    }

    public async Task UpdateAsync(LocationEntity location)
    {
        await _table.UpdateEntityAsync(location, location.ETag);
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
}
