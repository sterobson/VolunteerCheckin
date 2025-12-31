using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IAuthSessionRepository
{
    Task<AuthSessionEntity> AddAsync(AuthSessionEntity session);
    Task<AuthSessionEntity?> GetAsync(string sessionId);
    Task<AuthSessionEntity?> GetBySessionTokenHashAsync(string sessionTokenHash);
    Task<IEnumerable<AuthSessionEntity>> GetByPersonAsync(string personId);
    Task<IEnumerable<AuthSessionEntity>> GetByPersonAndEventAsync(string personId, string eventId);
    Task UpdateAsync(AuthSessionEntity session);
    Task DeleteAsync(string sessionId);
    Task RevokeAsync(string sessionId);
    Task RevokeAllForPersonAsync(string personId);
}
