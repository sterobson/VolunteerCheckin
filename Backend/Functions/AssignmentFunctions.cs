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

public class AssignmentFunctions
{
    private readonly ILogger<AssignmentFunctions> _logger;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly ILocationRepository _locationRepository;

    public AssignmentFunctions(
        ILogger<AssignmentFunctions> logger,
        IAssignmentRepository assignmentRepository,
        IMarshalRepository marshalRepository,
        ILocationRepository locationRepository)
    {
        _logger = logger;
        _assignmentRepository = assignmentRepository;
        _marshalRepository = marshalRepository;
        _locationRepository = locationRepository;
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

            string marshalId;
            string marshalName;

            // If MarshalId is provided, use the existing marshal
            if (!string.IsNullOrEmpty(request.MarshalId))
            {
                MarshalEntity? marshal = await _marshalRepository.GetAsync(request.EventId, request.MarshalId);
                if (marshal == null)
                {
                    return new BadRequestObjectResult(new { message = "Marshal not found" });
                }
                marshalId = marshal.MarshalId;
                marshalName = marshal.Name;
            }
            // If MarshalName is provided, find or create a marshal
            else if (!string.IsNullOrEmpty(request.MarshalName))
            {
                // Try to find existing marshal by name
                MarshalEntity? existingMarshal = await _marshalRepository.FindByNameAsync(request.EventId, request.MarshalName);

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
                    await _marshalRepository.AddAsync(newMarshal);
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

            await _assignmentRepository.AddAsync(assignmentEntity);

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
            IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByEventAsync(eventId);
            AssignmentEntity? assignmentEntity = assignments.FirstOrDefault(a => a.RowKey == assignmentId);

            if (assignmentEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Assignment not found" });
            }

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

            return new OkObjectResult(response);
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
            IEnumerable<AssignmentEntity> assignmentEntities = await _assignmentRepository.GetByEventAsync(eventId);

            List<AssignmentResponse> assignments = assignmentEntities.Select(assignmentEntity => new AssignmentResponse(
                assignmentEntity.RowKey,
                assignmentEntity.EventId,
                assignmentEntity.LocationId,
                assignmentEntity.MarshalName,
                assignmentEntity.IsCheckedIn,
                assignmentEntity.CheckInTime,
                assignmentEntity.CheckInLatitude,
                assignmentEntity.CheckInLongitude,
                assignmentEntity.CheckInMethod
            )).ToList();

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
            await _assignmentRepository.DeleteAsync(eventId, assignmentId);

            _logger.LogInformation($"Assignment deleted: {assignmentId}");

            return new NoContentResult();
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
            IEnumerable<LocationEntity> locations = await _locationRepository.GetByEventAsync(eventId);
            IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByEventAsync(eventId);

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
                    location.What3Words,
                    location.StartTime,
                    location.EndTime
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
