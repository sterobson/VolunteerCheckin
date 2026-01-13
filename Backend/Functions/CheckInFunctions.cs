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

#pragma warning disable MA0051
    [Function("CheckIn")]
    public async Task<IActionResult> CheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "checkin")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CheckInRequest? request = JsonSerializer.Deserialize<CheckInRequest>(body, FunctionHelpers.JsonOptions);

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
                    _logger.LogWarning("Check-in rejected: {Distance}m away from location (max {MaxDistance}m) - Assignment: {AssignmentId}", Math.Round(distance), Constants.CheckInRadiusMeters, request.AssignmentId);
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
            assignment.CheckedInBy = assignment.MarshalName; // Self check-in via marshal link

            // Update assignment first
            await _assignmentRepository.UpdateAsync(assignment);

            // Update location checked-in count with retry logic
            location.CheckedInCount++;
            bool locationUpdated = await FunctionHelpers.ExecuteWithRetryAsync(
                () => _locationRepository.UpdateAsync(location),
                maxRetries: 3,
                baseDelayMs: 100
            );

            if (!locationUpdated)
            {
                // Rollback assignment with retry logic
                _logger.LogError("Failed to update location count after retries, attempting rollback");
                assignment.IsCheckedIn = false;
                assignment.CheckInTime = null;
                assignment.CheckInLatitude = null;
                assignment.CheckInLongitude = null;
                assignment.CheckInMethod = string.Empty;
                assignment.CheckedInBy = string.Empty;

                bool rollbackSucceeded = await FunctionHelpers.ExecuteWithRetryAsync(
                    () => _assignmentRepository.UpdateAsync(assignment),
                    maxRetries: 3,
                    baseDelayMs: 100
                );

                if (!rollbackSucceeded)
                {
                    _logger.LogCritical("CRITICAL: Failed to rollback assignment after location update failure. Data inconsistency for assignment {AssignmentId}", assignment.RowKey);
                }

                return new StatusCodeResult(500);
            }

            AssignmentResponse response = assignment.ToResponse();

            _logger.LogInformation("Check-in successful: {AssignmentId} ({CheckInMethod}) by {CheckedInBy}",
                assignment.RowKey, checkInMethod, assignment.CheckedInBy);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during check-in");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

/// <summary>
    /// Toggle check-in status for marshals (self) and area leads (marshals in their area).
    /// POST /api/checkin/{eventId}/{assignmentId}/toggle
    /// Optionally accepts GPS coordinates in request body for GPS-based check-in.
    /// </summary>
#pragma warning disable MA0051
    [Function("ToggleCheckIn")]
    public async Task<IActionResult> ToggleCheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "checkin/{eventId}/{assignmentId}/toggle")] HttpRequest req,
        string eventId,
        string assignmentId)
    {
        try
        {
            // Parse optional request body for GPS coordinates
            ToggleCheckInRequest? request = null;
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    request = JsonSerializer.Deserialize<ToggleCheckInRequest>(body, FunctionHelpers.JsonOptions);
                }
            }
            catch
            {
                // Ignore parse errors - body is optional
            }

            // Require authentication
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

            // Find the assignment
            AssignmentEntity? assignment = await _assignmentRepository.GetAsync(eventId, assignmentId);
            if (assignment == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorAssignmentNotFound });
            }

            // Get the location to update count and validate GPS
            LocationEntity? location = await _locationRepository.GetAsync(assignment.EventId, assignment.LocationId);
            if (location == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorLocationNotFound });
            }

            // Check authorization: must be the marshal themselves OR an area lead for this checkpoint's area
            bool isAuthorized = false;
            bool isSelfCheckIn = false;
            string checkInMethod = Constants.CheckInMethodManual;

            // Check if this is the marshal's own assignment
            if (!string.IsNullOrEmpty(claims.MarshalId) && assignment.MarshalId == claims.MarshalId)
            {
                isAuthorized = true;
                isSelfCheckIn = true;
                checkInMethod = Constants.CheckInMethodManual;
            }
            else
            {
                // Check if user is an area lead for this checkpoint's area
                List<string> areaLeadAreaIds = [.. claims.EventRoles
                    .Where(r => r.Role == Constants.RoleEventAreaLead)
                    .SelectMany(r => r.AreaIds)];

                if (areaLeadAreaIds.Count > 0 && !string.IsNullOrEmpty(location.AreaIdsJson))
                {
                    List<string> checkpointAreaIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(location.AreaIdsJson) ?? [];
                    isAuthorized = checkpointAreaIds.Any(areaId => areaLeadAreaIds.Contains(areaId));
                    if (isAuthorized)
                    {
                        checkInMethod = Constants.CheckInMethodAreaLead;
                    }
                }
            }

            if (!isAuthorized)
            {
                _logger.LogWarning("Toggle check-in unauthorized: ClaimsMarshalId={ClaimsMarshalId}, AssignmentMarshalId={AssignmentMarshalId}, AssignmentId={AssignmentId}",
                    claims.MarshalId ?? "(null)", assignment.MarshalId, assignmentId);
                return new UnauthorizedObjectResult(new { message = "You are not authorized to check in this assignment. The assignment may belong to a different marshal." });
            }

            // For self check-in with GPS coordinates, record them but don't enforce distance
            double? checkInLatitude = null;
            double? checkInLongitude = null;

            if (isSelfCheckIn && !assignment.IsCheckedIn && request?.Latitude != null && request?.Longitude != null)
            {
                checkInMethod = Constants.CheckInMethodGps;
                checkInLatitude = request.Latitude;
                checkInLongitude = request.Longitude;
            }

            // Determine desired action: explicit action or toggle
            string? requestedAction = request?.Action?.ToLowerInvariant();

            // Handle 'refresh' action - re-check-in to update timestamp (for stale check-ins)
            bool isRefresh = requestedAction == "refresh";
            if (isRefresh && assignment.IsCheckedIn)
            {
                _logger.LogInformation("Refreshing stale check-in for assignment {AssignmentId}", assignmentId);
                // Update the check-in time and GPS data
                assignment.CheckInTime = DateTime.UtcNow;
                assignment.CheckInLatitude = checkInLatitude;
                assignment.CheckInLongitude = checkInLongitude;
                assignment.CheckInMethod = checkInMethod;
                assignment.CheckedInBy = !string.IsNullOrEmpty(claims.PersonName) ? claims.PersonName : claims.PersonEmail;

                await _assignmentRepository.UpdateAsync(assignment);
                return new OkObjectResult(assignment.ToResponse());
            }

            bool wantToCheckIn = requestedAction switch
            {
                "check-in" or "checkin" or "refresh" => true,
                "check-out" or "checkout" => false,
                _ => !assignment.IsCheckedIn // Toggle: do the opposite of current state
            };

            // If already in desired state, return success without changes (idempotent)
            if (assignment.IsCheckedIn == wantToCheckIn)
            {
                _logger.LogInformation("Toggle check-in: {AssignmentId} already in desired state ({State})",
                    assignment.RowKey, wantToCheckIn ? "checked in" : "checked out");
                return new OkObjectResult(assignment.ToResponse());
            }

            // Determine who is performing the check-in (name if available, else email)
            string checkedInBy = !string.IsNullOrEmpty(claims.PersonName) ? claims.PersonName : claims.PersonEmail;

            // Store original state for rollback
            bool wasCheckedIn = assignment.IsCheckedIn;
            DateTime? originalCheckInTime = assignment.CheckInTime;
            double? originalLat = assignment.CheckInLatitude;
            double? originalLon = assignment.CheckInLongitude;
            string originalMethod = assignment.CheckInMethod;
            string originalCheckedInBy = assignment.CheckedInBy;

            // Apply the state change
            if (wantToCheckIn)
            {
                assignment.IsCheckedIn = true;
                assignment.CheckInTime = DateTime.UtcNow;
                assignment.CheckInLatitude = checkInLatitude;
                assignment.CheckInLongitude = checkInLongitude;
                assignment.CheckInMethod = checkInMethod;
                assignment.CheckedInBy = checkedInBy;
                location.CheckedInCount++;
            }
            else
            {
                assignment.IsCheckedIn = false;
                assignment.CheckInTime = null;
                assignment.CheckInLatitude = null;
                assignment.CheckInLongitude = null;
                assignment.CheckInMethod = string.Empty;
                assignment.CheckedInBy = string.Empty;
                location.CheckedInCount--;
            }

            // Update assignment first
            await _assignmentRepository.UpdateAsync(assignment);

            // Update location with retry logic
            bool locationUpdated = await FunctionHelpers.ExecuteWithRetryAsync(
                () => _locationRepository.UpdateAsync(location),
                maxRetries: 3,
                baseDelayMs: 100
            );

            if (!locationUpdated)
            {
                // Rollback assignment
                _logger.LogError("Failed to update location count in toggle check-in after retries, attempting rollback");
                assignment.IsCheckedIn = wasCheckedIn;
                assignment.CheckInTime = originalCheckInTime;
                assignment.CheckInLatitude = originalLat;
                assignment.CheckInLongitude = originalLon;
                assignment.CheckInMethod = originalMethod;
                assignment.CheckedInBy = originalCheckedInBy;

                bool rollbackSucceeded = await FunctionHelpers.ExecuteWithRetryAsync(
                    () => _assignmentRepository.UpdateAsync(assignment),
                    maxRetries: 3,
                    baseDelayMs: 100
                );

                if (!rollbackSucceeded)
                {
                    _logger.LogCritical("CRITICAL: Failed to rollback assignment in toggle check-in. Data inconsistency for assignment {AssignmentId}", assignment.RowKey);
                }

                return new StatusCodeResult(500);
            }

            AssignmentResponse response = assignment.ToResponse();

            _logger.LogInformation("Toggle check-in: {AssignmentId} by {Method} - Now {CheckInStatus}",
                assignment.RowKey, checkInMethod, assignment.IsCheckedIn ? "checked in" : "checked out");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during toggle check-in");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

#pragma warning disable MA0051
    [Function("AdminCheckIn")]
    public async Task<IActionResult> AdminCheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "checkin/admin/{eventId}/{assignmentId}")] HttpRequest req,
        string eventId,
        string assignmentId)
    {
        try
        {
            // Require authentication and admin permissions
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

            // Check authorization: must be admin OR area lead for this checkpoint's area
            bool isAuthorized = false;
            string checkInMethod = Constants.CheckInMethodAdmin;

            if (claims.IsEventAdmin || claims.IsSystemAdmin)
            {
                // Admins can check in anyone, but need elevated permissions
                if (!claims.CanUseElevatedPermissions)
                {
                    return new ForbidResult();
                }
                isAuthorized = true;
            }
            else
            {
                // Check if user is an area lead for this checkpoint's area
                List<string> areaLeadAreaIds = [.. claims.EventRoles
                    .Where(r => r.Role == Constants.RoleEventAreaLead)
                    .SelectMany(r => r.AreaIds)];

                if (areaLeadAreaIds.Count > 0 && !string.IsNullOrEmpty(location.AreaIdsJson))
                {
                    List<string> checkpointAreaIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(location.AreaIdsJson) ?? [];
                    isAuthorized = checkpointAreaIds.Any(areaId => areaLeadAreaIds.Contains(areaId));
                    if (isAuthorized)
                    {
                        checkInMethod = Constants.CheckInMethodAreaLead;
                    }
                }
            }

            if (!isAuthorized)
            {
                return new ForbidResult();
            }

            // Determine who is performing the check-in (name if available, else email)
            string checkedInBy = !string.IsNullOrEmpty(claims.PersonName) ? claims.PersonName : claims.PersonEmail;

            // Store original state for rollback
            bool wasCheckedIn = assignment.IsCheckedIn;
            DateTime? originalCheckInTime = assignment.CheckInTime;
            double? originalLat = assignment.CheckInLatitude;
            double? originalLon = assignment.CheckInLongitude;
            string originalMethod = assignment.CheckInMethod;
            string originalCheckedInBy = assignment.CheckedInBy;

            // If already checked in, undo the check-in
            if (assignment.IsCheckedIn)
            {
                assignment.IsCheckedIn = false;
                assignment.CheckInTime = null;
                assignment.CheckInLatitude = null;
                assignment.CheckInLongitude = null;
                assignment.CheckInMethod = string.Empty;
                assignment.CheckedInBy = string.Empty;
                location.CheckedInCount--;
            }
            else
            {
                // Check in via admin or area lead
                assignment.IsCheckedIn = true;
                assignment.CheckInTime = DateTime.UtcNow;
                assignment.CheckInMethod = checkInMethod;
                assignment.CheckedInBy = checkedInBy;
                location.CheckedInCount++;
            }

            // Update assignment first
            await _assignmentRepository.UpdateAsync(assignment);

            // Update location with retry logic
            bool locationUpdated = await FunctionHelpers.ExecuteWithRetryAsync(
                () => _locationRepository.UpdateAsync(location),
                maxRetries: 3,
                baseDelayMs: 100
            );

            if (!locationUpdated)
            {
                // Rollback assignment with retry logic
                _logger.LogError("Failed to update location count in admin check-in after retries, attempting rollback");
                assignment.IsCheckedIn = wasCheckedIn;
                assignment.CheckInTime = originalCheckInTime;
                assignment.CheckInLatitude = originalLat;
                assignment.CheckInLongitude = originalLon;
                assignment.CheckInMethod = originalMethod;
                assignment.CheckedInBy = originalCheckedInBy;

                bool rollbackSucceeded = await FunctionHelpers.ExecuteWithRetryAsync(
                    () => _assignmentRepository.UpdateAsync(assignment),
                    maxRetries: 3,
                    baseDelayMs: 100
                );

                if (!rollbackSucceeded)
                {
                    _logger.LogCritical("CRITICAL: Failed to rollback assignment in admin check-in. Data inconsistency for assignment {AssignmentId}", assignment.RowKey);
                }

                return new StatusCodeResult(500);
            }

            AssignmentResponse response = assignment.ToResponse();

            _logger.LogInformation("Admin check-in toggle: {AssignmentId} by {CheckedInBy} - Now {CheckInStatus}",
                assignment.RowKey, checkedInBy, assignment.IsCheckedIn ? "checked in" : "checked out");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during admin check-in");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051
}
