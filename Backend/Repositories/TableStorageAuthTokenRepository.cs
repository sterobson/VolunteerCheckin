using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageAuthTokenRepository : IAuthTokenRepository
{
    private readonly TableClient _table;

    public TableStorageAuthTokenRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetAuthTokensTable();
    }

    public async Task<AuthTokenEntity> AddAsync(AuthTokenEntity token)
    {
        // Ensure RowKey is set to TokenHash for O(1) lookup
        if (string.IsNullOrEmpty(token.RowKey))
        {
            token.RowKey = token.TokenHash;
        }
        await _table.AddEntityAsync(token);
        return token;
    }

    /// <summary>
    /// Get token by hash using O(1) direct lookup.
    /// TokenHash is used as the RowKey for efficient retrieval.
    /// </summary>
    public async Task<AuthTokenEntity?> GetByTokenHashAsync(string tokenHash)
    {
        try
        {
            Response<AuthTokenEntity> response = await _table.GetEntityAsync<AuthTokenEntity>("AUTHTOKEN", tokenHash);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<AuthTokenEntity>> GetByPersonAsync(string personId)
    {
        List<AuthTokenEntity> tokens = [];
        await foreach (AuthTokenEntity token in _table.QueryAsync<AuthTokenEntity>(t => t.PartitionKey == "AUTHTOKEN"))
        {
            if (string.Equals(token.PersonId, personId, StringComparison.Ordinal))
            {
                tokens.Add(token);
            }
        }
        return tokens;
    }

    public async Task<AuthTokenEntity?> GetValidTokenByPersonAndCodeAsync(string personId, string loginCode)
    {
        DateTime now = DateTime.UtcNow;
        await foreach (AuthTokenEntity token in _table.QueryAsync<AuthTokenEntity>(t => t.PartitionKey == "AUTHTOKEN"))
        {
            if (string.Equals(token.PersonId, personId, StringComparison.Ordinal) &&
                string.Equals(token.LoginCode, loginCode, StringComparison.Ordinal) &&
                token.UsedAt == null &&
                token.ExpiresAt > now)
            {
                return token;
            }
        }
        return null;
    }

    public async Task UpdateAsync(AuthTokenEntity token)
    {
        await _table.UpdateEntityAsync(token, token.ETag);
    }

    public async Task DeleteAsync(string tokenHash)
    {
        await _table.DeleteEntityAsync("AUTHTOKEN", tokenHash);
    }

    public async Task DeleteExpiredTokensAsync()
    {
        DateTime now = DateTime.UtcNow;
        List<AuthTokenEntity> expiredTokens = [];

        await foreach (AuthTokenEntity token in _table.QueryAsync<AuthTokenEntity>(t => t.PartitionKey == "AUTHTOKEN"))
        {
            if (token.ExpiresAt < now)
            {
                expiredTokens.Add(token);
            }
        }

        foreach (AuthTokenEntity token in expiredTokens)
        {
            await _table.DeleteEntityAsync(token.PartitionKey, token.RowKey);
        }
    }
}
