using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IChecklistItemRepository
{
    Task<ChecklistItemEntity> AddAsync(ChecklistItemEntity item);
    Task<ChecklistItemEntity?> GetAsync(string eventId, string itemId);
    Task<IEnumerable<ChecklistItemEntity>> GetByEventAsync(string eventId);
    Task UpdateAsync(ChecklistItemEntity item);
    Task DeleteAsync(string eventId, string itemId);
    Task DeleteAllByEventAsync(string eventId);
}
