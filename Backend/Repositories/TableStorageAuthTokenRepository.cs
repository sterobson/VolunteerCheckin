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
        await _table.AddEntityAsync(token);
        return token;
    }

    public async Task<AuthTokenEntity?> GetAsync(string tokenId)
    {
        try
        {
            Response<AuthTokenEntity> response = await _table.GetEntityAsync<AuthTokenEntity>("AUTHTOKEN", tokenId);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<AuthTokenEntity?> GetByTokenHashAsync(string tokenHash)
    {
        await foreach (AuthTokenEntity token in _table.QueryAsync<AuthTokenEntity>(t => t.PartitionKey == "AUTHTOKEN"))
        {
            if (token.TokenHash == tokenHash)
            {
                return token;
            }
        }
        return null;
    }

    public async Task<IEnumerable<AuthTokenEntity>> GetByPersonAsync(string personId)
    {
        List<AuthTokenEntity> tokens = [];
        await foreach (AuthTokenEntity token in _table.QueryAsync<AuthTokenEntity>(t => t.PartitionKey == "AUTHTOKEN"))
        {
            if (token.PersonId == personId)
            {
                tokens.Add(token);
            }
        }
        return tokens;
    }

    public async Task UpdateAsync(AuthTokenEntity token)
    {
        await _table.UpdateEntityAsync(token, token.ETag);
    }

    public async Task DeleteAsync(string tokenId)
    {
        await _table.DeleteEntityAsync("AUTHTOKEN", tokenId);
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
