using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Functions;

public class EventFunctions
{
    private readonly ILogger<EventFunctions> _logger;
    private readonly TableStorageService _tableStorage;
    private readonly GpxParserService _gpxParser;

    public EventFunctions(ILogger<EventFunctions> logger, TableStorageService tableStorage, GpxParserService gpxParser)
    {
        _logger = logger;
        _tableStorage = tableStorage;
        _gpxParser = gpxParser;
    }

    private async Task<bool> IsUserAuthorizedForEvent(string eventId, string userEmail)
    {
        try
        {
            TableClient mappingsTable = _tableStorage.GetUserEventMappingsTable();
            await mappingsTable.GetEntityAsync<UserEventMappingEntity>(eventId, userEmail);
            return true;
        }
        catch
        {
            return false;
        }
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

            TableClient table = _tableStorage.GetEventsTable();
            await table.AddEntityAsync(eventEntity);

            // Auto-create UserEventMapping for the creator
            UserEventMappingEntity mappingEntity = new()
            {
                PartitionKey = eventEntity.RowKey,
                RowKey = request.AdminEmail,
                EventId = eventEntity.RowKey,
                UserEmail = request.AdminEmail,
                Role = "Admin"
            };

            TableClient mappingsTable = _tableStorage.GetUserEventMappingsTable();
            await mappingsTable.AddEntityAsync(mappingEntity);

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
            TableClient table = _tableStorage.GetEventsTable();
            Response<EventEntity> eventEntity = await table.GetEntityAsync<EventEntity>("EVENT", eventId);

            List<EmergencyContact> emergencyContacts = JsonSerializer.Deserialize<List<EmergencyContact>>(eventEntity.Value.EmergencyContactsJson)
                ?? [];

            List<RoutePoint> route = JsonSerializer.Deserialize<List<RoutePoint>>(eventEntity.Value.GpxRouteJson)
                ?? [];

            EventResponse response = new(
                eventEntity.Value.RowKey,
                eventEntity.Value.Name,
                eventEntity.Value.Description,
                eventEntity.Value.EventDate,
                eventEntity.Value.TimeZoneId,
                eventEntity.Value.AdminEmail,
                emergencyContacts,
                route,
                eventEntity.Value.IsActive,
                eventEntity.Value.CreatedDate
            );

            return new OkObjectResult(response);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Event not found" });
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

            TableClient table = _tableStorage.GetEventsTable();
            TableClient mappingsTable = _tableStorage.GetUserEventMappingsTable();
            List<EventResponse> events = [];

            // Get all events where user is an admin
            HashSet<string> userEventIds = [];
            await foreach (UserEventMappingEntity mapping in mappingsTable.QueryAsync<UserEventMappingEntity>(m => m.RowKey == adminEmail))
            {
                userEventIds.Add(mapping.EventId);
            }

            // Fetch all events and filter by user access
            await foreach (EventEntity eventEntity in table.QueryAsync<EventEntity>(e => e.PartitionKey == "EVENT"))
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

            TableClient table = _tableStorage.GetEventsTable();
            Response<EventEntity> eventEntity = await table.GetEntityAsync<EventEntity>("EVENT", eventId);

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

            eventEntity.Value.Name = request.Name;
            eventEntity.Value.Description = request.Description;
            eventEntity.Value.EventDate = eventDateUtc;
            eventEntity.Value.TimeZoneId = request.TimeZoneId;
            eventEntity.Value.AdminEmail = request.AdminEmail;
            eventEntity.Value.EmergencyContactsJson = JsonSerializer.Serialize(request.EmergencyContacts ?? []);

            await table.UpdateEntityAsync(eventEntity.Value, eventEntity.Value.ETag);

            List<EmergencyContact> emergencyContacts = JsonSerializer.Deserialize<List<EmergencyContact>>(eventEntity.Value.EmergencyContactsJson)
                ?? [];

            List<RoutePoint> route = JsonSerializer.Deserialize<List<RoutePoint>>(eventEntity.Value.GpxRouteJson)
                ?? [];

            EventResponse response = new(
                eventEntity.Value.RowKey,
                eventEntity.Value.Name,
                eventEntity.Value.Description,
                eventEntity.Value.EventDate,
                eventEntity.Value.TimeZoneId,
                eventEntity.Value.AdminEmail,
                emergencyContacts,
                route,
                eventEntity.Value.IsActive,
                eventEntity.Value.CreatedDate
            );

            _logger.LogInformation($"Event updated: {eventId}");

            return new OkObjectResult(response);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Event not found" });
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

            TableClient table = _tableStorage.GetEventsTable();
            await table.DeleteEntityAsync("EVENT", eventId);

            _logger.LogInformation($"Event deleted: {eventId}");

            return new NoContentResult();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Event not found" });
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
            TableClient mappingsTable = _tableStorage.GetUserEventMappingsTable();
            List<UserEventMappingResponse> admins = [];

            await foreach (UserEventMappingEntity mapping in mappingsTable.QueryAsync<UserEventMappingEntity>(m => m.PartitionKey == eventId))
            {
                admins.Add(new UserEventMappingResponse(
                    mapping.EventId,
                    mapping.UserEmail,
                    mapping.Role,
                    mapping.CreatedDate
                ));
            }

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

            TableClient mappingsTable = _tableStorage.GetUserEventMappingsTable();

            // Check if mapping already exists
            try
            {
                await mappingsTable.GetEntityAsync<UserEventMappingEntity>(eventId, request.UserEmail);
                return new BadRequestObjectResult(new { message = "User is already an admin for this event" });
            }
            catch
            {
                // Mapping doesn't exist, create it
            }

            UserEventMappingEntity mappingEntity = new()
            {
                PartitionKey = eventId,
                RowKey = request.UserEmail,
                EventId = eventId,
                UserEmail = request.UserEmail,
                Role = "Admin"
            };

            await mappingsTable.AddEntityAsync(mappingEntity);

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

            TableClient mappingsTable = _tableStorage.GetUserEventMappingsTable();

            // Count total admins for this event
            int adminCount = 0;
            await foreach (UserEventMappingEntity mapping in mappingsTable.QueryAsync<UserEventMappingEntity>(m => m.PartitionKey == eventId))
            {
                adminCount++;
            }

            // Ensure at least one admin remains
            if (adminCount <= 1)
            {
                return new BadRequestObjectResult(new { message = "Cannot remove the last admin from an event" });
            }

            await mappingsTable.DeleteEntityAsync(eventId, userEmail);

            _logger.LogInformation($"Admin removed from event {eventId}: {userEmail}");

            return new NoContentResult();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Admin not found for this event" });
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
            TableClient table = _tableStorage.GetEventsTable();
            Response<EventEntity> eventEntity = await table.GetEntityAsync<EventEntity>("EVENT", eventId);

            string routeJson = JsonSerializer.Serialize(route);

            // Check if the route is too large
            if (System.Text.Encoding.UTF8.GetByteCount(routeJson) > 60000) // Leave some buffer under 64KB
            {
                return new BadRequestObjectResult(new { message = $"Route is too large ({route.Count} points). The GPX file has too many points even after simplification. Try a simpler route or fewer track points." });
            }

            eventEntity.Value.GpxRouteJson = routeJson;
            await table.UpdateEntityAsync(eventEntity.Value, eventEntity.Value.ETag);

            _logger.LogInformation($"GPX route uploaded for event {eventId}: {route.Count} points (simplified)");

            return new OkObjectResult(new { success = true, message = $"Route uploaded successfully with {route.Count} points (simplified)", route });
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Event not found" });
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
