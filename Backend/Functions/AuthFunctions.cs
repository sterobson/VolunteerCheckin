using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Helpers;

namespace VolunteerCheckin.Functions.Functions;

public class AuthFunctions
{
    private readonly ILogger<AuthFunctions> _logger;
    private readonly AuthService _authService;
    private readonly ClaimsService _claimsService;
    private readonly IPersonRepository _personRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly RateLimitService _rateLimitService;

    public AuthFunctions(
        ILogger<AuthFunctions> logger,
        AuthService authService,
        ClaimsService claimsService,
        IPersonRepository personRepository,
        IMarshalRepository marshalRepository,
        RateLimitService rateLimitService)
    {
        _logger = logger;
        _authService = authService;
        _claimsService = claimsService;
        _personRepository = personRepository;
        _marshalRepository = marshalRepository;
        _rateLimitService = rateLimitService;
    }

    /// <summary>
    /// Request a magic link to be sent to an email address.
    /// POST /api/auth/request-login
    /// Body: { "Email": "user@example.com" }
    /// </summary>
    [Function("RequestLogin")]
    public async Task<IActionResult> RequestLogin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/request-login")] HttpRequest req)
    {
        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            RequestLoginRequest? request = JsonSerializer.Deserialize<RequestLoginRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return new BadRequestObjectResult(new RequestLoginResponse(
                    Success: false,
                    Message: "Email is required"
                ));
            }

            // Rate limit check
            if (!_rateLimitService.IsAllowedMagicLinkRequest(request.Email))
            {
                _logger.LogWarning($"Rate limit exceeded for magic link request: {request.Email}");
                return new ObjectResult(new RequestLoginResponse(
                    Success: false,
                    Message: "Too many login requests. Please try again later."
                ))
                {
                    StatusCode = 429 // Too Many Requests
                };
            }

            // Get IP address for audit trail
            string ipAddress = req.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Get frontend URL for constructing the magic link
            string frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:5174";

            bool success = await _authService.RequestMagicLinkAsync(request.Email, ipAddress, frontendUrl);

            if (success)
            {
                return new OkObjectResult(new RequestLoginResponse(
                    Success: true,
                    Message: "Magic link sent to your email"
                ));
            }
            else
            {
                return new BadRequestObjectResult(new RequestLoginResponse(
                    Success: false,
                    Message: "Invalid email address"
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting magic link");
            return new ObjectResult(new RequestLoginResponse(
                Success: false,
                Message: "An error occurred"
            ))
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Verify a magic link token and create a session.
    /// POST /api/auth/verify-token
    /// Body: { "Token": "..." }
    /// Returns: { "Success": true, "SessionToken": "...", "Person": {...} }
    /// </summary>
    [Function("VerifyToken")]
    public async Task<IActionResult> VerifyToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/verify-token")] HttpRequest req)
    {
        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            VerifyTokenRequest? request = JsonSerializer.Deserialize<VerifyTokenRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrWhiteSpace(request.Token))
            {
                return new BadRequestObjectResult(new VerifyTokenResponse(
                    Success: false,
                    SessionToken: null,
                    Person: null,
                    Message: "Token is required"
                ));
            }

            // Get IP address for audit trail
            string ipAddress = req.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            (bool success, string? sessionToken, PersonInfo? person, string? message) =
                await _authService.VerifyMagicLinkAsync(request.Token, ipAddress);

            if (success && sessionToken != null)
            {
                // Set session cookie
                req.HttpContext.Response.Cookies.Append("session", sessionToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    MaxAge = TimeSpan.FromHours(Constants.AdminSessionExpiryHours)
                });

                return new OkObjectResult(new VerifyTokenResponse(
                    Success: true,
                    SessionToken: sessionToken,
                    Person: person,
                    Message: message
                ));
            }
            else
            {
                return new BadRequestObjectResult(new VerifyTokenResponse(
                    Success: false,
                    SessionToken: null,
                    Person: null,
                    Message: message
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying token");
            return new ObjectResult(new VerifyTokenResponse(
                Success: false,
                SessionToken: null,
                Person: null,
                Message: "An error occurred"
            ))
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Authenticate as a marshal using a magic code.
    /// POST /api/auth/marshal-login
    /// Body: { "EventId": "...", "MagicCode": "ABC123" }
    /// Returns: { "Success": true, "SessionToken": "...", "Person": {...}, "MarshalId": "..." }
    /// </summary>
    [Function("MarshalLogin")]
    public async Task<IActionResult> MarshalLogin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/marshal-login")] HttpRequest req)
    {
        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            MarshalLoginRequest? request = JsonSerializer.Deserialize<MarshalLoginRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrWhiteSpace(request.EventId) || string.IsNullOrWhiteSpace(request.MagicCode))
            {
                return new BadRequestObjectResult(new MarshalLoginResponse(
                    Success: false,
                    SessionToken: null,
                    Person: null,
                    MarshalId: null,
                    Message: "Event ID and magic code are required"
                ));
            }

            // Get IP address for audit trail
            string ipAddress = req.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Rate limit check - per IP
            if (!_rateLimitService.IsAllowedMarshalCodeAttempt(ipAddress))
            {
                _logger.LogWarning($"Rate limit exceeded for marshal code attempt from IP: {ipAddress}");
                return new ObjectResult(new MarshalLoginResponse(
                    Success: false,
                    SessionToken: null,
                    Person: null,
                    MarshalId: null,
                    Message: "Too many login attempts. Please try again in a minute."
                ))
                {
                    StatusCode = 429
                };
            }

            // Rate limit check - per event
            if (!_rateLimitService.IsAllowedMarshalCodeAttemptForEvent(request.EventId))
            {
                _logger.LogWarning($"Rate limit exceeded for marshal code attempts for event: {request.EventId}");
                return new ObjectResult(new MarshalLoginResponse(
                    Success: false,
                    SessionToken: null,
                    Person: null,
                    MarshalId: null,
                    Message: "Too many login attempts for this event. Please try again later."
                ))
                {
                    StatusCode = 429
                };
            }

            (bool success, string? sessionToken, PersonInfo? person, string? marshalId, string? message) =
                await _authService.AuthenticateWithMagicCodeAsync(request.EventId, request.MagicCode, ipAddress);

            if (success && sessionToken != null)
            {
                // Set session cookie (no expiry for marshal sessions)
                req.HttpContext.Response.Cookies.Append("session", sessionToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict
                    // No MaxAge - marshal sessions don't expire
                });

                return new OkObjectResult(new MarshalLoginResponse(
                    Success: true,
                    SessionToken: sessionToken,
                    Person: person,
                    MarshalId: marshalId,
                    Message: message
                ));
            }
            else
            {
                return new BadRequestObjectResult(new MarshalLoginResponse(
                    Success: false,
                    SessionToken: null,
                    Person: null,
                    MarshalId: null,
                    Message: message
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error with marshal login");
            return new ObjectResult(new MarshalLoginResponse(
                Success: false,
                SessionToken: null,
                Person: null,
                MarshalId: null,
                Message: "An error occurred"
            ))
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Logout (revoke session).
    /// POST /api/auth/logout
    /// </summary>
    [Function("Logout")]
    public async Task<IActionResult> Logout(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/logout")] HttpRequest req)
    {
        try
        {
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            if (!string.IsNullOrWhiteSpace(sessionToken))
            {
                await _claimsService.RevokeSessionAsync(sessionToken);
            }

            // Clear session cookie
            req.HttpContext.Response.Cookies.Delete("session");

            return new OkObjectResult(new { Success = true, Message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error with logout");
            return new ObjectResult(new { Success = false, Message = "An error occurred" })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Get current user's claims for a specific event.
    /// GET /api/auth/me?eventId={eventId}
    /// </summary>
    [Function("GetMe")]
    public async Task<IActionResult> GetMe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/me")] HttpRequest req)
    {
        try
        {
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { Message = "Not authenticated" });
            }

            // Get event ID from query string (optional for cross-event admin)
            string? eventId = req.Query["eventId"].FirstOrDefault();

            // Get claims
            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);

            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { Message = "Invalid or expired session" });
            }

            // Update LastAccessedDate for marshal sessions
            if (!string.IsNullOrEmpty(claims.MarshalId) && !string.IsNullOrEmpty(claims.EventId))
            {
                MarshalEntity? marshal = await _marshalRepository.GetAsync(claims.EventId, claims.MarshalId);
                if (marshal != null)
                {
                    marshal.LastAccessedDate = DateTime.UtcNow;
                    await _marshalRepository.UpdateAsync(marshal);
                }
            }

            return new OkObjectResult(claims);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user claims");
            return new ObjectResult(new { Message = "An error occurred" })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Development-only instant login (bypasses email verification).
    /// Creates/gets person and directly creates session.
    /// POST /api/auth/instant-login
    /// Body: { "Email": "user@example.com" }
    /// WARNING: Only use in development! No email verification!
    /// </summary>
    [Function("InstantLogin")]
    public async Task<IActionResult> InstantLogin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/instant-login")] HttpRequest req)
    {
        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            InstantLoginRequest? request = JsonSerializer.Deserialize<InstantLoginRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return new BadRequestObjectResult(new InstantLoginResponse(
                    Success: false,
                    Email: string.Empty,
                    Message: "Email is required"
                ));
            }

            // Get IP address for audit trail
            string ipAddress = req.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Normalize email
            string email = request.Email.Trim().ToLowerInvariant();

            // Create session directly (bypass email verification)
            (bool success, string? sessionToken, PersonInfo? _) =
                await _authService.InstantLoginAsync(email, ipAddress);

            if (success && sessionToken != null)
            {
                // Set session cookie
                req.HttpContext.Response.Cookies.Append("session", sessionToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    MaxAge = TimeSpan.FromHours(Constants.AdminSessionExpiryHours)
                });

                return new OkObjectResult(new InstantLoginResponse(
                    Success: true,
                    Email: email,
                    Message: "Login successful"
                ));
            }
            else
            {
                return new BadRequestObjectResult(new InstantLoginResponse(
                    Success: false,
                    Email: email,
                    Message: "Login failed"
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error with instant login");
            return new ObjectResult(new InstantLoginResponse(
                Success: false,
                Email: string.Empty,
                Message: "An error occurred"
            ))
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Get current user's profile.
    /// GET /api/auth/profile
    /// </summary>
    [Function("GetProfile")]
    public async Task<IActionResult> GetProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/profile")] HttpRequest req)
    {
        try
        {
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { Message = "Not authenticated" });
            }

            // Get claims
            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken);

            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { Message = "Invalid or expired session" });
            }

            // Get full person details
            PersonEntity? person = await _personRepository.GetAsync(claims.PersonId);
            if (person == null)
            {
                return new NotFoundObjectResult(new { Message = "Person not found" });
            }

            PersonInfo personInfo = new PersonInfo(
                person.PersonId,
                person.Name,
                person.Email,
                person.Phone,
                person.IsSystemAdmin
            );

            return new OkObjectResult(personInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile");
            return new ObjectResult(new { Message = "An error occurred" })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Update current user's profile (name and phone).
    /// PUT /api/auth/profile
    /// Body: { "Name": "John Doe", "Phone": "555-1234" }
    /// </summary>
    [Function("UpdateProfile")]
    public async Task<IActionResult> UpdateProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "auth/profile")] HttpRequest req)
    {
        try
        {
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { Message = "Not authenticated" });
            }

            // Get claims
            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken);

            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { Message = "Invalid or expired session" });
            }

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            UpdateProfileRequest? request = JsonSerializer.Deserialize<UpdateProfileRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null)
            {
                return new BadRequestObjectResult(new { Message = "Invalid request body" });
            }

            // Validate name
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new BadRequestObjectResult(new { Message = "Name is required" });
            }

            // Validate email
            if (string.IsNullOrWhiteSpace(request.Email) || !Validators.IsValidEmail(request.Email))
            {
                return new BadRequestObjectResult(new { Message = "Valid email is required" });
            }

            // Get person
            PersonEntity? person = await _personRepository.GetAsync(claims.PersonId);
            if (person == null)
            {
                return new NotFoundObjectResult(new { Message = "Person not found" });
            }

            // Check if email is changing and if new email already exists
            string newEmail = request.Email.Trim().ToLowerInvariant();
            if (newEmail != person.Email)
            {
                PersonEntity? existingPerson = await _personRepository.GetByEmailAsync(newEmail);
                if (existingPerson != null && existingPerson.PersonId != claims.PersonId)
                {
                    return new BadRequestObjectResult(new { Message = "Email address is already in use by another person" });
                }
            }

            // Update person
            person.Name = request.Name.Trim();
            person.Email = newEmail;
            person.Phone = request.Phone?.Trim() ?? string.Empty;

            await _personRepository.UpdateAsync(person);

            PersonInfo personInfo = new PersonInfo(
                person.PersonId,
                person.Name,
                person.Email,
                person.Phone,
                person.IsSystemAdmin
            );

            _logger.LogInformation($"Profile updated for person {person.PersonId}");

            return new OkObjectResult(personInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile");
            return new ObjectResult(new { Message = "An error occurred" })
            {
                StatusCode = 500
            };
        }
    }
}
