using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

public interface IAuthTokenRepository
{
    Task<AuthTokenEntity> AddAsync(AuthTokenEntity token);
    /// <summary>
    /// Get token by its hash (O(1) lookup).
    /// The tokenHash is used as the RowKey for efficient lookups.
    /// </summary>
    Task<AuthTokenEntity?> GetByTokenHashAsync(string tokenHash);
    Task<IEnumerable<AuthTokenEntity>> GetByPersonAsync(string personId);
    /// <summary>
    /// Get a valid (unused, not expired) token for a person with matching login code.
    /// </summary>
    Task<AuthTokenEntity?> GetValidTokenByPersonAndCodeAsync(string personId, string loginCode);
    Task UpdateAsync(AuthTokenEntity token);
    Task DeleteAsync(string tokenHash);
    Task DeleteAllByPersonAsync(string personId);
    Task DeleteExpiredTokensAsync();
}
