using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Functions;

public class AuthFunctions
{
    private readonly ILogger<AuthFunctions> _logger;
    private readonly TableStorageService _tableStorage;
    private readonly EmailService _emailService;
    private readonly string _frontendUrl;

    public AuthFunctions(ILogger<AuthFunctions> logger, TableStorageService tableStorage, EmailService emailService)
    {
        _logger = logger;
        _tableStorage = tableStorage;
        _emailService = emailService;
        _frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:5173";
    }

    [Function("RequestMagicLink")]
    public async Task<IActionResult> RequestMagicLink(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/request-magic-link")] HttpRequest req)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<RequestMagicLinkRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return new BadRequestObjectResult(new { message = "Email is required" });
            }

            var adminTable = _tableStorage.GetAdminUsersTable();

            // Check if email exists in admin users
            try
            {
                await adminTable.GetEntityAsync<AdminUserEntity>("ADMIN", request.Email);
            }
            catch
            {
                // Email not found - don't reveal this to prevent email enumeration
                _logger.LogWarning($"Magic link requested for non-admin email: {request.Email}");
                return new OkObjectResult(new MagicLinkResponse(true, "If your email is registered as an admin, you will receive a login link shortly."));
            }

            // Generate magic link token
            var token = Guid.NewGuid().ToString("N");
            var magicLink = new MagicLinkEntity
            {
                RowKey = token,
                Email = request.Email,
                ExpiryDate = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false
            };

            var magicLinkTable = _tableStorage.GetMagicLinksTable();
            await magicLinkTable.AddEntityAsync(magicLink);

            // Send email with magic link
            var loginUrl = $"{_frontendUrl}/admin/login?token={token}";
            await _emailService.SendMagicLinkEmailAsync(request.Email, loginUrl);

            _logger.LogInformation($"Magic link sent to {request.Email}");

            return new OkObjectResult(new MagicLinkResponse(true, "If your email is registered as an admin, you will receive a login link shortly."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting magic link");
            return new StatusCodeResult(500);
        }
    }

    [Function("ValidateToken")]
    public async Task<IActionResult> ValidateToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/validate-token")] HttpRequest req)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<ValidateTokenRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Token))
            {
                return new BadRequestObjectResult(new { message = "Token is required" });
            }

            var magicLinkTable = _tableStorage.GetMagicLinksTable();

            MagicLinkEntity? magicLink;
            try
            {
                magicLink = await magicLinkTable.GetEntityAsync<MagicLinkEntity>("MAGICLINK", request.Token);
            }
            catch
            {
                return new OkObjectResult(new ValidateTokenResponse(false, string.Empty));
            }

            // Validate token
            if (magicLink.IsUsed || magicLink.ExpiryDate < DateTime.UtcNow)
            {
                return new OkObjectResult(new ValidateTokenResponse(false, string.Empty));
            }

            // Mark token as used
            magicLink.IsUsed = true;
            await magicLinkTable.UpdateEntityAsync(magicLink, magicLink.ETag);

            return new OkObjectResult(new ValidateTokenResponse(true, magicLink.Email));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return new StatusCodeResult(500);
        }
    }

    [Function("CreateAdmin")]
    public async Task<IActionResult> CreateAdmin(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "auth/create-admin")] HttpRequest req)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<RequestMagicLinkRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return new BadRequestObjectResult(new { message = "Email is required" });
            }

            var adminTable = _tableStorage.GetAdminUsersTable();
            var admin = new AdminUserEntity
            {
                RowKey = request.Email,
                Email = request.Email
            };

            await adminTable.AddEntityAsync(admin);

            _logger.LogInformation($"Admin created: {request.Email}");

            return new OkObjectResult(new { email = request.Email, created = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating admin");
            return new StatusCodeResult(500);
        }
    }
}
