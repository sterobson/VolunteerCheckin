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

    /// <summary>
    /// Get all sessions for a person. Uses OData filter for server-side filtering.
    /// Note: For high-volume scenarios, consider adding a secondary index table.
    /// </summary>
    public async Task<IEnumerable<AuthSessionEntity>> GetByPersonAsync(string personId)
    {
        // Use OData filter syntax for server-side filtering (more efficient than client-side)
        string filter = $"PartitionKey eq 'SESSION' and PersonId eq '{personId}'";
        List<AuthSessionEntity> sessions = [];
        await foreach (AuthSessionEntity session in _table.QueryAsync<AuthSessionEntity>(filter))
        {
            sessions.Add(session);
        }
        return sessions;
    }

    /// <summary>
    /// Get all sessions for a person in a specific event. Uses OData filter for server-side filtering.
    /// </summary>
    public async Task<IEnumerable<AuthSessionEntity>> GetByPersonAndEventAsync(string personId, string eventId)
    {
        // Use OData filter syntax for server-side filtering
        string filter = $"PartitionKey eq 'SESSION' and PersonId eq '{personId}' and EventId eq '{eventId}'";
        List<AuthSessionEntity> sessions = [];
        await foreach (AuthSessionEntity session in _table.QueryAsync<AuthSessionEntity>(filter))
        {
            sessions.Add(session);
        }
        return sessions;
    }

    public async Task UpdateAsync(AuthSessionEntity session)
    {
        await _table.UpdateEntityAsync(session, session.ETag);
    }

    public async Task UpdateUnconditionalAsync(AuthSessionEntity session)
    {
        // Use ETag.All to skip optimistic concurrency check
        // Useful for non-critical updates like LastAccessedAt
        await _table.UpdateEntityAsync(session, ETag.All, TableUpdateMode.Replace);
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
        DateTime revokedAt = DateTime.UtcNow;

        // Revoke all sessions in parallel
        List<Task> revokeTasks = [];
        foreach (AuthSessionEntity session in sessions)
        {
            if (!session.IsRevoked)
            {
                session.IsRevoked = true;
                session.RevokedAt = revokedAt;
                revokeTasks.Add(UpdateUnconditionalAsync(session));
            }
        }
        await Task.WhenAll(revokeTasks);
    }

    public async Task DeleteAllByPersonAsync(string personId)
    {
        IEnumerable<AuthSessionEntity> sessions = await GetByPersonAsync(personId);

        foreach (AuthSessionEntity session in sessions)
        {
            try
            {
                await _table.DeleteEntityAsync(session.PartitionKey, session.RowKey);
            }
            catch (RequestFailedException)
            {
                // Ignore delete failures
            }
        }
    }

    public async Task DeleteAllByEventAsync(string eventId)
    {
        // Query for all sessions associated with this event
        string filter = $"PartitionKey eq 'SESSION' and EventId eq '{eventId}'";
        List<AuthSessionEntity> sessions = [];
        await foreach (AuthSessionEntity session in _table.QueryAsync<AuthSessionEntity>(filter))
        {
            sessions.Add(session);
        }

        // Delete sessions in parallel
        List<Task> deleteTasks = [];
        foreach (AuthSessionEntity session in sessions)
        {
            deleteTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _table.DeleteEntityAsync(session.PartitionKey, session.RowKey);
                }
                catch (RequestFailedException)
                {
                    // Ignore delete failures (entity may have been deleted by another process)
                }
            }));
        }

        await Task.WhenAll(deleteTasks);
    }

    public async Task DeleteExpiredSessionsAsync()
    {
        DateTime now = DateTime.UtcNow;
        List<string> rowKeysToDelete = [];

        // Use OData filter to find revoked or expired sessions
        // This is a background cleanup task so a full scan is acceptable
        await foreach (AuthSessionEntity session in _table.QueryAsync<AuthSessionEntity>("PartitionKey eq 'SESSION'"))
        {
            // Delete sessions that are revoked and older than 24 hours
            // or sessions with expiry that have expired more than 7 days ago
            bool isOldRevokedSession = session.IsRevoked && session.RevokedAt.HasValue &&
                                       (now - session.RevokedAt.Value).TotalHours > 24;

            bool isExpiredSession = session.ExpiresAt.HasValue &&
                                   (now - session.ExpiresAt.Value).TotalDays > 7;

            if (isOldRevokedSession || isExpiredSession)
            {
                rowKeysToDelete.Add(session.RowKey);
            }
        }

        // Delete sessions in parallel for better performance
        List<Task> deleteTasks = [];
        foreach (string rowKey in rowKeysToDelete)
        {
            deleteTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _table.DeleteEntityAsync("SESSION", rowKey);
                }
                catch (RequestFailedException)
                {
                    // Ignore delete failures (entity may have been deleted by another process)
                }
            }));
        }

        await Task.WhenAll(deleteTasks);
    }
}
