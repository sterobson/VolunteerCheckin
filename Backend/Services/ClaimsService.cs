using System.Security.Cryptography;
using System.Text;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Services;

/// <summary>
/// Service for resolving user claims from session tokens.
/// Computes permissions per-request based on PersonId + EventId + AuthMethod.
/// </summary>
public class ClaimsService
{
    private readonly IAuthSessionRepository _sessionRepository;
    private readonly IPersonRepository _personRepository;
    private readonly IEventRoleRepository _roleRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly IUserEventMappingRepository _userEventMappingRepository;

    // Cache for legacy migration checks to avoid repeated database queries
    // Key: "{personId}:{eventId}", Value: true if migration was attempted
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _migrationCheckCache = new();

    public ClaimsService(
        IAuthSessionRepository sessionRepository,
        IPersonRepository personRepository,
        IEventRoleRepository roleRepository,
        IMarshalRepository marshalRepository,
        IUserEventMappingRepository userEventMappingRepository)
    {
        _sessionRepository = sessionRepository;
        _personRepository = personRepository;
        _roleRepository = roleRepository;
        _marshalRepository = marshalRepository;
        _userEventMappingRepository = userEventMappingRepository;
    }

    /// <summary>
    /// Resolve user claims from a session token for a specific event.
    /// </summary>
    /// <param name="sessionToken">The session token from cookie/header</param>
    /// <param name="eventId">The event being accessed (optional for cross-event admin)</param>
    /// <returns>UserClaims if valid, null if invalid/expired</returns>
    public virtual async Task<UserClaims?> GetClaimsAsync(string sessionToken, string? eventId = null)
    {
        // Hash the session token to look it up
        string tokenHash = HashToken(sessionToken);

        // Look up the session
        AuthSessionEntity? session = await _sessionRepository.GetBySessionTokenHashAsync(tokenHash);
        if (session == null || !session.IsValid())
        {
            return null;
        }

        // Update last accessed time (use unconditional update to avoid race conditions)
        session.LastAccessedAt = DateTime.UtcNow;
        await _sessionRepository.UpdateUnconditionalAsync(session);

        // Get the person
        PersonEntity? person = await _personRepository.GetAsync(session.PersonId);
        if (person == null)
        {
            return null;
        }

        // Determine the effective event ID
        // For marshal sessions, use the session's locked EventId
        // For admin sessions, use the provided eventId parameter
        string? effectiveEventId = session.EventId ?? eventId;

        // Get event roles if we have an event ID
        List<EventRoleInfo> eventRoles = [];
        if (effectiveEventId != null)
        {
            // Check new EventRoles table
            IEnumerable<EventRoleEntity> roles = await _roleRepository.GetByPersonAndEventAsync(person.PersonId, effectiveEventId);
            eventRoles = roles.Select(r => new EventRoleInfo(
                r.Role,
                System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.AreaIdsJson) ?? []
            )).ToList();

            // Migrate legacy UserEventMappings to new EventRoles table if needed
            // Use cache to avoid repeated database queries for the same person/event
            string migrationCacheKey = $"{person.PersonId}:{effectiveEventId}";
            if (!eventRoles.Any(r => r.Role == Constants.RoleEventAdmin) &&
                !_migrationCheckCache.ContainsKey(migrationCacheKey))
            {
                // Mark as checked first (even before the query) to prevent concurrent migration attempts
                _migrationCheckCache.TryAdd(migrationCacheKey, true);

                try
                {
                    UserEventMappingEntity? legacyMapping = await _userEventMappingRepository.GetAsync(effectiveEventId, person.Email);
                    if (legacyMapping != null && legacyMapping.Role == "Admin")
                    {
                        // Migrate to new EventRoles table
                        string roleId = Guid.NewGuid().ToString();
                        EventRoleEntity newRole = new EventRoleEntity
                        {
                            PartitionKey = person.PersonId,
                            RowKey = EventRoleEntity.CreateRowKey(effectiveEventId, roleId),
                            PersonId = person.PersonId,
                            EventId = effectiveEventId,
                            Role = Constants.RoleEventAdmin,
                            AreaIdsJson = "[]",
                            GrantedAt = DateTime.UtcNow,
                            GrantedByPersonId = "system-migration"
                        };
                        await _roleRepository.AddAsync(newRole);

                        eventRoles.Add(new EventRoleInfo(Constants.RoleEventAdmin, []));
                    }
                }
                catch
                {
                    // Ignore errors from legacy table - it may not exist
                }
            }
        }

        // Get marshal ID - prefer session's stored MarshalId (reliable for marshal sessions)
        // Fall back to lookup by PersonId for admin sessions viewing marshal data
        string? marshalId = session.MarshalId;
        if (marshalId == null && effectiveEventId != null)
        {
            IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(effectiveEventId);
            MarshalEntity? marshal = marshals.FirstOrDefault(m => m.PersonId == person.PersonId);
            marshalId = marshal?.MarshalId;
        }

        return new UserClaims(
            PersonId: person.PersonId,
            PersonName: person.Name,
            PersonEmail: person.Email,
            IsSystemAdmin: person.IsSystemAdmin,
            EventId: effectiveEventId,
            AuthMethod: session.AuthMethod,
            MarshalId: marshalId,
            EventRoles: eventRoles
        );
    }

    /// <summary>
    /// Create a new session for a person after they authenticate.
    /// </summary>
    /// <param name="personId">The person who authenticated</param>
    /// <param name="authMethod">How they authenticated</param>
    /// <param name="eventId">Event to lock session to (for marshal auth only)</param>
    /// <param name="ipAddress">IP address for audit trail</param>
    /// <param name="marshalId">Marshal ID for marshal sessions (optional)</param>
    /// <returns>The session token (unhashed) to return to the client</returns>
    public async Task<string> CreateSessionAsync(string personId, string authMethod, string? eventId, string ipAddress, string? marshalId = null)
    {
        // Generate a cryptographically secure random session token
        string sessionToken = GenerateSecureToken(64);
        string tokenHash = HashToken(sessionToken);

        DateTime now = DateTime.UtcNow;
        DateTime? expiresAt = null;

        // Set expiry based on auth method
        if (authMethod == Constants.AuthMethodSecureEmailLink)
        {
            expiresAt = now.AddHours(Constants.AdminSessionExpiryHours);
        }
        // Marshal sessions (MagicCode) never expire (expiresAt stays null)

        string sessionId = Guid.NewGuid().ToString();
        AuthSessionEntity session = new AuthSessionEntity
        {
            SessionId = sessionId,
            PartitionKey = "SESSION",
            RowKey = tokenHash,
            SessionTokenHash = tokenHash,
            PersonId = personId,
            EventId = eventId,
            MarshalId = marshalId,
            AuthMethod = authMethod,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            LastAccessedAt = now,
            IpAddress = ipAddress
        };

        await _sessionRepository.AddAsync(session);

        return sessionToken;
    }

    /// <summary>
    /// Revoke a session (logout).
    /// </summary>
    public async Task RevokeSessionAsync(string sessionToken)
    {
        string tokenHash = HashToken(sessionToken);
        AuthSessionEntity? session = await _sessionRepository.GetBySessionTokenHashAsync(tokenHash);
        if (session != null)
        {
            await _sessionRepository.RevokeAsync(session.SessionTokenHash);
        }
    }

    /// <summary>
    /// Revoke all sessions for a person (force logout everywhere).
    /// </summary>
    public async Task RevokeAllSessionsForPersonAsync(string personId)
    {
        await _sessionRepository.RevokeAllForPersonAsync(personId);
    }

    /// <summary>
    /// Hash a token using SHA256.
    /// Uses URL-safe Base64 encoding to avoid Azure Table Storage RowKey invalid characters.
    /// </summary>
    private static string HashToken(string token)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }

    /// <summary>
    /// Generate a cryptographically secure random token.
    /// </summary>
    private static string GenerateSecureToken(int length)
    {
        byte[] bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
