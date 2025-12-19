using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Functions;

public class CheckInFunctions
{
    private readonly ILogger<CheckInFunctions> _logger;
    private readonly TableStorageService _tableStorage;
    private const double AllowedRadiusMeters = 100; // 100 meters tolerance

    public CheckInFunctions(ILogger<CheckInFunctions> logger, TableStorageService tableStorage)
    {
        _logger = logger;
        _tableStorage = tableStorage;
    }

    [Function("CheckIn")]
    public async Task<IActionResult> CheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "checkin")] HttpRequest req)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CheckInRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.AssignmentId))
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            var assignmentsTable = _tableStorage.GetAssignmentsTable();

            // Find the assignment
            AssignmentEntity? assignment = null;
            await foreach (var a in assignmentsTable.QueryAsync<AssignmentEntity>(a => a.RowKey == request.AssignmentId))
            {
                assignment = a;
                break;
            }

            if (assignment == null)
            {
                return new NotFoundObjectResult(new { message = "Assignment not found" });
            }

            if (assignment.IsCheckedIn)
            {
                return new BadRequestObjectResult(new { message = "Already checked in" });
            }

            // Get the location to verify GPS if needed
            var locationsTable = _tableStorage.GetLocationsTable();
            var location = await locationsTable.GetEntityAsync<LocationEntity>(assignment.EventId, assignment.LocationId);

            string checkInMethod;

            if (request.ManualCheckIn)
            {
                checkInMethod = "Manual";
            }
            else if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                // Verify GPS coordinates
                var distance = GpsService.CalculateDistance(
                    location.Value.Latitude,
                    location.Value.Longitude,
                    request.Latitude.Value,
                    request.Longitude.Value
                );

                if (distance > AllowedRadiusMeters)
                {
                    return new BadRequestObjectResult(new
                    {
                        message = $"You are {Math.Round(distance)}m away from the location. You must be within {AllowedRadiusMeters}m to check in.",
                        distance = Math.Round(distance),
                        allowedRadius = AllowedRadiusMeters
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

            await assignmentsTable.UpdateEntityAsync(assignment, assignment.ETag);

            // Update location checked-in count
            location.Value.CheckedInCount++;
            await locationsTable.UpdateEntityAsync(location.Value, location.Value.ETag);

            var response = new AssignmentResponse(
                assignment.RowKey,
                assignment.EventId,
                assignment.LocationId,
                assignment.MarshalName,
                assignment.IsCheckedIn,
                assignment.CheckInTime,
                assignment.CheckInLatitude,
                assignment.CheckInLongitude,
                assignment.CheckInMethod
            );

            _logger.LogInformation($"Check-in successful: {assignment.RowKey} ({checkInMethod})");

            return new OkObjectResult(response);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Assignment or location not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during check-in");
            return new StatusCodeResult(500);
        }
    }

    [Function("AdminCheckIn")]
    public async Task<IActionResult> AdminCheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "checkin/admin/{assignmentId}")] HttpRequest req,
        string assignmentId)
    {
        try
        {
            var assignmentsTable = _tableStorage.GetAssignmentsTable();

            // Find the assignment
            AssignmentEntity? assignment = null;
            await foreach (var a in assignmentsTable.QueryAsync<AssignmentEntity>(a => a.RowKey == assignmentId))
            {
                assignment = a;
                break;
            }

            if (assignment == null)
            {
                return new NotFoundObjectResult(new { message = "Assignment not found" });
            }

            // Get the location to update count
            var locationsTable = _tableStorage.GetLocationsTable();
            var location = await locationsTable.GetEntityAsync<LocationEntity>(assignment.EventId, assignment.LocationId);

            // If already checked in, undo the check-in
            if (assignment.IsCheckedIn)
            {
                assignment.IsCheckedIn = false;
                assignment.CheckInTime = null;
                assignment.CheckInLatitude = null;
                assignment.CheckInLongitude = null;
                assignment.CheckInMethod = string.Empty;
                location.Value.CheckedInCount--;
            }
            else
            {
                // Check in via admin
                assignment.IsCheckedIn = true;
                assignment.CheckInTime = DateTime.UtcNow;
                assignment.CheckInMethod = "Admin";
                location.Value.CheckedInCount++;
            }

            await assignmentsTable.UpdateEntityAsync(assignment, assignment.ETag);
            await locationsTable.UpdateEntityAsync(location.Value, location.Value.ETag);

            var response = new AssignmentResponse(
                assignment.RowKey,
                assignment.EventId,
                assignment.LocationId,
                assignment.MarshalName,
                assignment.IsCheckedIn,
                assignment.CheckInTime,
                assignment.CheckInLatitude,
                assignment.CheckInLongitude,
                assignment.CheckInMethod
            );

            _logger.LogInformation($"Admin check-in toggle: {assignment.RowKey} - Now {(assignment.IsCheckedIn ? "checked in" : "checked out")}");

            return new OkObjectResult(response);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Assignment not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during admin check-in");
            return new StatusCodeResult(500);
        }
    }
}
