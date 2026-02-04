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
    private readonly EmailService _emailService;

    public EventFunctions(
        ILogger<EventFunctions> logger,
        IEventRepository eventRepository,
        IPersonRepository personRepository,
        IEventRoleRepository eventRoleRepository,
        ClaimsService claimsService,
        IEventService eventService,
        IEventDeletionRepository eventDeletionRepository,
        EmailService emailService)
    {
        _logger = logger;
        _eventRepository = eventRepository;
        _personRepository = personRepository;
        _eventRoleRepository = eventRoleRepository;
        _claimsService = claimsService;
        _eventService = eventService;
        _eventDeletionRepository = eventDeletionRepository;
        _emailService = emailService;
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
                    Task = request.ChecklistTerm ?? "Tasks",
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

            // Grant EventOwner role to the creator
            string roleId = Guid.NewGuid().ToString();
            EventRoleEntity eventRole = new EventRoleEntity
            {
                PartitionKey = claims.PersonId,
                RowKey = EventRoleEntity.CreateRowKey(eventEntity.RowKey, roleId),
                PersonId = claims.PersonId,
                EventId = eventEntity.RowKey,
                Role = Constants.RoleEventOwner,
                AreaIdsJson = "[]",
                GrantedByPersonId = claims.PersonId,
                GrantedAt = DateTime.UtcNow
            };
            await _eventRoleRepository.AddAsync(eventRole);

            EventResponse response = eventEntity.ToResponse();

            _logger.LogInformation("Event created: {EventId}, Owner: {PersonId}", eventEntity.RowKey, claims.PersonId);

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

    [Function("GetAllEventsSummary")]
    public async Task<IActionResult> GetAllEventsSummary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/summary")] HttpRequest req)
    {
        try
        {
            // Require authentication via session token
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

            // Get all events where user has any event role (Owner, Administrator, Contributor, Viewer, or legacy Admin)
            IEnumerable<EventRoleEntity> userRoles = await _eventRoleRepository.GetByPersonAsync(claims.PersonId);
            HashSet<string> userEventIds = userRoles
                .Where(r => r.Role == Constants.RoleEventOwner ||
                           r.Role == Constants.RoleEventAdministrator ||
                           r.Role == Constants.RoleEventContributor ||
                           r.Role == Constants.RoleEventViewer ||
                           r.Role == Constants.RoleEventAdmin)
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
            // Check authorization via claims (supports sample codes)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            if (!claims.CanModifyEvent)
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
            // Check authorization via claims (supports sample codes)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            if (!claims.CanDeleteEvent)
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

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Check if user has admin access to this event
            if (!claims.IsEventAdmin)
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

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Must have elevated permissions and be event admin or system admin
            // Sample code users always have CanUseElevatedPermissions via SampleCode auth method
            if (!claims.CanUseElevatedPermissions)
            {
                return new ObjectResult(new { message = "Forbidden" }) { StatusCode = 403 };
            }

            if (!claims.IsEventAdmin)
            {
                return new ObjectResult(new { message = "Forbidden" }) { StatusCode = 403 };
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
            // Check authorization via claims (supports sample codes)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            if (!claims.CanManageUsers)
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
                GrantedByPersonId = claims.PersonId,
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
            // Check authorization via claims (supports sample codes)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            if (!claims.CanManageUsers)
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

    // ============================================================================
    // New User Management Endpoints (Role-Based Access Control)
    // ============================================================================

    /// <summary>
    /// Validate a role string and return the role constant if valid
    /// </summary>
    private static string? ValidateRole(string role)
    {
        return role switch
        {
            Constants.RoleEventOwner => Constants.RoleEventOwner,
            Constants.RoleEventAdministrator => Constants.RoleEventAdministrator,
            Constants.RoleEventContributor => Constants.RoleEventContributor,
            Constants.RoleEventViewer => Constants.RoleEventViewer,
            _ => null
        };
    }

    /// <summary>
    /// Get human-readable description for a role
    /// </summary>
    private static string GetRoleDescription(string role)
    {
        return role switch
        {
            Constants.RoleEventOwner => "Full control including event deletion and managing other owners",
            Constants.RoleEventAdministrator => "Can modify event data and manage non-owner users",
            Constants.RoleEventContributor => "Can modify event data but cannot manage users",
            Constants.RoleEventViewer => "Read-only access to view event data",
            _ => "Access to this event"
        };
    }

    /// <summary>
    /// Count the number of owners for an event (includes legacy EventAdmin)
    /// </summary>
    private async Task<int> CountEventOwnersAsync(string eventId)
    {
        IEnumerable<EventRoleEntity> allRoles = await _eventRoleRepository.GetByEventAsync(eventId);
        return allRoles.Count(r => r.Role == Constants.RoleEventOwner || r.Role == Constants.RoleEventAdmin);
    }

    [Function("GetEventUsers")]
    public async Task<IActionResult> GetEventUsers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/users")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Must have elevated permissions and any event role
            if (!claims.CanUseElevatedPermissions)
            {
                return new ObjectResult(new { message = "Forbidden" }) { StatusCode = 403 };
            }

            if (!claims.HasAnyEventRole)
            {
                return new ObjectResult(new { message = "Forbidden" }) { StatusCode = 403 };
            }

            // Get all roles for this event
            IEnumerable<EventRoleEntity> roles = await _eventRoleRepository.GetByEventAsync(eventId);

            // Filter to only include the new hierarchical roles + legacy EventAdmin
            List<EventRoleEntity> userRoles = [.. roles.Where(r =>
                r.Role == Constants.RoleEventOwner ||
                r.Role == Constants.RoleEventAdministrator ||
                r.Role == Constants.RoleEventContributor ||
                r.Role == Constants.RoleEventViewer ||
                r.Role == Constants.RoleEventAdmin)];

            // Build response by looking up person info for each role
            List<EventUserResponse> users = [];
            foreach (EventRoleEntity role in userRoles)
            {
                PersonEntity? person = await _personRepository.GetAsync(role.PersonId);
                if (person != null)
                {
                    // Look up who granted the role
                    string? grantedByName = null;
                    if (!string.IsNullOrEmpty(role.GrantedByPersonId))
                    {
                        PersonEntity? grantedBy = await _personRepository.GetAsync(role.GrantedByPersonId);
                        grantedByName = grantedBy?.Name;
                    }

                    // Map legacy EventAdmin to EventOwner for display
                    string displayRole = role.Role == Constants.RoleEventAdmin
                        ? Constants.RoleEventOwner
                        : role.Role;

                    users.Add(new EventUserResponse(
                        eventId,
                        person.PersonId,
                        person.Email,
                        person.Name ?? person.Email,
                        displayRole,
                        role.GrantedAt,
                        grantedByName
                    ));
                }
            }

            return new OkObjectResult(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event users");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetMyEventPermissions")]
    public async Task<IActionResult> GetMyEventPermissions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/my-permissions")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Build permissions response
            EventPermissionsResponse permissions = new(
                Role: claims.PrimaryEventRole ?? "None",
                CanManageOwners: claims.CanManageOwners,
                CanManageUsers: claims.CanManageUsers,
                CanModifyEvent: claims.CanModifyEvent,
                CanDeleteEvent: claims.CanDeleteEvent,
                IsReadOnly: claims.IsReadOnly
            );

            return new OkObjectResult(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event permissions");
            return new StatusCodeResult(500);
        }
    }

#pragma warning disable MA0051
    [Function("AddEventUser")]
    public async Task<IActionResult> AddEventUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/users")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Must have elevated permissions
            if (!claims.CanUseElevatedPermissions)
            {
                return new ObjectResult(new { message = "Forbidden" }) { StatusCode = 403 };
            }

            // Must be able to manage users
            if (!claims.CanManageUsers)
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to manage users for this event" });
            }

            (AddEventUserRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<AddEventUserRequest>(req);
            if (error != null) return error;

            if (string.IsNullOrWhiteSpace(request!.UserEmail))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorEmailRequired });
            }

            // Validate role
            string? validRole = ValidateRole(request.Role);
            if (validRole == null)
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidRole });
            }

            // Only owners can add other owners
            if (validRole == Constants.RoleEventOwner && !claims.CanManageOwners)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorOnlyOwnerCanManageOwners });
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
                    Name = request.UserName?.Trim() ?? string.Empty,
                    Phone = string.Empty,
                    CreatedAt = DateTime.UtcNow
                };
                await _personRepository.AddAsync(person);
            }
            else if (!string.IsNullOrWhiteSpace(request.UserName) && string.IsNullOrWhiteSpace(person.Name))
            {
                // If person exists but has no name, and a name was provided, update it
                person.Name = request.UserName.Trim();
                await _personRepository.UpdateAsync(person);
            }

            // Check if they already have any event user role
            IEnumerable<EventRoleEntity> existingRoles = await _eventRoleRepository.GetByPersonAndEventAsync(person.PersonId, eventId);
            bool hasUserRole = existingRoles.Any(r =>
                r.Role == Constants.RoleEventOwner ||
                r.Role == Constants.RoleEventAdministrator ||
                r.Role == Constants.RoleEventContributor ||
                r.Role == Constants.RoleEventViewer ||
                r.Role == Constants.RoleEventAdmin);

            if (hasUserRole)
            {
                return new BadRequestObjectResult(new { message = "User already has a role in this event" });
            }

            // Create the role
            string roleId = Guid.NewGuid().ToString();
            EventRoleEntity eventRole = new EventRoleEntity
            {
                PartitionKey = person.PersonId,
                RowKey = EventRoleEntity.CreateRowKey(eventId, roleId),
                PersonId = person.PersonId,
                EventId = eventId,
                Role = validRole,
                AreaIdsJson = "[]",
                GrantedByPersonId = claims.PersonId,
                GrantedAt = DateTime.UtcNow
            };
            await _eventRoleRepository.AddAsync(eventRole);

            // Get granter's name for response
            PersonEntity? granter = await _personRepository.GetAsync(claims.PersonId);
            string granterName = granter?.Name ?? granter?.Email ?? "An administrator";

            EventUserResponse response = new(
                eventId,
                person.PersonId,
                email,
                person.Name ?? email,
                validRole,
                eventRole.GrantedAt,
                granter?.Name
            );

            _logger.LogInformation("User added to event {EventId}: {Email} with role {Role} (PersonId: {PersonId})",
                eventId, email, validRole, person.PersonId);

            // Send email notification to the added user
            try
            {
                // Get event name for the email
                EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);
                string eventName = eventEntity?.Name ?? "an event";

                // Build login URL
                string frontendUrl = FunctionHelpers.GetFrontendUrl(req);
                bool useHashRouting = FunctionHelpers.UsesHashRouting(req);
                string routePrefix = useHashRouting ? "/#" : "";
                string loginUrl = $"{frontendUrl}{routePrefix}/admin";

                // Get role description
                string roleDescription = GetRoleDescription(validRole);

                await _emailService.SendEventUserAddedEmailAsync(
                    email,
                    person.Name,
                    eventName,
                    validRole,
                    roleDescription,
                    granterName,
                    loginUrl
                );

                _logger.LogInformation("Notification email sent to {Email} for event {EventId}", email, eventId);
            }
            catch (Exception emailEx)
            {
                // Log the email failure but don't fail the request - the user was still added successfully
                _logger.LogWarning(emailEx, "Failed to send notification email to {Email} for event {EventId}", email, eventId);
            }

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding event user");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

#pragma warning disable MA0051
    [Function("UpdateEventUserRole")]
    public async Task<IActionResult> UpdateEventUserRole(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "events/{eventId}/users/{personId}")] HttpRequest req,
        string eventId,
        string personId)
    {
        try
        {
            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Must have elevated permissions
            if (!claims.CanUseElevatedPermissions)
            {
                return new ObjectResult(new { message = "Forbidden" }) { StatusCode = 403 };
            }

            // Must be able to manage users
            if (!claims.CanManageUsers)
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to manage users for this event" });
            }

            // Cannot change own role
            if (claims.PersonId == personId)
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorCannotChangeSelfRole });
            }

            (UpdateEventUserRoleRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<UpdateEventUserRoleRequest>(req);
            if (error != null) return error;

            // Validate new role
            string? newRole = ValidateRole(request!.Role);
            if (newRole == null)
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidRole });
            }

            // Get target user's current roles
            IEnumerable<EventRoleEntity> existingRoles = await _eventRoleRepository.GetByPersonAndEventAsync(personId, eventId);
            EventRoleEntity? currentRole = existingRoles.FirstOrDefault(r =>
                r.Role == Constants.RoleEventOwner ||
                r.Role == Constants.RoleEventAdministrator ||
                r.Role == Constants.RoleEventContributor ||
                r.Role == Constants.RoleEventViewer ||
                r.Role == Constants.RoleEventAdmin);

            if (currentRole == null)
            {
                return new NotFoundObjectResult(new { message = "User not found in this event" });
            }

            bool targetIsOwner = currentRole.Role == Constants.RoleEventOwner || currentRole.Role == Constants.RoleEventAdmin;
            bool changingToOwner = newRole == Constants.RoleEventOwner;
            bool changingFromOwner = targetIsOwner && newRole != Constants.RoleEventOwner;

            // Only owners can change owner roles
            if ((targetIsOwner || changingToOwner) && !claims.CanManageOwners)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorOnlyOwnerCanManageOwners });
            }

            // Cannot remove last owner
            if (changingFromOwner)
            {
                int ownerCount = await CountEventOwnersAsync(eventId);
                if (ownerCount <= 1)
                {
                    return new BadRequestObjectResult(new { message = Constants.ErrorCannotRemoveLastOwner });
                }
            }

            // Update the role
            currentRole.Role = newRole;
            currentRole.GrantedByPersonId = claims.PersonId;
            currentRole.GrantedAt = DateTime.UtcNow;
            await _eventRoleRepository.UpdateAsync(currentRole);

            // Get user info for response
            PersonEntity? person = await _personRepository.GetAsync(personId);
            PersonEntity? granter = await _personRepository.GetAsync(claims.PersonId);

            EventUserResponse response = new(
                eventId,
                personId,
                person?.Email ?? "",
                person?.Name ?? person?.Email ?? "",
                newRole,
                currentRole.GrantedAt,
                granter?.Name
            );

            _logger.LogInformation("User role updated in event {EventId}: {PersonId} to {Role}",
                eventId, personId, newRole);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating event user role");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    [Function("RemoveEventUser")]
    public async Task<IActionResult> RemoveEventUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{eventId}/users/{personId}")] HttpRequest req,
        string eventId,
        string personId)
    {
        try
        {
            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Must have elevated permissions
            if (!claims.CanUseElevatedPermissions)
            {
                return new ObjectResult(new { message = "Forbidden" }) { StatusCode = 403 };
            }

            // Must be able to manage users
            if (!claims.CanManageUsers)
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to manage users for this event" });
            }

            // Cannot remove self
            if (claims.PersonId == personId)
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorCannotRemoveSelf });
            }

            // Get target user's roles
            IEnumerable<EventRoleEntity> existingRoles = await _eventRoleRepository.GetByPersonAndEventAsync(personId, eventId);
            EventRoleEntity? currentRole = existingRoles.FirstOrDefault(r =>
                r.Role == Constants.RoleEventOwner ||
                r.Role == Constants.RoleEventAdministrator ||
                r.Role == Constants.RoleEventContributor ||
                r.Role == Constants.RoleEventViewer ||
                r.Role == Constants.RoleEventAdmin);

            if (currentRole == null)
            {
                return new NotFoundObjectResult(new { message = "User not found in this event" });
            }

            bool targetIsOwner = currentRole.Role == Constants.RoleEventOwner || currentRole.Role == Constants.RoleEventAdmin;

            // Only owners can remove other owners
            if (targetIsOwner && !claims.CanManageOwners)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorOnlyOwnerCanManageOwners });
            }

            // Cannot remove last owner
            if (targetIsOwner)
            {
                int ownerCount = await CountEventOwnersAsync(eventId);
                if (ownerCount <= 1)
                {
                    return new BadRequestObjectResult(new { message = Constants.ErrorCannotRemoveLastOwner });
                }
            }

            // Delete the role
            await _eventRoleRepository.DeleteAsync(personId, currentRole.RowKey);

            _logger.LogInformation("User removed from event {EventId}: {PersonId}",
                eventId, personId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing event user");
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
            // Check authorization via claims (supports sample codes)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            if (!claims.CanModifyEvent)
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
