using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageAreaRepository : IAreaRepository
{
    private readonly TableClient _table;

    public TableStorageAreaRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetAreasTable();
    }

    public async Task<AreaEntity> AddAsync(AreaEntity area)
    {
        await _table.AddEntityAsync(area);
        return area;
    }

    public async Task<AreaEntity?> GetAsync(string eventId, string areaId)
    {
        try
        {
            Response<AreaEntity> response = await _table.GetEntityAsync<AreaEntity>(eventId, areaId);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<AreaEntity>> GetByEventAsync(string eventId)
    {
        List<AreaEntity> areas = [];
        await foreach (AreaEntity area in _table.QueryAsync<AreaEntity>(a => a.PartitionKey == eventId))
        {
            areas.Add(area);
        }
        return areas.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Name);
    }

    public async Task<AreaEntity?> GetDefaultAreaAsync(string eventId)
    {
        // Return first match - there should only be one default area per event
#pragma warning disable S1751 // Loop intentionally returns on first iteration
        await foreach (AreaEntity area in _table.QueryAsync<AreaEntity>(
            a => a.PartitionKey == eventId && a.IsDefault))
        {
            return area;
        }
#pragma warning restore S1751
        return null;
    }

    public async Task UpdateAsync(AreaEntity area)
    {
        await _table.UpdateEntityAsync(area, area.ETag);
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
}
