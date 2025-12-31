using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IAuthTokenRepository
{
    Task<AuthTokenEntity> AddAsync(AuthTokenEntity token);
    Task<AuthTokenEntity?> GetAsync(string tokenId);
    Task<AuthTokenEntity?> GetByTokenHashAsync(string tokenHash);
    Task<IEnumerable<AuthTokenEntity>> GetByPersonAsync(string personId);
    Task UpdateAsync(AuthTokenEntity token);
    Task DeleteAsync(string tokenId);
    Task DeleteExpiredTokensAsync();
}
