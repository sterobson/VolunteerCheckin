using System.Security.Cryptography;
using System.Text;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

using Microsoft.Extensions.Logging;

namespace VolunteerCheckin.Functions.Services;

/// <summary>
/// Service for handling authentication (magic links and marshal codes).
/// </summary>
public class AuthService
{
    private readonly IAuthTokenRepository _tokenRepository;
    private readonly IPersonRepository _personRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly ClaimsService _claimsService;
    private readonly EmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAuthTokenRepository tokenRepository,
        IPersonRepository personRepository,
        IMarshalRepository marshalRepository,
        ClaimsService claimsService,
        EmailService emailService,
        ILogger<AuthService> logger)
    {
        _tokenRepository = tokenRepository;
        _personRepository = personRepository;
        _marshalRepository = marshalRepository;
        _claimsService = claimsService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Request a magic link to be sent to an email address.
    /// Creates or updates the person record and sends them a login link.
    /// </summary>
    public async Task<bool> RequestMagicLinkAsync(string email, string ipAddress, string baseUrl)
    {
        // Validate email
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return false;
        }

        email = email.Trim().ToLowerInvariant();

        // Get or create person
        PersonEntity? person = await _personRepository.GetByEmailAsync(email);
        if (person == null)
        {
            // Create new person
            string personId = Guid.NewGuid().ToString();
            person = new PersonEntity
            {
                PersonId = personId,
                PartitionKey = "PERSON",
                RowKey = personId,
                Email = email,
                Name = string.Empty, // They'll fill this in later
                Phone = string.Empty,
                IsSystemAdmin = false,
                CreatedAt = DateTime.UtcNow
            };
            await _personRepository.AddAsync(person);
        }

        // Generate token
        string token = GenerateSecureToken(32);
        string tokenHash = HashToken(token);
        _logger.LogInformation($"Generated token (length {token.Length}), hash: {tokenHash.Substring(0, Math.Min(10, tokenHash.Length))}...");

        // Create auth token
        string tokenId = Guid.NewGuid().ToString();
        AuthTokenEntity authToken = new AuthTokenEntity
        {
            TokenId = tokenId,
            PartitionKey = "AUTHTOKEN",
            RowKey = tokenHash,  // Must match TokenHash for O(1) lookup
            TokenHash = tokenHash,
            PersonId = person.PersonId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(Constants.MagicLinkExpiryMinutes),
            RequestIpAddress = ipAddress
        };

        await _tokenRepository.AddAsync(authToken);

        // Send email with magic link (pointing to frontend)
        string magicLink = $"{baseUrl}/#/admin/verify?token={token}";
        await _emailService.SendMagicLinkEmailAsync(email, magicLink);

        return true;
    }

    /// <summary>
    /// Verify a magic link token and create a session.
    /// </summary>
    public async Task<(bool Success, string? SessionToken, PersonInfo? Person, string? Message)> VerifyMagicLinkAsync(string token, string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("VerifyMagicLinkAsync called with null or empty token");
            return (false, null, null, "Invalid or expired token");
        }

        _logger.LogInformation($"VerifyMagicLinkAsync called with token length: {token.Length}");

        // Hash the token to look it up
        string tokenHash = HashToken(token);
        _logger.LogInformation($"Token hash computed: {tokenHash.Substring(0, Math.Min(10, tokenHash.Length))}...");

        // Find the token
        AuthTokenEntity? authToken = await _tokenRepository.GetByTokenHashAsync(tokenHash);
        if (authToken == null)
        {
            _logger.LogWarning("Token not found in repository");
            return (false, null, null, "Invalid or expired token");
        }

        if (!authToken.IsValid())
        {
            _logger.LogWarning($"Token is not valid. UsedAt: {authToken.UsedAt}, ExpiresAt: {authToken.ExpiresAt}, Now: {DateTime.UtcNow}");
            return (false, null, null, "Invalid or expired token");
        }

        // Mark token as used
        authToken.UsedAt = DateTime.UtcNow;
        authToken.UseIpAddress = ipAddress;
        await _tokenRepository.UpdateAsync(authToken);

        // Get the person
        PersonEntity? person = await _personRepository.GetAsync(authToken.PersonId);
        if (person == null)
        {
            return (false, null, null, "Person not found");
        }

        // Create session (cross-event, no EventId lock)
        string sessionToken = await _claimsService.CreateSessionAsync(
            person.PersonId,
            Constants.AuthMethodSecureEmailLink,
            eventId: null,
            ipAddress
        );

        PersonInfo personInfo = new PersonInfo(
            person.PersonId,
            person.Name,
            person.Email,
            person.Phone,
            person.IsSystemAdmin
        );

        return (true, sessionToken, personInfo, "Login successful");
    }

    /// <summary>
    /// Authenticate a marshal using their magic code.
    /// </summary>
    public async Task<(bool Success, string? SessionToken, PersonInfo? Person, string? MarshalId, string? Message)> AuthenticateWithMagicCodeAsync(
        string eventId,
        string magicCode,
        string ipAddress)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(magicCode))
        {
            return (false, null, null, null, "Event ID and magic code are required");
        }

        magicCode = magicCode.Trim().ToUpperInvariant();

        // Find marshal by magic code
        IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
        MarshalEntity? marshal = marshals.FirstOrDefault(m => m.MagicCode == magicCode);

        if (marshal == null)
        {
            return (false, null, null, null, "Invalid magic code");
        }

        // Handle legacy marshals with empty PersonId - generate a new one
        string personId = marshal.PersonId;
        if (string.IsNullOrWhiteSpace(personId))
        {
            personId = Guid.NewGuid().ToString();
            marshal.PersonId = personId;
            await _marshalRepository.UpdateAsync(marshal);
            _logger.LogInformation($"Generated new PersonId {personId} for legacy marshal {marshal.MarshalId}");
        }

        // Get or create person for this marshal
        PersonEntity? person = await _personRepository.GetAsync(personId);
        if (person == null)
        {
            // Create person from marshal details
            person = new PersonEntity
            {
                PersonId = personId,
                PartitionKey = "PERSON",
                RowKey = personId,
                Email = marshal.Email,
                Name = marshal.Name,
                Phone = marshal.PhoneNumber,
                IsSystemAdmin = false,
                CreatedAt = DateTime.UtcNow
            };
            await _personRepository.AddAsync(person);
        }

        // Create session locked to this event, storing marshalId directly for reliable lookup
        string sessionToken = await _claimsService.CreateSessionAsync(
            person.PersonId,
            Constants.AuthMethodMarshalMagicCode,
            eventId: eventId,
            ipAddress,
            marshalId: marshal.MarshalId
        );

        PersonInfo personInfo = new PersonInfo(
            person.PersonId,
            person.Name,
            person.Email,
            person.Phone,
            person.IsSystemAdmin
        );

        return (true, sessionToken, personInfo, marshal.MarshalId, "Login successful");
    }

    /// <summary>
    /// Development-only instant login (bypasses email verification).
    /// Creates or gets a person and directly creates a session.
    /// WARNING: Only use in development! No email verification!
    /// </summary>
    public async Task<(bool Success, string? SessionToken, PersonInfo? Person)> InstantLoginAsync(string email, string ipAddress)
    {
        // Validate email
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return (false, null, null);
        }

        email = email.Trim().ToLowerInvariant();

        // Get or create person
        PersonEntity? person = await _personRepository.GetByEmailAsync(email);
        if (person == null)
        {
            // Create new person
            string personId = Guid.NewGuid().ToString();
            person = new PersonEntity
            {
                PersonId = personId,
                PartitionKey = "PERSON",
                RowKey = personId,
                Email = email,
                Name = string.Empty,
                Phone = string.Empty,
                IsSystemAdmin = false,
                CreatedAt = DateTime.UtcNow
            };
            await _personRepository.AddAsync(person);
        }

        // Create session directly (no email verification)
        string sessionToken = await _claimsService.CreateSessionAsync(
            person.PersonId,
            Constants.AuthMethodSecureEmailLink,
            eventId: null, // Cross-event session
            ipAddress
        );

        PersonInfo personInfo = new PersonInfo(
            person.PersonId,
            person.Name,
            person.Email,
            person.Phone,
            person.IsSystemAdmin
        );

        return (true, sessionToken, personInfo);
    }

    /// <summary>
    /// Generate a random magic code for a marshal (6 characters, A-Z and 0-9).
    /// </summary>
    public static string GenerateMagicCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        byte[] randomBytes = new byte[Constants.MagicCodeLength];
        RandomNumberGenerator.Fill(randomBytes);

        char[] code = new char[Constants.MagicCodeLength];
        for (int i = 0; i < Constants.MagicCodeLength; i++)
        {
            code[i] = chars[randomBytes[i] % chars.Length];
        }

        return new string(code);
    }

    /// <summary>
    /// Hash a token using SHA256.
    /// </summary>
    private static string HashToken(string token)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        // Use URL-safe Base64 encoding to avoid Azure Table Storage RowKey invalid characters
        // Standard Base64 uses +/= which are invalid for RowKey
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }

    /// <summary>
    /// Generate a cryptographically secure random token (URL-safe).
    /// Uses Base64Url encoding to avoid issues with URL special characters.
    /// </summary>
    private static string GenerateSecureToken(int length)
    {
        byte[] bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        // Use URL-safe base64 encoding (replace + with -, / with _, and remove padding =)
        string base64 = Convert.ToBase64String(bytes);
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
