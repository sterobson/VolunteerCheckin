using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure;
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
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CreateAssignmentRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            var assignmentEntity = new AssignmentEntity
            {
                PartitionKey = request.EventId,
                RowKey = Guid.NewGuid().ToString(),
                EventId = request.EventId,
                LocationId = request.LocationId,
                MarshalName = request.MarshalName
            };

            var table = _tableStorage.GetAssignmentsTable();
            await table.AddEntityAsync(assignmentEntity);

            var response = new AssignmentResponse(
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
            var table = _tableStorage.GetAssignmentsTable();
            var assignmentEntity = await table.GetEntityAsync<AssignmentEntity>(eventId, assignmentId);

            var response = new AssignmentResponse(
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
            var table = _tableStorage.GetAssignmentsTable();
            var assignments = new List<AssignmentResponse>();

            await foreach (var assignmentEntity in table.QueryAsync<AssignmentEntity>(a => a.PartitionKey == eventId))
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
            var table = _tableStorage.GetAssignmentsTable();
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
            var locationsTable = _tableStorage.GetLocationsTable();
            var assignmentsTable = _tableStorage.GetAssignmentsTable();

            var locations = new List<LocationEntity>();
            await foreach (var location in locationsTable.QueryAsync<LocationEntity>(l => l.PartitionKey == eventId))
            {
                locations.Add(location);
            }

            var assignments = new List<AssignmentEntity>();
            await foreach (var assignment in assignmentsTable.QueryAsync<AssignmentEntity>(a => a.PartitionKey == eventId))
            {
                assignments.Add(assignment);
            }

            var locationStatuses = new List<LocationStatusResponse>();

            foreach (var location in locations)
            {
                var locationAssignments = assignments
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

                var checkedInCount = locationAssignments.Count(a => a.IsCheckedIn);

                locationStatuses.Add(new LocationStatusResponse(
                    location.RowKey,
                    location.Name,
                    location.Latitude,
                    location.Longitude,
                    location.RequiredMarshals,
                    checkedInCount,
                    locationAssignments
                ));
            }

            var response = new EventStatusResponse(eventId, locationStatuses);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event status");
            return new StatusCodeResult(500);
        }
    }
}
