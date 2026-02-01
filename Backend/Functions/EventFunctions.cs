using Azure;
using Azure.Data.Tables;
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

public class EventFunctions
{
    private readonly ILogger<EventFunctions> _logger;
    private readonly IEventRepository _eventRepository;
    private readonly IPersonRepository _personRepository;
    private readonly IEventRoleRepository _eventRoleRepository;
    private readonly ClaimsService _claimsService;
    private readonly IEventService _eventService;
    private readonly IEventDeletionRepository _eventDeletionRepository;

    public EventFunctions(
        ILogger<EventFunctions> logger,
        IEventRepository eventRepository,
        IPersonRepository personRepository,
        IEventRoleRepository eventRoleRepository,
        ClaimsService claimsService,
        IEventService eventService,
        IEventDeletionRepository eventDeletionRepository)
    {
        _logger = logger;
        _eventRepository = eventRepository;
        _personRepository = personRepository;
        _eventRoleRepository = eventRoleRepository;
        _claimsService = claimsService;
        _eventService = eventService;
        _eventDeletionRepository = eventDeletionRepository;
    }

    private async Task<bool> IsUserAuthorizedForEvent(string eventId, string userEmail)
    {
        PersonEntity? person = await _personRepository.GetByEmailAsync(userEmail);
        if (person == null) return false;

        IEnumerable<EventRoleEntity> roles = await _eventRoleRepository.GetByPersonAndEventAsync(person.PersonId, eventId);
        return roles.Any(r => r.Role == Constants.RoleEventAdmin);
    }

#pragma warning disable MA0051
    [Function("CreateEvent")]
    public async Task<IActionResult> CreateEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events")] HttpRequest req)
    {
        try
        {
            // Require authentication - get claims to identify who is creating the event
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            (CreateEventRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<CreateEventRequest>(req);
            if (error != null) return error;

            // Convert the event date to UTC
            DateTime eventDateUtc = FunctionHelpers.ConvertToUtc(request!.EventDate, request.TimeZoneId);

            EventEntity eventEntity = new()
            {
                RowKey = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                EventDate = eventDateUtc,
                TimeZoneId = request.TimeZoneId
            };

            // Build and set the v2 payload
            EventPayload payload = new()
            {
                Terminology = new TerminologyPayload
                {
                    Person = request.PeopleTerm ?? "Marshals",
                    Location = request.CheckpointTerm ?? "Checkpoints",
                    Area = request.AreaTerm ?? "Areas",
                    Task = request.ChecklistTerm ?? "Checklists",
                    Course = request.CourseTerm ?? "Course"
                },
                Styling = new StylingPayload
                {
                    Locations = new LocationStylingPayload
                    {
                        DefaultType = request.DefaultCheckpointStyleType ?? "default",
                        DefaultColor = request.DefaultCheckpointStyleColor ?? string.Empty,
                        DefaultBackgroundShape = request.DefaultCheckpointStyleBackgroundShape ?? string.Empty,
                        DefaultBackgroundColor = request.DefaultCheckpointStyleBackgroundColor ?? string.Empty,
                        DefaultBorderColor = request.DefaultCheckpointStyleBorderColor ?? string.Empty,
                        DefaultIconColor = request.DefaultCheckpointStyleIconColor ?? string.Empty,
                        DefaultSize = request.DefaultCheckpointStyleSize ?? string.Empty,
                        DefaultMapRotation = request.DefaultCheckpointStyleMapRotation ?? string.Empty
                    },
                    Branding = new BrandingPayload
                    {
                        AccentColour = request.BrandingAccentColor ?? string.Empty,
                        HeaderGradientStart = request.BrandingHeaderGradientStart ?? string.Empty,
                        HeaderGradientEnd = request.BrandingHeaderGradientEnd ?? string.Empty,
                        PageGradientStart = request.BrandingPageGradientStart ?? string.Empty,
                        PageGradientEnd = request.BrandingPageGradientEnd ?? string.Empty,
                        LogoUrl = request.BrandingLogoUrl ?? string.Empty,
                        LogoPosition = request.BrandingLogoPosition ?? string.Empty
                    }
                }
            };
            eventEntity.SetPayload(payload);

            await _eventRepository.AddAsync(eventEntity);

            // Grant EventAdmin role to the creator
            string roleId = Guid.NewGuid().ToString();
            EventRoleEntity eventRole = new EventRoleEntity
            {
                PartitionKey = claims.PersonId,
                RowKey = EventRoleEntity.CreateRowKey(eventEntity.RowKey, roleId),
                PersonId = claims.PersonId,
                EventId = eventEntity.RowKey,
                Role = Constants.RoleEventAdmin,
                AreaIdsJson = "[]",
                GrantedByPersonId = claims.PersonId,
                GrantedAt = DateTime.UtcNow
            };
            await _eventRoleRepository.AddAsync(eventRole);

            EventResponse response = eventEntity.ToResponse();

            _logger.LogInformation("Event created: {EventId}, Admin: {PersonId}", eventEntity.RowKey, claims.PersonId);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating event");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    [Function("GetEvent")]
    public async Task<IActionResult> GetEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);

            if (eventEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Event not found" });
            }

            EventResponse response = eventEntity.ToResponse();

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetAllEvents")]
    public async Task<IActionResult> GetAllEvents(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events")] HttpRequest req)
    {
        try
        {
            // Get admin email from header
            (string? adminEmail, IActionResult? error) = FunctionHelpers.GetAdminEmailFromHeader(req);
            if (error != null) return error;
            if (string.IsNullOrEmpty(adminEmail)) return new UnauthorizedObjectResult(new { message = "Admin email required" });

            // Get the person by email
            PersonEntity? person = await _personRepository.GetByEmailAsync(adminEmail);
            if (person == null) return new OkObjectResult(new List<EventResponse>());

            // Get all events where user has EventAdmin role
            IEnumerable<EventRoleEntity> userRoles = await _eventRoleRepository.GetByPersonAsync(person.PersonId);
            HashSet<string> userEventIds = userRoles
                .Where(r => r.Role == Constants.RoleEventAdmin)
                .Select(r => r.EventId)
                .ToHashSet();

            // Fetch all events and filter by user access
            IEnumerable<EventEntity> allEvents = await _eventRepository.GetAllAsync();
            IEnumerable<EventResponse> events = allEvents
                .Where(e => userEventIds.Contains(e.RowKey))
                .Select(e => e.ToResponse());

            // Check if pagination is requested
            if (req.Query.ContainsKey("page") || req.Query.ContainsKey("pageSize"))
            {
                (int page, int pageSize) = FunctionHelpers.GetPaginationParams(req);
                PaginatedResponse<EventResponse> paginatedResponse = FunctionHelpers.CreatePaginatedResponse(events, page, pageSize);
                return new OkObjectResult(paginatedResponse);
            }

            List<EventResponse> eventsList = [.. events];
            return new OkObjectResult(eventsList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting events");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetAllEventsSummary")]
    public async Task<IActionResult> GetAllEventsSummary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/summary")] HttpRequest req)
    {
        try
        {
            // Get admin email from header
            (string? adminEmail, IActionResult? error) = FunctionHelpers.GetAdminEmailFromHeader(req);
            if (error != null) return error;
            if (string.IsNullOrEmpty(adminEmail)) return new UnauthorizedObjectResult(new { message = "Admin email required" });

            // Get the person by email
            PersonEntity? person = await _personRepository.GetByEmailAsync(adminEmail);
            if (person == null) return new OkObjectResult(new List<EventSummaryResponse>());

            // Get all events where user has EventAdmin role
            IEnumerable<EventRoleEntity> userRoles = await _eventRoleRepository.GetByPersonAsync(person.PersonId);
            HashSet<string> userEventIds = userRoles
                .Where(r => r.Role == Constants.RoleEventAdmin)
                .Select(r => r.EventId)
                .ToHashSet();

            // Fetch all events and filter by user access, return summary only
            IEnumerable<EventEntity> allEvents = await _eventRepository.GetAllAsync();
            List<EventSummaryResponse> summaries = [.. allEvents
                .Where(e => userEventIds.Contains(e.RowKey))
                .Select(e => e.ToSummaryResponse())];

            return new OkObjectResult(summaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event summaries");
            return new StatusCodeResult(500);
        }
    }

#pragma warning disable MA0051
    [Function("UpdateEvent")]
    public async Task<IActionResult> UpdateEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "events/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Check authorization
            (string? adminEmail, IActionResult? authError) = FunctionHelpers.GetAdminEmailFromHeader(req);
            if (authError != null) return authError;

            if (!await IsUserAuthorizedForEvent(eventId, adminEmail!))
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to update this event" });
            }

            (UpdateEventRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<UpdateEventRequest>(req);
            if (error != null) return error;

            EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);

            if (eventEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Event not found" });
            }

            // Convert the event date to UTC and apply all updates
            DateTime eventDateUtc = FunctionHelpers.ConvertToUtc(request!.EventDate, request.TimeZoneId);

            eventEntity.ApplyUpdates(
                name: request.Name,
                description: request.Description,
                eventDateUtc: eventDateUtc,
                timeZoneId: request.TimeZoneId,
                peopleTerm: request.PeopleTerm,
                checkpointTerm: request.CheckpointTerm,
                areaTerm: request.AreaTerm,
                checklistTerm: request.ChecklistTerm,
                courseTerm: request.CourseTerm,
                defaultCheckpointStyleType: request.DefaultCheckpointStyleType,
                defaultCheckpointStyleColor: request.DefaultCheckpointStyleColor,
                defaultCheckpointStyleBackgroundShape: request.DefaultCheckpointStyleBackgroundShape,
                defaultCheckpointStyleBackgroundColor: request.DefaultCheckpointStyleBackgroundColor,
                defaultCheckpointStyleBorderColor: request.DefaultCheckpointStyleBorderColor,
                defaultCheckpointStyleIconColor: request.DefaultCheckpointStyleIconColor,
                defaultCheckpointStyleSize: request.DefaultCheckpointStyleSize,
                defaultCheckpointStyleMapRotation: request.DefaultCheckpointStyleMapRotation,
                brandingHeaderGradientStart: request.BrandingHeaderGradientStart,
                brandingHeaderGradientEnd: request.BrandingHeaderGradientEnd,
                brandingLogoUrl: request.BrandingLogoUrl,
                brandingLogoPosition: request.BrandingLogoPosition,
                brandingAccentColor: request.BrandingAccentColor,
                brandingPageGradientStart: request.BrandingPageGradientStart,
                brandingPageGradientEnd: request.BrandingPageGradientEnd,
                routeColor: request.RouteColor,
                routeStyle: request.RouteStyle,
                routeWeight: request.RouteWeight
            );

            await _eventRepository.UpdateAsync(eventEntity);

            EventResponse response = eventEntity.ToResponse();

            _logger.LogInformation("Event updated: {EventId}", eventId);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating event");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    [Function("DeleteEvent")]
    public async Task<IActionResult> DeleteEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            (string? adminEmail, IActionResult? headerError) = FunctionHelpers.GetAdminEmailFromHeader(req);
            if (headerError != null) return headerError;

            if (!await IsUserAuthorizedForEvent(eventId, adminEmail!))
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to delete this event" });
            }

            await _eventService.DeleteEventWithAllDataAsync(eventId);

            _logger.LogInformation("Event deleted with all related data: {EventId}", eventId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting event");
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Request deletion of an event. This queues the event for deletion by a background process.
    /// The event will be marked as pending deletion immediately, preventing any further access.
    /// Supports both regular admin authentication and sample event authentication.
    /// </summary>
    [Function("RequestEventDeletion")]
    public async Task<IActionResult> RequestEventDeletion(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/request-deletion")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Try to authenticate via session token or sample code
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            string? sampleCode = FunctionHelpers.GetSampleCodeFromHeader(req);

            if (string.IsNullOrWhiteSpace(sessionToken) && string.IsNullOrWhiteSpace(sampleCode))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsWithSampleSupportAsync(sessionToken, sampleCode, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Check if user has admin access to this event
            if (!claims.IsSystemAdmin && !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to delete this event" });
            }

            // Check if deletion is already pending
            if (await _eventDeletionRepository.IsDeletionPendingAsync(eventId))
            {
                return new ConflictObjectResult(new { message = "Event deletion is already in progress" });
            }

            // Get the event details for the deletion record
            EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);
            if (eventEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Event not found" });
            }

            // Determine requester info
            string requesterEmail = claims.PersonEmail ?? claims.PersonId;
            string requesterName = claims.PersonName ?? requesterEmail;

            // Create the deletion request
            EventDeletionEntity deletion = new()
            {
                EventId = eventId,
                EventName = eventEntity.Name,
                RequestedByEmail = requesterEmail,
                RequestedByName = requesterName,
                RequestedAt = DateTime.UtcNow,
                Status = EventDeletionStatus.Pending
            };

            await _eventDeletionRepository.AddAsync(deletion);

            // Immediately delete the Event and SampleEventAdmin records to prevent further access
            // The remaining data will be cleaned up by the scheduled background job
            await _eventService.DeleteEventRecordImmediateAsync(eventId);

            _logger.LogInformation(
                "Event deletion requested: {EventId} ({EventName}) by {Name} ({Email}). Event record deleted immediately.",
                eventId, eventEntity.Name, requesterName, requesterEmail);

            return new OkObjectResult(new
            {
                message = "Event has been deleted",
                eventId,
                eventName = eventEntity.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting event deletion");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetEventAdmins")]
    public async Task<IActionResult> GetEventAdmins(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/admins")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Require authentication and admin permissions (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            string? sampleCode = FunctionHelpers.GetSampleCodeFromHeader(req);

            if (string.IsNullOrWhiteSpace(sessionToken) && string.IsNullOrWhiteSpace(sampleCode))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsWithSampleSupportAsync(sessionToken, sampleCode, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Must have elevated permissions and be event admin or system admin
            // Sample code users always have CanUseElevatedPermissions via SampleCode auth method
            if (!claims.CanUseElevatedPermissions)
            {
                return new ForbidResult();
            }

            if (!claims.IsEventAdmin && !claims.IsSystemAdmin)
            {
                return new ForbidResult();
            }

            // Get all EventAdmin roles for this event
            IEnumerable<EventRoleEntity> roles = await _eventRoleRepository.GetByEventAsync(eventId);
            List<EventRoleEntity> adminRoles = [.. roles.Where(r => r.Role == Constants.RoleEventAdmin)];

            // Build response by looking up person info for each role
            List<EventAdminResponse> admins = [];
            foreach (EventRoleEntity role in adminRoles)
            {
                PersonEntity? person = await _personRepository.GetAsync(role.PersonId);
                if (person != null)
                {
                    admins.Add(new EventAdminResponse(eventId, person.Email, role.Role, role.GrantedAt));
                }
            }

            return new OkObjectResult(admins);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event admins");
            return new StatusCodeResult(500);
        }
    }

#pragma warning disable MA0051
    [Function("AddEventAdmin")]
    public async Task<IActionResult> AddEventAdmin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/admins")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Check authorization
            (string? adminEmail, IActionResult? authError) = FunctionHelpers.GetAdminEmailFromHeader(req);
            if (authError != null) return authError;

            if (!await IsUserAuthorizedForEvent(eventId, adminEmail!))
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to manage admins for this event" });
            }

            (AddEventAdminRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<AddEventAdminRequest>(req);
            if (error != null) return error;

            if (string.IsNullOrWhiteSpace(request!.UserEmail))
            {
                return new BadRequestObjectResult(new { message = "User email is required" });
            }

            // Normalize email
            string email = request.UserEmail.Trim().ToLowerInvariant();

            // Validate email format
            if (!Validators.IsValidEmail(email))
            {
                return new BadRequestObjectResult(new { message = "Invalid email address format" });
            }

            // Get or create PersonEntity
            PersonEntity? person = await _personRepository.GetByEmailAsync(email);
            if (person == null)
            {
                string personId = Guid.NewGuid().ToString();
                person = new PersonEntity
                {
                    PersonId = personId,
                    PartitionKey = "PERSON",
                    RowKey = personId,
                    Email = email,
                    Name = string.Empty, // They'll update this themselves
                    Phone = string.Empty,
                    IsSystemAdmin = false,
                    CreatedAt = DateTime.UtcNow
                };
                await _personRepository.AddAsync(person);
            }

            // Check if they already have EventAdmin role
            IEnumerable<EventRoleEntity> existingRoles = await _eventRoleRepository.GetByPersonAndEventAsync(person.PersonId, eventId);
            bool hasAdminRole = existingRoles.Any(r => r.Role == Constants.RoleEventAdmin);

            if (hasAdminRole)
            {
                return new BadRequestObjectResult(new { message = "User is already an admin for this event" });
            }

            // Get the PersonId of the admin granting this role
            PersonEntity? grantingAdmin = await _personRepository.GetByEmailAsync(adminEmail!);
            string grantedByPersonId = grantingAdmin?.PersonId ?? string.Empty;

            // Create EventAdmin role
            string roleId = Guid.NewGuid().ToString();
            EventRoleEntity eventRole = new EventRoleEntity
            {
                PartitionKey = person.PersonId,
                RowKey = EventRoleEntity.CreateRowKey(eventId, roleId),
                PersonId = person.PersonId,
                EventId = eventId,
                Role = Constants.RoleEventAdmin,
                AreaIdsJson = "[]", // Event-wide admin
                GrantedByPersonId = grantedByPersonId,
                GrantedAt = DateTime.UtcNow
            };
            await _eventRoleRepository.AddAsync(eventRole);

            EventAdminResponse response = new(eventId, email, Constants.RoleEventAdmin, eventRole.GrantedAt);

            _logger.LogInformation("Admin added to event {EventId}: {Email} (PersonId: {PersonId})", eventId, email, person.PersonId);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding event admin");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    [Function("RemoveEventAdmin")]
    public async Task<IActionResult> RemoveEventAdmin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{eventId}/admins/{userEmail}")] HttpRequest req,
        string eventId,
        string userEmail)
    {
        try
        {
            (string? adminEmail, IActionResult? headerError) = FunctionHelpers.GetAdminEmailFromHeader(req);
            if (headerError != null) return headerError;

            if (!await IsUserAuthorizedForEvent(eventId, adminEmail!))
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to manage admins for this event" });
            }

            // Normalize email
            string email = userEmail.Trim().ToLowerInvariant();

            // Count total admins for this event
            IEnumerable<EventRoleEntity> allRoles = await _eventRoleRepository.GetByEventAsync(eventId);
            int adminCount = allRoles.Count(r => r.Role == Constants.RoleEventAdmin);

            // Ensure at least one admin remains
            if (adminCount <= 1)
            {
                _logger.LogWarning("Attempt to remove last admin from event {EventId} rejected: {Email}", eventId, email);
                return new BadRequestObjectResult(new { message = Constants.ErrorCannotRemoveLastAdmin });
            }

            // Find the person to remove
            PersonEntity? person = await _personRepository.GetByEmailAsync(email);
            if (person == null)
            {
                return new NotFoundObjectResult(new { message = "User not found" });
            }

            // Get all EventAdmin roles for this person in this event and delete in parallel
            IEnumerable<EventRoleEntity> roles = await _eventRoleRepository.GetByPersonAndEventAsync(person.PersonId, eventId);
            List<Task> deleteTasks = [.. roles
                .Where(r => r.Role == Constants.RoleEventAdmin)
                .Select(role => _eventRoleRepository.DeleteAsync(person.PersonId, role.RowKey))];
            await Task.WhenAll(deleteTasks);

            _logger.LogInformation("Admin removed from event {EventId}: {Email}", eventId, email);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing event admin");
            return new StatusCodeResult(500);
        }
    }

#pragma warning disable MA0051
    [Function("UploadGpx")]
    public async Task<IActionResult> UploadGpx(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/upload-gpx")] HttpRequest req,
        string eventId)
    {
        try
        {
            (string? adminEmail, IActionResult? headerError) = FunctionHelpers.GetAdminEmailFromHeader(req);
            if (headerError != null) return headerError;

            if (!await IsUserAuthorizedForEvent(eventId, adminEmail!))
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to upload routes for this event" });
            }

            // Get the uploaded file
            if (req.Form.Files.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "No file uploaded" });
            }

            IFormFile gpxFile = req.Form.Files[0];
            if (!gpxFile.FileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
            {
                return new BadRequestObjectResult(new { message = "File must be a .gpx file" });
            }

            // Parse the GPX file
            List<RoutePoint> route;
            await using (Stream stream = gpxFile.OpenReadStream())
            {
                route = GpxParserService.ParseGpxFile(stream);
            }

            if (route.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "No route points found in GPX file" });
            }

            // Update the event with the route
            EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);

            if (eventEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Event not found" });
            }

            string routeJson = JsonSerializer.Serialize(route);

            // Check if the route is too large
            if (System.Text.Encoding.UTF8.GetByteCount(routeJson) > 60000) // Leave some buffer under 64KB
            {
                return new BadRequestObjectResult(new { message = $"Route is too large ({route.Count} points). The GPX file has too many points even after simplification. Try a simpler route or fewer track points." });
            }

            eventEntity.GpxRouteJson = routeJson;
            await _eventRepository.UpdateAsync(eventEntity);

            _logger.LogInformation("GPX route uploaded for event {EventId}: {RouteCount} points (simplified)", eventId, route.Count);

            return new OkObjectResult(new { success = true, message = $"Route uploaded successfully with {route.Count} points (simplified)", route });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid GPX file");
            return new BadRequestObjectResult(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading GPX route");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051
}
