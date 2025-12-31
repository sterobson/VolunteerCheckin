using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStorageAuthSessionRepository : IAuthSessionRepository
{
    private readonly TableClient _table;

    public TableStorageAuthSessionRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetAuthSessionsTable();
    }

    public async Task<AuthSessionEntity> AddAsync(AuthSessionEntity session)
    {
        await _table.AddEntityAsync(session);
        return session;
    }

    public async Task<AuthSessionEntity?> GetAsync(string sessionId)
    {
        try
        {
            Response<AuthSessionEntity> response = await _table.GetEntityAsync<AuthSessionEntity>("SESSION", sessionId);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<AuthSessionEntity?> GetBySessionTokenHashAsync(string sessionTokenHash)
    {
        await foreach (AuthSessionEntity session in _table.QueryAsync<AuthSessionEntity>(s => s.PartitionKey == "SESSION"))
        {
            if (session.SessionTokenHash == sessionTokenHash)
            {
                return session;
            }
        }
        return null;
    }

    public async Task<IEnumerable<AuthSessionEntity>> GetByPersonAsync(string personId)
    {
        List<AuthSessionEntity> sessions = [];
        await foreach (AuthSessionEntity session in _table.QueryAsync<AuthSessionEntity>(s => s.PartitionKey == "SESSION"))
        {
            if (session.PersonId == personId)
            {
                sessions.Add(session);
            }
        }
        return sessions;
    }

    public async Task<IEnumerable<AuthSessionEntity>> GetByPersonAndEventAsync(string personId, string eventId)
    {
        List<AuthSessionEntity> sessions = [];
        await foreach (AuthSessionEntity session in _table.QueryAsync<AuthSessionEntity>(s => s.PartitionKey == "SESSION"))
        {
            if (session.PersonId == personId && session.EventId == eventId)
            {
                sessions.Add(session);
            }
        }
        return sessions;
    }

    public async Task UpdateAsync(AuthSessionEntity session)
    {
        await _table.UpdateEntityAsync(session, session.ETag);
    }

    public async Task DeleteAsync(string sessionId)
    {
        await _table.DeleteEntityAsync("SESSION", sessionId);
    }

    public async Task RevokeAsync(string sessionId)
    {
        AuthSessionEntity? session = await GetAsync(sessionId);
        if (session != null)
        {
            session.IsRevoked = true;
            session.RevokedAt = DateTime.UtcNow;
            await UpdateAsync(session);
        }
    }

    public async Task RevokeAllForPersonAsync(string personId)
    {
        IEnumerable<AuthSessionEntity> sessions = await GetByPersonAsync(personId);
        foreach (AuthSessionEntity session in sessions)
        {
            if (!session.IsRevoked)
            {
                session.IsRevoked = true;
                session.RevokedAt = DateTime.UtcNow;
                await UpdateAsync(session);
            }
        }
    }
}
