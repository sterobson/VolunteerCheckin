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

    public AuthFunctions(ILogger<AuthFunctions> logger, TableStorageService tableStorage)
    {
        _logger = logger;
        _tableStorage = tableStorage;
    }

    [Function("InstantLogin")]
    public async Task<IActionResult> InstantLogin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/instant-login")] HttpRequest req)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<InstantLoginRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return new BadRequestObjectResult(new { message = "Email is required" });
            }

            var adminTable = _tableStorage.GetAdminUsersTable();

            // Try to get existing admin, or create new one
            AdminUserEntity admin;
            try
            {
                admin = await adminTable.GetEntityAsync<AdminUserEntity>("ADMIN", request.Email);
                _logger.LogInformation($"Existing admin logged in: {request.Email}");
            }
            catch
            {
                // Admin doesn't exist, create new one
                admin = new AdminUserEntity
                {
                    RowKey = request.Email,
                    Email = request.Email
                };
                await adminTable.AddEntityAsync(admin);
                _logger.LogInformation($"New admin created and logged in: {request.Email}");
            }

            return new OkObjectResult(new InstantLoginResponse(true, admin.Email, "Login successful"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during instant login");
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
            var request = JsonSerializer.Deserialize<InstantLoginRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
