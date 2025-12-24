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

namespace VolunteerCheckin.Functions.Functions;

public class EventFunctions
{
    private readonly ILogger<EventFunctions> _logger;
    private readonly IEventRepository _eventRepository;
    private readonly IUserEventMappingRepository _userEventMappingRepository;
    private readonly GpxParserService _gpxParser;

    public EventFunctions(
        ILogger<EventFunctions> logger,
        IEventRepository eventRepository,
        IUserEventMappingRepository userEventMappingRepository,
        GpxParserService gpxParser)
    {
        _logger = logger;
        _eventRepository = eventRepository;
        _userEventMappingRepository = userEventMappingRepository;
        _gpxParser = gpxParser;
    }

    private async Task<bool> IsUserAuthorizedForEvent(string eventId, string userEmail)
    {
        UserEventMappingEntity? mapping = await _userEventMappingRepository.GetAsync(eventId, userEmail);
        return mapping != null;
    }

    [Function("CreateEvent")]
    public async Task<IActionResult> CreateEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CreateEventRequest? request = JsonSerializer.Deserialize<CreateEventRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            // Convert the event date to UTC
            DateTime eventDateUtc;
            try
            {
                TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId);
                DateTime unspecifiedDateTime = DateTime.SpecifyKind(request.EventDate, DateTimeKind.Unspecified);
                eventDateUtc = TimeZoneInfo.ConvertTimeToUtc(unspecifiedDateTime, timeZone);
            }
            catch
            {
                // If timezone conversion fails, treat as UTC
                eventDateUtc = DateTime.SpecifyKind(request.EventDate, DateTimeKind.Utc);
            }

            EventEntity eventEntity = new()
            {
                RowKey = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                EventDate = eventDateUtc,
                TimeZoneId = request.TimeZoneId,
                AdminEmail = request.AdminEmail,
                EmergencyContactsJson = JsonSerializer.Serialize(request.EmergencyContacts ?? [])
            };

            await _eventRepository.AddAsync(eventEntity);

            // Auto-create UserEventMapping for the creator
            UserEventMappingEntity mappingEntity = new()
            {
                PartitionKey = eventEntity.RowKey,
                RowKey = request.AdminEmail,
                EventId = eventEntity.RowKey,
                UserEmail = request.AdminEmail,
                Role = "Admin"
            };

            await _userEventMappingRepository.AddAsync(mappingEntity);

            List<EmergencyContact> emergencyContacts = JsonSerializer.Deserialize<List<EmergencyContact>>(eventEntity.EmergencyContactsJson)
                ?? [];

            List<RoutePoint> route = JsonSerializer.Deserialize<List<RoutePoint>>(eventEntity.GpxRouteJson)
                ?? [];

            EventResponse response = new(
                eventEntity.RowKey,
                eventEntity.Name,
                eventEntity.Description,
                eventEntity.EventDate,
                eventEntity.TimeZoneId,
                eventEntity.AdminEmail,
                emergencyContacts,
                route,
                eventEntity.IsActive,
                eventEntity.CreatedDate
            );

            _logger.LogInformation($"Event created: {eventEntity.RowKey}, Admin: {request.AdminEmail}");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating event");
            return new StatusCodeResult(500);
        }
    }

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

            List<EmergencyContact> emergencyContacts = JsonSerializer.Deserialize<List<EmergencyContact>>(eventEntity.EmergencyContactsJson)
                ?? [];

            List<RoutePoint> route = JsonSerializer.Deserialize<List<RoutePoint>>(eventEntity.GpxRouteJson)
                ?? [];

            EventResponse response = new(
                eventEntity.RowKey,
                eventEntity.Name,
                eventEntity.Description,
                eventEntity.EventDate,
                eventEntity.TimeZoneId,
                eventEntity.AdminEmail,
                emergencyContacts,
                route,
                eventEntity.IsActive,
                eventEntity.CreatedDate
            );

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
            string? adminEmail = req.Headers["X-Admin-Email"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return new BadRequestObjectResult(new { message = "Admin email header is required" });
            }

            // Get all events where user is an admin
            IEnumerable<UserEventMappingEntity> userMappings = await _userEventMappingRepository.GetByUserAsync(adminEmail);
            HashSet<string> userEventIds = userMappings.Select(m => m.EventId).ToHashSet();

            // Fetch all events and filter by user access
            IEnumerable<EventEntity> allEvents = await _eventRepository.GetAllAsync();
            List<EventResponse> events = [];

            foreach (EventEntity eventEntity in allEvents)
            {
                if (userEventIds.Contains(eventEntity.RowKey))
                {
                    List<EmergencyContact> emergencyContacts = JsonSerializer.Deserialize<List<EmergencyContact>>(eventEntity.EmergencyContactsJson)
                        ?? [];

                    List<RoutePoint> route = JsonSerializer.Deserialize<List<RoutePoint>>(eventEntity.GpxRouteJson)
                        ?? [];

                    events.Add(new EventResponse(
                        eventEntity.RowKey,
                        eventEntity.Name,
                        eventEntity.Description,
                        eventEntity.EventDate,
                        eventEntity.TimeZoneId,
                        eventEntity.AdminEmail,
                        emergencyContacts,
                        route,
                        eventEntity.IsActive,
                        eventEntity.CreatedDate
                    ));
                }
            }

            return new OkObjectResult(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting events");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateEvent")]
    public async Task<IActionResult> UpdateEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "events/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Check authorization
            string? adminEmail = req.Headers["X-Admin-Email"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return new BadRequestObjectResult(new { message = "Admin email header is required" });
            }

            if (!await IsUserAuthorizedForEvent(eventId, adminEmail))
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to update this event" });
            }

            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CreateEventRequest? request = JsonSerializer.Deserialize<CreateEventRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);

            if (eventEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Event not found" });
            }

            // Convert the event date to UTC
            DateTime eventDateUtc;
            try
            {
                TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId);
                DateTime unspecifiedDateTime = DateTime.SpecifyKind(request.EventDate, DateTimeKind.Unspecified);
                eventDateUtc = TimeZoneInfo.ConvertTimeToUtc(unspecifiedDateTime, timeZone);
            }
            catch
            {
                // If timezone conversion fails, treat as UTC
                eventDateUtc = DateTime.SpecifyKind(request.EventDate, DateTimeKind.Utc);
            }

            eventEntity.Name = request.Name;
            eventEntity.Description = request.Description;
            eventEntity.EventDate = eventDateUtc;
            eventEntity.TimeZoneId = request.TimeZoneId;
            eventEntity.AdminEmail = request.AdminEmail;
            eventEntity.EmergencyContactsJson = JsonSerializer.Serialize(request.EmergencyContacts ?? []);

            await _eventRepository.UpdateAsync(eventEntity);

            List<EmergencyContact> emergencyContacts = JsonSerializer.Deserialize<List<EmergencyContact>>(eventEntity.EmergencyContactsJson)
                ?? [];

            List<RoutePoint> route = JsonSerializer.Deserialize<List<RoutePoint>>(eventEntity.GpxRouteJson)
                ?? [];

            EventResponse response = new(
                eventEntity.RowKey,
                eventEntity.Name,
                eventEntity.Description,
                eventEntity.EventDate,
                eventEntity.TimeZoneId,
                eventEntity.AdminEmail,
                emergencyContacts,
                route,
                eventEntity.IsActive,
                eventEntity.CreatedDate
            );

            _logger.LogInformation($"Event updated: {eventId}");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating event");
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteEvent")]
    public async Task<IActionResult> DeleteEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Check authorization
            string? adminEmail = req.Headers["X-Admin-Email"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return new BadRequestObjectResult(new { message = "Admin email header is required" });
            }

            if (!await IsUserAuthorizedForEvent(eventId, adminEmail))
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to delete this event" });
            }

            await _eventRepository.DeleteAsync(eventId);

            _logger.LogInformation($"Event deleted: {eventId}");

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting event");
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
            IEnumerable<UserEventMappingEntity> mappings = await _userEventMappingRepository.GetByEventAsync(eventId);

            List<UserEventMappingResponse> admins = mappings.Select(mapping => new UserEventMappingResponse(
                mapping.EventId,
                mapping.UserEmail,
                mapping.Role,
                mapping.CreatedDate
            )).ToList();

            return new OkObjectResult(admins);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event admins");
            return new StatusCodeResult(500);
        }
    }

    [Function("AddEventAdmin")]
    public async Task<IActionResult> AddEventAdmin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/admins")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Check authorization
            string? adminEmail = req.Headers["X-Admin-Email"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return new BadRequestObjectResult(new { message = "Admin email header is required" });
            }

            if (!await IsUserAuthorizedForEvent(eventId, adminEmail))
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to manage admins for this event" });
            }

            string body = await new StreamReader(req.Body).ReadToEndAsync();
            AddEventAdminRequest? request = JsonSerializer.Deserialize<AddEventAdminRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.UserEmail))
            {
                return new BadRequestObjectResult(new { message = "User email is required" });
            }

            // Check if mapping already exists
            UserEventMappingEntity? existingMapping = await _userEventMappingRepository.GetAsync(eventId, request.UserEmail);
            if (existingMapping != null)
            {
                return new BadRequestObjectResult(new { message = "User is already an admin for this event" });
            }

            UserEventMappingEntity mappingEntity = new()
            {
                PartitionKey = eventId,
                RowKey = request.UserEmail,
                EventId = eventId,
                UserEmail = request.UserEmail,
                Role = "Admin"
            };

            await _userEventMappingRepository.AddAsync(mappingEntity);

            UserEventMappingResponse response = new(
                mappingEntity.EventId,
                mappingEntity.UserEmail,
                mappingEntity.Role,
                mappingEntity.CreatedDate
            );

            _logger.LogInformation($"Admin added to event {eventId}: {request.UserEmail}");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding event admin");
            return new StatusCodeResult(500);
        }
    }

    [Function("RemoveEventAdmin")]
    public async Task<IActionResult> RemoveEventAdmin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{eventId}/admins/{userEmail}")] HttpRequest req,
        string eventId,
        string userEmail)
    {
        try
        {
            // Check authorization
            string? adminEmail = req.Headers["X-Admin-Email"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return new BadRequestObjectResult(new { message = "Admin email header is required" });
            }

            if (!await IsUserAuthorizedForEvent(eventId, adminEmail))
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to manage admins for this event" });
            }

            // Count total admins for this event
            IEnumerable<UserEventMappingEntity> mappings = await _userEventMappingRepository.GetByEventAsync(eventId);
            int adminCount = mappings.Count();

            // Ensure at least one admin remains
            if (adminCount <= 1)
            {
                return new BadRequestObjectResult(new { message = "Cannot remove the last admin from an event" });
            }

            await _userEventMappingRepository.DeleteAsync(eventId, userEmail);

            _logger.LogInformation($"Admin removed from event {eventId}: {userEmail}");

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing event admin");
            return new StatusCodeResult(500);
        }
    }

    [Function("UploadGpx")]
    public async Task<IActionResult> UploadGpx(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/upload-gpx")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Check authorization
            string? adminEmail = req.Headers["X-Admin-Email"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return new BadRequestObjectResult(new { message = "Admin email header is required" });
            }

            if (!await IsUserAuthorizedForEvent(eventId, adminEmail))
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
            using (Stream stream = gpxFile.OpenReadStream())
            {
                route = _gpxParser.ParseGpxFile(stream);
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

            _logger.LogInformation($"GPX route uploaded for event {eventId}: {route.Count} points (simplified)");

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
}
