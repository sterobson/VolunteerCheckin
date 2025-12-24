using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Functions;

public class AuthFunctions
{
    private readonly ILogger<AuthFunctions> _logger;
    private readonly IAdminUserRepository _adminUserRepository;

    public AuthFunctions(ILogger<AuthFunctions> logger, IAdminUserRepository adminUserRepository)
    {
        _logger = logger;
        _adminUserRepository = adminUserRepository;
    }

    [Function("InstantLogin")]
    public async Task<IActionResult> InstantLogin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/instant-login")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            InstantLoginRequest? request = JsonSerializer.Deserialize<InstantLoginRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return new BadRequestObjectResult(new { message = "Email is required" });
            }

            // Try to get existing admin, or create new one
            AdminUserEntity? admin = await _adminUserRepository.GetByEmailAsync(request.Email);

            if (admin == null)
            {
                // Admin doesn't exist, create new one
                admin = new AdminUserEntity
                {
                    RowKey = request.Email,
                    Email = request.Email
                };
                await _adminUserRepository.AddAsync(admin);
                _logger.LogInformation($"New admin created and logged in: {request.Email}");
            }
            else
            {
                _logger.LogInformation($"Existing admin logged in: {request.Email}");
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
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            InstantLoginRequest? request = JsonSerializer.Deserialize<InstantLoginRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return new BadRequestObjectResult(new { message = "Email is required" });
            }

            AdminUserEntity admin = new()
            {
                RowKey = request.Email,
                Email = request.Email
            };

            await _adminUserRepository.AddAsync(admin);

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
