using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Functions;

public class EventFunctions
{
    private readonly ILogger<EventFunctions> _logger;
    private readonly TableStorageService _tableStorage;

    public EventFunctions(ILogger<EventFunctions> logger, TableStorageService tableStorage)
    {
        _logger = logger;
        _tableStorage = tableStorage;
    }

    [Function("CreateEvent")]
    public async Task<IActionResult> CreateEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events")] HttpRequest req)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CreateEventRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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

            var eventEntity = new EventEntity
            {
                RowKey = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                EventDate = eventDateUtc,
                TimeZoneId = request.TimeZoneId,
                AdminEmail = request.AdminEmail,
                EmergencyContactsJson = JsonSerializer.Serialize(request.EmergencyContacts ?? new List<EmergencyContact>())
            };

            var table = _tableStorage.GetEventsTable();
            await table.AddEntityAsync(eventEntity);

            var emergencyContacts = JsonSerializer.Deserialize<List<EmergencyContact>>(eventEntity.EmergencyContactsJson)
                ?? new List<EmergencyContact>();

            var response = new EventResponse(
                eventEntity.RowKey,
                eventEntity.Name,
                eventEntity.Description,
                eventEntity.EventDate,
                eventEntity.TimeZoneId,
                eventEntity.AdminEmail,
                emergencyContacts,
                eventEntity.IsActive,
                eventEntity.CreatedDate
            );

            _logger.LogInformation($"Event created: {eventEntity.RowKey}");

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
            var table = _tableStorage.GetEventsTable();
            var eventEntity = await table.GetEntityAsync<EventEntity>("EVENT", eventId);

            var emergencyContacts = JsonSerializer.Deserialize<List<EmergencyContact>>(eventEntity.Value.EmergencyContactsJson)
                ?? new List<EmergencyContact>();

            var response = new EventResponse(
                eventEntity.Value.RowKey,
                eventEntity.Value.Name,
                eventEntity.Value.Description,
                eventEntity.Value.EventDate,
                eventEntity.Value.TimeZoneId,
                eventEntity.Value.AdminEmail,
                emergencyContacts,
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
            var table = _tableStorage.GetEventsTable();
            var events = new List<EventResponse>();

            await foreach (var eventEntity in table.QueryAsync<EventEntity>(e => e.PartitionKey == "EVENT"))
            {
                var emergencyContacts = JsonSerializer.Deserialize<List<EmergencyContact>>(eventEntity.EmergencyContactsJson)
                    ?? new List<EmergencyContact>();

                events.Add(new EventResponse(
                    eventEntity.RowKey,
                    eventEntity.Name,
                    eventEntity.Description,
                    eventEntity.EventDate,
                    eventEntity.TimeZoneId,
                    eventEntity.AdminEmail,
                    emergencyContacts,
                    eventEntity.IsActive,
                    eventEntity.CreatedDate
                ));
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
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CreateEventRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            var table = _tableStorage.GetEventsTable();
            var eventEntity = await table.GetEntityAsync<EventEntity>("EVENT", eventId);

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
            eventEntity.Value.EmergencyContactsJson = JsonSerializer.Serialize(request.EmergencyContacts ?? new List<EmergencyContact>());

            await table.UpdateEntityAsync(eventEntity.Value, eventEntity.Value.ETag);

            var emergencyContacts = JsonSerializer.Deserialize<List<EmergencyContact>>(eventEntity.Value.EmergencyContactsJson)
                ?? new List<EmergencyContact>();

            var response = new EventResponse(
                eventEntity.Value.RowKey,
                eventEntity.Value.Name,
                eventEntity.Value.Description,
                eventEntity.Value.EventDate,
                eventEntity.Value.TimeZoneId,
                eventEntity.Value.AdminEmail,
                emergencyContacts,
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
            var table = _tableStorage.GetEventsTable();
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
}
