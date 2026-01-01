using System.Collections.Concurrent;

namespace VolunteerCheckin.Functions.Services;

/// <summary>
/// In-memory rate limiting service for authentication endpoints.
/// Tracks request counts by key (email, IP, etc.) with sliding windows.
///
/// Note: For multi-instance deployments, this should be replaced with
/// a distributed cache (Redis) or Azure Table Storage.
/// </summary>
public class RateLimitService
{
    private readonly ConcurrentDictionary<string, RateLimitEntry> _entries = new();
    private readonly Timer _cleanupTimer;

    public RateLimitService()
    {
        // Clean up expired entries every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Check if a request is allowed under the rate limit.
    /// Returns true if allowed, false if rate limited.
    /// </summary>
    /// <param name="key">Unique key for the rate limit (e.g., "email:user@example.com" or "ip:192.168.1.1")</param>
    /// <param name="maxRequests">Maximum number of requests allowed in the window</param>
    /// <param name="windowSeconds">Time window in seconds</param>
    public virtual bool IsAllowed(string key, int maxRequests, int windowSeconds)
    {
        DateTime now = DateTime.UtcNow;
        DateTime windowStart = now.AddSeconds(-windowSeconds);

        RateLimitEntry entry = _entries.GetOrAdd(key, _ => new RateLimitEntry());

        lock (entry)
        {
            // Remove requests outside the window
            while (entry.Requests.Count > 0 && entry.Requests.Peek() < windowStart)
            {
                entry.Requests.Dequeue();
            }

            // Check if we're at the limit
            if (entry.Requests.Count >= maxRequests)
            {
                return false;
            }

            // Add the current request
            entry.Requests.Enqueue(now);
            return true;
        }
    }

    /// <summary>
    /// Check rate limit for magic link requests (per email).
    /// </summary>
    public virtual bool IsAllowedMagicLinkRequest(string email)
    {
        string key = $"magic-link:{email.ToLowerInvariant()}";
        return IsAllowed(key, Constants.MaxMagicLinkRequestsPerEmailPerHour, 3600);
    }

    /// <summary>
    /// Check rate limit for marshal code attempts (per IP).
    /// </summary>
    public virtual bool IsAllowedMarshalCodeAttempt(string ipAddress)
    {
        string key = $"marshal-code-ip:{ipAddress}";
        return IsAllowed(key, Constants.MaxMarshalCodeAttemptsPerIpPerMinute, 60);
    }

    /// <summary>
    /// Check rate limit for marshal code attempts (per event, per hour).
    /// </summary>
    public virtual bool IsAllowedMarshalCodeAttemptForEvent(string eventId)
    {
        string key = $"marshal-code-event:{eventId}";
        return IsAllowed(key, Constants.MaxMarshalCodeAttemptsPerEventPerHour, 3600);
    }

    /// <summary>
    /// Get remaining requests for a key.
    /// </summary>
    public virtual int GetRemainingRequests(string key, int maxRequests, int windowSeconds)
    {
        if (!_entries.TryGetValue(key, out RateLimitEntry? entry))
        {
            return maxRequests;
        }

        DateTime windowStart = DateTime.UtcNow.AddSeconds(-windowSeconds);

        lock (entry)
        {
            // Count only requests within the window
            int count = entry.Requests.Count(r => r >= windowStart);
            return Math.Max(0, maxRequests - count);
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        DateTime cutoff = DateTime.UtcNow.AddHours(-1);
        List<string> keysToRemove = new();

        foreach (KeyValuePair<string, RateLimitEntry> kvp in _entries)
        {
            lock (kvp.Value)
            {
                // Remove entries where all requests are older than 1 hour
                if (kvp.Value.Requests.Count == 0 ||
                    kvp.Value.Requests.All(r => r < cutoff))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
        }

        foreach (string key in keysToRemove)
        {
            _entries.TryRemove(key, out _);
        }
    }

    private class RateLimitEntry
    {
        public Queue<DateTime> Requests { get; } = new();
    }
}
