using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageMarshalRepository : IMarshalRepository
{
    private readonly TableClient _table;

    public TableStorageMarshalRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetMarshalsTable();
    }

    public async Task<MarshalEntity> AddAsync(MarshalEntity marshal)
    {
        await _table.AddEntityAsync(marshal);
        return marshal;
    }

    public async Task<MarshalEntity?> GetAsync(string eventId, string marshalId)
    {
        try
        {
            Response<MarshalEntity> response = await _table.GetEntityAsync<MarshalEntity>(eventId, marshalId);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<MarshalEntity>> GetByEventAsync(string eventId)
    {
        List<MarshalEntity> marshals = [];
        await foreach (MarshalEntity marshal in _table.QueryAsync<MarshalEntity>(m => m.PartitionKey == eventId))
        {
            marshals.Add(marshal);
        }
        return marshals;
    }

    public async Task<MarshalEntity?> FindByNameAsync(string eventId, string name)
    {
        await foreach (MarshalEntity marshal in _table.QueryAsync<MarshalEntity>(m => m.PartitionKey == eventId))
        {
            if (marshal.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return marshal;
            }
        }
        return null;
    }

    public async Task UpdateAsync(MarshalEntity marshal)
    {
        await _table.UpdateEntityAsync(marshal, marshal.ETag);
    }

    public async Task UpdateUnconditionalAsync(MarshalEntity marshal)
    {
        await _table.UpdateEntityAsync(marshal, ETag.All, TableUpdateMode.Merge);
    }

    public async Task DeleteAsync(string eventId, string marshalId)
    {
        await _table.DeleteEntityAsync(eventId, marshalId);
    }
}
