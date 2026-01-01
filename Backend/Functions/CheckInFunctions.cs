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
    private readonly GpsService _gpsService;
    private readonly ClaimsService _claimsService;

    public CheckInFunctions(
        ILogger<CheckInFunctions> logger,
        IAssignmentRepository assignmentRepository,
        ILocationRepository locationRepository,
        GpsService gpsService,
        ClaimsService claimsService)
    {
        _logger = logger;
        _assignmentRepository = assignmentRepository;
        _locationRepository = locationRepository;
        _gpsService = gpsService;
        _claimsService = claimsService;
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
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidRequest });
            }

            // Find the assignment using partition key query
            AssignmentEntity? assignment = await _assignmentRepository.GetAsync(request.EventId, request.AssignmentId);

            if (assignment == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorAssignmentNotFound });
            }

            if (assignment.IsCheckedIn)
            {
                return new BadRequestObjectResult(new { message = "Already checked in" });
            }

            // Get the location to verify GPS if needed
            LocationEntity? location = await _locationRepository.GetAsync(assignment.EventId, assignment.LocationId);

            if (location == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorLocationNotFound });
            }

            string checkInMethod;

            if (request.ManualCheckIn)
            {
                checkInMethod = Constants.CheckInMethodManual;
            }
            else if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                // Verify GPS coordinates
                double distance = _gpsService.CalculateDistance(
                    location.Latitude,
                    location.Longitude,
                    request.Latitude.Value,
                    request.Longitude.Value
                );

                if (distance > Constants.CheckInRadiusMeters)
                {
                    _logger.LogWarning($"Check-in rejected: {Math.Round(distance)}m away from location (max {Constants.CheckInRadiusMeters}m) - Assignment: {request.AssignmentId}");
                    return new BadRequestObjectResult(new
                    {
                        message = $"You are {Math.Round(distance)}m away from the location. You must be within {Constants.CheckInRadiusMeters}m to check in.",
                        distance = Math.Round(distance),
                        allowedRadius = Constants.CheckInRadiusMeters
                    });
                }

                checkInMethod = Constants.CheckInMethodGps;
            }
            else
            {
                return new BadRequestObjectResult(new { message = "Either provide GPS coordinates or request manual check-in" });
            }

            // Prepare assignment update
            assignment.IsCheckedIn = true;
            assignment.CheckInTime = DateTime.UtcNow;
            assignment.CheckInLatitude = request.Latitude;
            assignment.CheckInLongitude = request.Longitude;
            assignment.CheckInMethod = checkInMethod;

            // Update assignment first
            await _assignmentRepository.UpdateAsync(assignment);

            // Update location checked-in count with rollback on failure
            try
            {
                location.CheckedInCount++;
                await _locationRepository.UpdateAsync(location);
            }
            catch (Exception locationEx)
            {
                // Attempt to rollback assignment change
                _logger.LogError(locationEx, "Failed to update location count, attempting rollback");
                try
                {
                    assignment.IsCheckedIn = false;
                    assignment.CheckInTime = null;
                    assignment.CheckInLatitude = null;
                    assignment.CheckInLongitude = null;
                    assignment.CheckInMethod = string.Empty;
                    await _assignmentRepository.UpdateAsync(assignment);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback assignment, data may be inconsistent");
                }
                throw; // Re-throw original exception
            }

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
            // Require authentication and admin permissions
            string? sessionToken = req.Cookies["session"] ?? req.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Must have elevated permissions (logged in via email, not magic code)
            if (!claims.CanUseElevatedPermissions)
            {
                return new ForbidResult();
            }

            // Must be event admin or system admin
            if (!claims.IsEventAdmin && !claims.IsSystemAdmin)
            {
                return new ForbidResult();
            }

            // Find the assignment using partition key query
            AssignmentEntity? assignment = await _assignmentRepository.GetAsync(eventId, assignmentId);

            if (assignment == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorAssignmentNotFound });
            }

            // Get the location to update count
            LocationEntity? location = await _locationRepository.GetAsync(assignment.EventId, assignment.LocationId);

            if (location == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorLocationNotFound });
            }

            // Store original state for rollback
            bool wasCheckedIn = assignment.IsCheckedIn;
            DateTime? originalCheckInTime = assignment.CheckInTime;
            double? originalLat = assignment.CheckInLatitude;
            double? originalLon = assignment.CheckInLongitude;
            string originalMethod = assignment.CheckInMethod;
            int originalCount = location.CheckedInCount;

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
                assignment.CheckInMethod = Constants.CheckInMethodAdmin;
                location.CheckedInCount++;
            }

            // Update assignment first
            await _assignmentRepository.UpdateAsync(assignment);

            // Update location with rollback on failure
            try
            {
                await _locationRepository.UpdateAsync(location);
            }
            catch (Exception locationEx)
            {
                // Attempt to rollback assignment change
                _logger.LogError(locationEx, "Failed to update location count in admin check-in, attempting rollback");
                try
                {
                    assignment.IsCheckedIn = wasCheckedIn;
                    assignment.CheckInTime = originalCheckInTime;
                    assignment.CheckInLatitude = originalLat;
                    assignment.CheckInLongitude = originalLon;
                    assignment.CheckInMethod = originalMethod;
                    await _assignmentRepository.UpdateAsync(assignment);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback assignment in admin check-in, data may be inconsistent");
                }
                throw;
            }

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
