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

public class PeopleFunctions
{
    private readonly ILogger<PeopleFunctions> _logger;
    private readonly IPersonRepository _personRepository;
    private readonly IEventRoleRepository _roleRepository;
    private readonly ClaimsService _claimsService;

    public PeopleFunctions(
        ILogger<PeopleFunctions> logger,
        IPersonRepository personRepository,
        IEventRoleRepository roleRepository,
        ClaimsService claimsService)
    {
        _logger = logger;
        _personRepository = personRepository;
        _roleRepository = roleRepository;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Get person details (event admins only).
    /// GET /api/people/{personId}?eventId={eventId}
    /// </summary>
#pragma warning disable MA0051
    [Function("GetPerson")]
    public async Task<IActionResult> GetPerson(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId}")] HttpRequest req,
        string personId)
    {
        try
        {
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { Message = "Not authenticated" });
            }

            // Get event ID from query string (required for permission check)
            string? eventId = req.Query["eventId"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return new BadRequestObjectResult(new { Message = "eventId query parameter is required" });
            }

            // Get claims
            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);

            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { Message = "Invalid or expired session" });
            }

            // Check if user is an event admin
            if (!claims.CanUseElevatedPermissions || !claims.IsEventAdmin)
            {
                return new ForbidResult();
            }

            // Get person
            PersonEntity? person = await _personRepository.GetAsync(personId);
            if (person == null)
            {
                return new NotFoundObjectResult(new { Message = "Person not found" });
            }

            // Get their roles in this event
            IEnumerable<EventRoleEntity> roles = await _roleRepository.GetByPersonAndEventAsync(personId, eventId);
            List<EventRoleInfo> eventRoles = [.. roles.Select(r => new EventRoleInfo(
                r.Role,
                JsonSerializer.Deserialize<List<string>>(r.AreaIdsJson) ?? []
            ))];

            PersonDetailsResponse response = new PersonDetailsResponse(
                person.PersonId,
                person.Name,
                person.Email,
                person.Phone,
                person.IsSystemAdmin,
                eventRoles,
                person.CreatedAt
            );

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting person");
            return new ObjectResult(new { Message = "An error occurred" })
            {
                StatusCode = 500
            };
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Update person details (event admins only).
    /// PUT /api/people/{personId}?eventId={eventId}
    /// Body: { "Name": "John Doe", "Email": "john@example.com", "Phone": "555-1234" }
    /// </summary>
#pragma warning disable MA0051
    [Function("UpdatePerson")]
    public async Task<IActionResult> UpdatePerson(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "people/{personId}")] HttpRequest req,
        string personId)
    {
        try
        {
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { Message = "Not authenticated" });
            }

            // Get event ID from query string (required for permission check)
            string? eventId = req.Query["eventId"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return new BadRequestObjectResult(new { Message = "eventId query parameter is required" });
            }

            // Get claims
            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);

            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { Message = "Invalid or expired session" });
            }

            // Check if user is an event admin
            if (!claims.CanUseElevatedPermissions || !claims.IsEventAdmin)
            {
                return new ForbidResult();
            }

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            UpdatePersonRequest? request = JsonSerializer.Deserialize<UpdatePersonRequest>(requestBody, FunctionHelpers.JsonOptions);

            if (request == null)
            {
                return new BadRequestObjectResult(new { Message = "Invalid request body" });
            }

            // Validate
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new BadRequestObjectResult(new { Message = "Name is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Email) || !Validators.IsValidEmail(request.Email))
            {
                return new BadRequestObjectResult(new { Message = "Valid email is required" });
            }

            // Get person
            PersonEntity? person = await _personRepository.GetAsync(personId);
            if (person == null)
            {
                return new NotFoundObjectResult(new { Message = "Person not found" });
            }

            // Check if email is changing and if new email already exists
            string newEmail = request.Email.Trim().ToLowerInvariant();
            if (newEmail != person.Email)
            {
                PersonEntity? existingPerson = await _personRepository.GetByEmailAsync(newEmail);
                if (existingPerson != null && existingPerson.PersonId != personId)
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

            _logger.LogInformation("Person updated by admin {AdminPersonId}: {PersonId}", claims.PersonId, person.PersonId);

            return new OkObjectResult(personInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating person");
            return new ObjectResult(new { Message = "An error occurred" })
            {
                StatusCode = 500
            };
        }
    }
#pragma warning restore MA0051
}
