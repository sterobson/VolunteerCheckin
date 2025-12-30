using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IChecklistCompletionRepository
{
    Task<ChecklistCompletionEntity> AddAsync(ChecklistCompletionEntity completion);
    Task<ChecklistCompletionEntity?> GetAsync(string eventId, string itemId, string completionId);
    Task<IEnumerable<ChecklistCompletionEntity>> GetByEventAsync(string eventId);
    Task<IEnumerable<ChecklistCompletionEntity>> GetByItemAsync(string eventId, string itemId);
    Task<IEnumerable<ChecklistCompletionEntity>> GetByMarshalAsync(string eventId, string marshalId);
    Task UpdateAsync(ChecklistCompletionEntity completion);
    Task DeleteAsync(string eventId, string itemId, string completionId);
    Task DeleteAllByEventAsync(string eventId);
    Task DeleteAllByItemAsync(string eventId, string itemId);
}
