using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageAssignmentRepository : IAssignmentRepository
{
    private readonly TableClient _table;

    public TableStorageAssignmentRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetAssignmentsTable();
    }

    public async Task<AssignmentEntity> AddAsync(AssignmentEntity assignment)
    {
        await _table.AddEntityAsync(assignment);
        return assignment;
    }

    public async Task<IEnumerable<AssignmentEntity>> GetAllAsync()
    {
        List<AssignmentEntity> assignments = [];
        await foreach (AssignmentEntity assignment in _table.QueryAsync<AssignmentEntity>())
        {
            assignments.Add(assignment);
        }
        return assignments;
    }

    public async Task<IEnumerable<AssignmentEntity>> GetByEventAsync(string eventId)
    {
        List<AssignmentEntity> assignments = [];
        await foreach (AssignmentEntity assignment in _table.QueryAsync<AssignmentEntity>(a => a.PartitionKey == eventId))
        {
            assignments.Add(assignment);
        }
        return assignments;
    }

    public async Task<IEnumerable<AssignmentEntity>> GetByMarshalAsync(string eventId, string marshalId)
    {
        List<AssignmentEntity> assignments = [];
        await foreach (AssignmentEntity assignment in _table.QueryAsync<AssignmentEntity>(
            a => a.PartitionKey == eventId && a.MarshalId == marshalId))
        {
            assignments.Add(assignment);
        }
        return assignments;
    }

    public async Task<IEnumerable<AssignmentEntity>> GetByLocationAsync(string eventId, string locationId)
    {
        List<AssignmentEntity> assignments = [];
        await foreach (AssignmentEntity assignment in _table.QueryAsync<AssignmentEntity>(
            a => a.PartitionKey == eventId && a.LocationId == locationId))
        {
            assignments.Add(assignment);
        }
        return assignments;
    }

    public async Task<bool> ExistsAsync(string eventId, string marshalId, string locationId)
    {
        await foreach (AssignmentEntity assignment in _table.QueryAsync<AssignmentEntity>(
            a => a.PartitionKey == eventId && a.MarshalId == marshalId && a.LocationId == locationId))
        {
            return true;
        }
        return false;
    }

    public async Task UpdateAsync(AssignmentEntity assignment)
    {
        await _table.UpdateEntityAsync(assignment, assignment.ETag);
    }

    public async Task DeleteAsync(string eventId, string assignmentId)
    {
        await _table.DeleteEntityAsync(eventId, assignmentId);
    }

    public async Task DeleteAllByEventAsync(string eventId)
    {
        await foreach (AssignmentEntity assignment in _table.QueryAsync<AssignmentEntity>(a => a.PartitionKey == eventId))
        {
            await _table.DeleteEntityAsync(eventId, assignment.RowKey);
        }
    }

    public async Task DeleteAllByLocationAsync(string eventId, string locationId)
    {
        await foreach (AssignmentEntity assignment in _table.QueryAsync<AssignmentEntity>(
            a => a.PartitionKey == eventId && a.LocationId == locationId))
        {
            await _table.DeleteEntityAsync(eventId, assignment.RowKey);
        }
    }
}
