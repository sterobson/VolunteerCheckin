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

public class AssignmentFunctions
{
    private readonly ILogger<AssignmentFunctions> _logger;
    private readonly TableStorageService _tableStorage;

    public AssignmentFunctions(ILogger<AssignmentFunctions> logger, TableStorageService tableStorage)
    {
        _logger = logger;
        _tableStorage = tableStorage;
    }

    [Function("CreateAssignment")]
    public async Task<IActionResult> CreateAssignment(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "assignments")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CreateAssignmentRequest? request = JsonSerializer.Deserialize<CreateAssignmentRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            TableClient marshalsTable = _tableStorage.GetMarshalsTable();
            string marshalId;
            string marshalName;

            // If MarshalId is provided, use the existing marshal
            if (!string.IsNullOrEmpty(request.MarshalId))
            {
                Response<MarshalEntity> marshal = await marshalsTable.GetEntityAsync<MarshalEntity>(request.EventId, request.MarshalId);
                marshalId = marshal.Value.MarshalId;
                marshalName = marshal.Value.Name;
            }
            // If MarshalName is provided, find or create a marshal
            else if (!string.IsNullOrEmpty(request.MarshalName))
            {
                // Try to find existing marshal by name
                MarshalEntity? existingMarshal = null;
                await foreach (MarshalEntity m in marshalsTable.QueryAsync<MarshalEntity>(
                    m => m.PartitionKey == request.EventId))
                {
                    if (m.Name.Equals(request.MarshalName, StringComparison.OrdinalIgnoreCase))
                    {
                        existingMarshal = m;
                        break;
                    }
                }

                if (existingMarshal != null)
                {
                    marshalId = existingMarshal.MarshalId;
                    marshalName = existingMarshal.Name;
                }
                else
                {
                    // Create new marshal
                    marshalId = Guid.NewGuid().ToString();
                    MarshalEntity newMarshal = new()
                    {
                        PartitionKey = request.EventId,
                        RowKey = marshalId,
                        EventId = request.EventId,
                        MarshalId = marshalId,
                        Name = request.MarshalName
                    };
                    await marshalsTable.AddEntityAsync(newMarshal);
                    marshalName = request.MarshalName;
                    _logger.LogInformation($"Created new marshal: {marshalId} - {marshalName}");
                }
            }
            else
            {
                return new BadRequestObjectResult(new { message = "Either MarshalId or MarshalName must be provided" });
            }

            AssignmentEntity assignmentEntity = new()
            {
                PartitionKey = request.EventId,
                RowKey = Guid.NewGuid().ToString(),
                EventId = request.EventId,
                LocationId = request.LocationId,
                MarshalId = marshalId,
                MarshalName = marshalName
            };

            TableClient table = _tableStorage.GetAssignmentsTable();
            await table.AddEntityAsync(assignmentEntity);

            AssignmentResponse response = new(
                assignmentEntity.RowKey,
                assignmentEntity.EventId,
                assignmentEntity.LocationId,
                assignmentEntity.MarshalName,
                assignmentEntity.IsCheckedIn,
                assignmentEntity.CheckInTime,
                assignmentEntity.CheckInLatitude,
                assignmentEntity.CheckInLongitude,
                assignmentEntity.CheckInMethod
            );

            _logger.LogInformation($"Assignment created: {assignmentEntity.RowKey}");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating assignment");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetAssignment")]
    public async Task<IActionResult> GetAssignment(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "assignments/{eventId}/{assignmentId}")] HttpRequest req,
        string eventId,
        string assignmentId)
    {
        try
        {
            TableClient table = _tableStorage.GetAssignmentsTable();
            Response<AssignmentEntity> assignmentEntity = await table.GetEntityAsync<AssignmentEntity>(eventId, assignmentId);

            AssignmentResponse response = new(
                assignmentEntity.Value.RowKey,
                assignmentEntity.Value.EventId,
                assignmentEntity.Value.LocationId,
                assignmentEntity.Value.MarshalName,
                assignmentEntity.Value.IsCheckedIn,
                assignmentEntity.Value.CheckInTime,
                assignmentEntity.Value.CheckInLatitude,
                assignmentEntity.Value.CheckInLongitude,
                assignmentEntity.Value.CheckInMethod
            );

            return new OkObjectResult(response);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Assignment not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assignment");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetAssignmentsByEvent")]
    public async Task<IActionResult> GetAssignmentsByEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "assignments/event/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            TableClient table = _tableStorage.GetAssignmentsTable();
            List<AssignmentResponse> assignments = [];

            await foreach (AssignmentEntity assignmentEntity in table.QueryAsync<AssignmentEntity>(a => a.PartitionKey == eventId))
            {
                assignments.Add(new AssignmentResponse(
                    assignmentEntity.RowKey,
                    assignmentEntity.EventId,
                    assignmentEntity.LocationId,
                    assignmentEntity.MarshalName,
                    assignmentEntity.IsCheckedIn,
                    assignmentEntity.CheckInTime,
                    assignmentEntity.CheckInLatitude,
                    assignmentEntity.CheckInLongitude,
                    assignmentEntity.CheckInMethod
                ));
            }

            return new OkObjectResult(assignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assignments");
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteAssignment")]
    public async Task<IActionResult> DeleteAssignment(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "assignments/{eventId}/{assignmentId}")] HttpRequest req,
        string eventId,
        string assignmentId)
    {
        try
        {
            TableClient table = _tableStorage.GetAssignmentsTable();
            await table.DeleteEntityAsync(eventId, assignmentId);

            _logger.LogInformation($"Assignment deleted: {assignmentId}");

            return new NoContentResult();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Assignment not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting assignment");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetEventStatus")]
    public async Task<IActionResult> GetEventStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/status")] HttpRequest req,
        string eventId)
    {
        try
        {
            TableClient locationsTable = _tableStorage.GetLocationsTable();
            TableClient assignmentsTable = _tableStorage.GetAssignmentsTable();

            List<LocationEntity> locations = [];
            await foreach (LocationEntity location in locationsTable.QueryAsync<LocationEntity>(l => l.PartitionKey == eventId))
            {
                locations.Add(location);
            }

            List<AssignmentEntity> assignments = [];
            await foreach (AssignmentEntity assignment in assignmentsTable.QueryAsync<AssignmentEntity>(a => a.PartitionKey == eventId))
            {
                assignments.Add(assignment);
            }

            List<LocationStatusResponse> locationStatuses = [];

            foreach (LocationEntity location in locations)
            {
                List<AssignmentResponse> locationAssignments = assignments
                    .Where(a => a.LocationId == location.RowKey)
                    .Select(a => new AssignmentResponse(
                        a.RowKey,
                        a.EventId,
                        a.LocationId,
                        a.MarshalName,
                        a.IsCheckedIn,
                        a.CheckInTime,
                        a.CheckInLatitude,
                        a.CheckInLongitude,
                        a.CheckInMethod
                    ))
                    .ToList();

                int checkedInCount = locationAssignments.Count(a => a.IsCheckedIn);

                locationStatuses.Add(new LocationStatusResponse(
                    location.RowKey,
                    location.Name,
                    location.Description,
                    location.Latitude,
                    location.Longitude,
                    location.RequiredMarshals,
                    checkedInCount,
                    locationAssignments,
                    location.What3Words
                ));
            }

            EventStatusResponse response = new(eventId, locationStatuses);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event status");
            return new StatusCodeResult(500);
        }
    }
}
