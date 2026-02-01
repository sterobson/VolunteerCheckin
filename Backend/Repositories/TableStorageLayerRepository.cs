using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageLayerRepository : ILayerRepository
{
    private readonly TableClient _table;

    public TableStorageLayerRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetLayersTable();
    }

    public async Task<LayerEntity> AddAsync(LayerEntity layer)
    {
        await _table.AddEntityAsync(layer);
        return layer;
    }

    public async Task<LayerEntity?> GetAsync(string eventId, string layerId)
    {
        try
        {
            Response<LayerEntity> response = await _table.GetEntityAsync<LayerEntity>(eventId, layerId);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<LayerEntity>> GetByEventAsync(string eventId)
    {
        List<LayerEntity> layers = [];
        await foreach (LayerEntity layer in _table.QueryAsync<LayerEntity>(l => l.PartitionKey == eventId))
        {
            layers.Add(layer);
        }
        return layers.OrderBy(l => l.DisplayOrder).ThenBy(l => l.Name, StringComparer.Ordinal);
    }

    public async Task UpdateAsync(LayerEntity layer)
    {
        await _table.UpdateEntityAsync(layer, layer.ETag);
    }

    public async Task DeleteAsync(string eventId, string layerId)
    {
        await _table.DeleteEntityAsync(eventId, layerId);
    }

    public async Task DeleteAllByEventAsync(string eventId)
    {
        await foreach (LayerEntity layer in _table.QueryAsync<LayerEntity>(l => l.PartitionKey == eventId))
        {
            await _table.DeleteEntityAsync(eventId, layer.RowKey);
        }
    }
}
