using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface ILayerRepository
{
    Task<LayerEntity> AddAsync(LayerEntity layer);
    Task<LayerEntity?> GetAsync(string eventId, string layerId);
    Task<IEnumerable<LayerEntity>> GetByEventAsync(string eventId);
    Task UpdateAsync(LayerEntity layer);
    Task DeleteAsync(string eventId, string layerId);
    Task DeleteAllByEventAsync(string eventId);
}
