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
        // Ensure RowKey is set to SessionTokenHash for O(1) lookup
        if (string.IsNullOrEmpty(session.RowKey))
        {
            session.RowKey = session.SessionTokenHash;
        }
        await _table.AddEntityAsync(session);
        return session;
    }

    /// <summary>
    /// Get session by token hash using O(1) direct lookup.
    /// SessionTokenHash is used as the RowKey for efficient retrieval.
    /// </summary>
    public async Task<AuthSessionEntity?> GetBySessionTokenHashAsync(string sessionTokenHash)
    {
        try
        {
            Response<AuthSessionEntity> response = await _table.GetEntityAsync<AuthSessionEntity>("SESSION", sessionTokenHash);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
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

    public async Task DeleteAsync(string sessionTokenHash)
    {
        await _table.DeleteEntityAsync("SESSION", sessionTokenHash);
    }

    public async Task RevokeAsync(string sessionTokenHash)
    {
        AuthSessionEntity? session = await GetBySessionTokenHashAsync(sessionTokenHash);
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

    public async Task DeleteExpiredSessionsAsync()
    {
        DateTime now = DateTime.UtcNow;
        List<AuthSessionEntity> sessionsToDelete = [];

        await foreach (AuthSessionEntity session in _table.QueryAsync<AuthSessionEntity>(s => s.PartitionKey == "SESSION"))
        {
            // Delete sessions that are revoked and older than 24 hours
            // or sessions with expiry that have expired more than 7 days ago
            bool isOldRevokedSession = session.IsRevoked && session.RevokedAt.HasValue &&
                                       (now - session.RevokedAt.Value).TotalHours > 24;

            bool isExpiredSession = session.ExpiresAt.HasValue &&
                                   (now - session.ExpiresAt.Value).TotalDays > 7;

            if (isOldRevokedSession || isExpiredSession)
            {
                sessionsToDelete.Add(session);
            }
        }

        foreach (AuthSessionEntity session in sessionsToDelete)
        {
            try
            {
                await _table.DeleteEntityAsync("SESSION", session.RowKey);
            }
            catch (RequestFailedException)
            {
                // Ignore delete failures (entity may have been deleted by another process)
            }
        }
    }
}
