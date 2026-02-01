using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Azure Table Storage implementation of IIncidentRepository
/// </summary>
public class TableStorageIncidentRepository : IIncidentRepository
{
    private readonly TableClient _table;

    public TableStorageIncidentRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetIncidentsTable();
    }

    public async Task<IncidentEntity?> GetAsync(string eventId, string incidentId)
    {
        try
        {
            Response<IncidentEntity> response = await _table.GetEntityAsync<IncidentEntity>(eventId, incidentId);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<IncidentEntity>> GetByEventAsync(string eventId)
    {
        List<IncidentEntity> incidents = [];
        await foreach (IncidentEntity incident in _table.QueryAsync<IncidentEntity>(i => i.PartitionKey == eventId))
        {
            incidents.Add(incident);
        }
        return incidents.OrderByDescending(i => i.IncidentTime);
    }

    public async Task<IEnumerable<IncidentEntity>> GetByAreaAsync(string eventId, string areaId)
    {
        List<IncidentEntity> incidents = [];
        await foreach (IncidentEntity incident in _table.QueryAsync<IncidentEntity>(i => i.PartitionKey == eventId && i.AreaId == areaId))
        {
            incidents.Add(incident);
        }
        return incidents.OrderByDescending(i => i.IncidentTime);
    }

    public async Task AddAsync(IncidentEntity incident)
    {
        await _table.AddEntityAsync(incident);
    }

    public async Task UpdateAsync(IncidentEntity incident)
    {
        await _table.UpdateEntityAsync(incident, incident.ETag);
    }

    public async Task DeleteAllByEventAsync(string eventId)
    {
        await foreach (IncidentEntity incident in _table.QueryAsync<IncidentEntity>(i => i.PartitionKey == eventId))
        {
            await _table.DeleteEntityAsync(incident.PartitionKey, incident.RowKey);
        }
    }
}
