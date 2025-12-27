using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IAssignmentRepository
{
    Task<AssignmentEntity> AddAsync(AssignmentEntity assignment);
    Task<AssignmentEntity?> GetAsync(string eventId, string assignmentId);
    Task<IEnumerable<AssignmentEntity>> GetByEventAsync(string eventId);
    Task<IEnumerable<AssignmentEntity>> GetByMarshalAsync(string eventId, string marshalId);
    Task<IEnumerable<AssignmentEntity>> GetByLocationAsync(string eventId, string locationId);
    Task<bool> ExistsAsync(string eventId, string marshalId, string locationId);
    Task UpdateAsync(AssignmentEntity assignment);
    Task DeleteAsync(string eventId, string assignmentId);
    Task DeleteAllByEventAsync(string eventId);
    Task DeleteAllByLocationAsync(string eventId, string locationId);
}
