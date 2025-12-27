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

public class CheckInFunctions
{
    private readonly ILogger<CheckInFunctions> _logger;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly ILocationRepository _locationRepository;

    public CheckInFunctions(
        ILogger<CheckInFunctions> logger,
        IAssignmentRepository assignmentRepository,
        ILocationRepository locationRepository)
    {
        _logger = logger;
        _assignmentRepository = assignmentRepository;
        _locationRepository = locationRepository;
    }

    [Function("CheckIn")]
    public async Task<IActionResult> CheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "checkin")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CheckInRequest? request = JsonSerializer.Deserialize<CheckInRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.EventId) || string.IsNullOrWhiteSpace(request.AssignmentId))
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            // Find the assignment using partition key query
            AssignmentEntity? assignment = await _assignmentRepository.GetAsync(request.EventId, request.AssignmentId);

            if (assignment == null)
            {
                return new NotFoundObjectResult(new { message = "Assignment not found" });
            }

            if (assignment.IsCheckedIn)
            {
                return new BadRequestObjectResult(new { message = "Already checked in" });
            }

            // Get the location to verify GPS if needed
            LocationEntity? location = await _locationRepository.GetAsync(assignment.EventId, assignment.LocationId);

            if (location == null)
            {
                return new NotFoundObjectResult(new { message = "Location not found" });
            }

            string checkInMethod;

            if (request.ManualCheckIn)
            {
                checkInMethod = "Manual";
            }
            else if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                // Verify GPS coordinates
                double distance = GpsService.CalculateDistance(
                    location.Latitude,
                    location.Longitude,
                    request.Latitude.Value,
                    request.Longitude.Value
                );

                if (distance > Constants.CheckInRadiusMeters)
                {
                    return new BadRequestObjectResult(new
                    {
                        message = $"You are {Math.Round(distance)}m away from the location. You must be within {Constants.CheckInRadiusMeters}m to check in.",
                        distance = Math.Round(distance),
                        allowedRadius = Constants.CheckInRadiusMeters
                    });
                }

                checkInMethod = "GPS";
            }
            else
            {
                return new BadRequestObjectResult(new { message = "Either provide GPS coordinates or request manual check-in" });
            }

            // Update assignment
            assignment.IsCheckedIn = true;
            assignment.CheckInTime = DateTime.UtcNow;
            assignment.CheckInLatitude = request.Latitude;
            assignment.CheckInLongitude = request.Longitude;
            assignment.CheckInMethod = checkInMethod;

            await _assignmentRepository.UpdateAsync(assignment);

            // Update location checked-in count
            location.CheckedInCount++;
            await _locationRepository.UpdateAsync(location);

            AssignmentResponse response = assignment.ToResponse();

            _logger.LogInformation($"Check-in successful: {assignment.RowKey} ({checkInMethod})");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during check-in");
            return new StatusCodeResult(500);
        }
    }

    [Function("AdminCheckIn")]
    public async Task<IActionResult> AdminCheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "checkin/admin/{eventId}/{assignmentId}")] HttpRequest req,
        string eventId,
        string assignmentId)
    {
        try
        {
            // Find the assignment using partition key query
            AssignmentEntity? assignment = await _assignmentRepository.GetAsync(eventId, assignmentId);

            if (assignment == null)
            {
                return new NotFoundObjectResult(new { message = "Assignment not found" });
            }

            // Get the location to update count
            LocationEntity? location = await _locationRepository.GetAsync(assignment.EventId, assignment.LocationId);

            if (location == null)
            {
                return new NotFoundObjectResult(new { message = "Location not found" });
            }

            // If already checked in, undo the check-in
            if (assignment.IsCheckedIn)
            {
                assignment.IsCheckedIn = false;
                assignment.CheckInTime = null;
                assignment.CheckInLatitude = null;
                assignment.CheckInLongitude = null;
                assignment.CheckInMethod = string.Empty;
                location.CheckedInCount--;
            }
            else
            {
                // Check in via admin
                assignment.IsCheckedIn = true;
                assignment.CheckInTime = DateTime.UtcNow;
                assignment.CheckInMethod = "Admin";
                location.CheckedInCount++;
            }

            await _assignmentRepository.UpdateAsync(assignment);
            await _locationRepository.UpdateAsync(location);

            AssignmentResponse response = assignment.ToResponse();

            _logger.LogInformation($"Admin check-in toggle: {assignment.RowKey} - Now {(assignment.IsCheckedIn ? "checked in" : "checked out")}");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during admin check-in");
            return new StatusCodeResult(500);
        }
    }
}
