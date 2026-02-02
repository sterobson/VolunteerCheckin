using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using VolunteerCheckin.Functions.Helpers;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Functions;

/// <summary>
/// Functions for creating and managing sample/demo events.
/// Sample events allow anonymous users to try the system without authentication.
/// </summary>
public class SampleEventFunctions
{
    private readonly ILogger<SampleEventFunctions> _logger;
    private readonly SampleEventService _sampleEventService;

    public SampleEventFunctions(
        ILogger<SampleEventFunctions> logger,
        SampleEventService sampleEventService)
    {
        _logger = logger;
        _sampleEventService = sampleEventService;
    }

    /// <summary>
    /// Create a new sample event.
    /// No authentication required.
    /// Rate limited to one per device per 24 hours (persisted to table storage).
    /// </summary>
    [Function("CreateSampleEvent")]
    public async Task<HttpResponseData> CreateSampleEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sample-event")] HttpRequestData req)
    {
        // Get device fingerprint from header for rate limiting
        string deviceFingerprint = req.Headers.TryGetValues("X-Device-Fingerprint", out IEnumerable<string>? values)
            ? values.FirstOrDefault() ?? "unknown"
            : "unknown";

        // Also use IP as fallback/additional check
        string? clientIp = req.Headers.TryGetValues("X-Forwarded-For", out IEnumerable<string>? ipValues)
            ? ipValues.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim()
            : null;

        _logger.LogInformation("Sample event creation attempt. Device: {DeviceFingerprint}, IP: {ClientIp}",
            deviceFingerprint, clientIp ?? "unknown");

        // Check persistent rate limit: 1 per device per 24 hours
        bool deviceRateLimited = await _sampleEventService.IsDeviceRateLimitedAsync(deviceFingerprint);
        _logger.LogInformation("Device rate limit check result: {IsLimited}", deviceRateLimited);

        if (deviceRateLimited)
        {
            _logger.LogWarning("Rate limit exceeded for sample event creation. Device: {DeviceFingerprint}, IP: {ClientIp}",
                deviceFingerprint, clientIp ?? "unknown");

            HttpResponseData rateLimitResponse = req.CreateResponse(HttpStatusCode.TooManyRequests);
            await rateLimitResponse.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                message = "You've already created a sample event today. Please try again tomorrow."
            });
            return rateLimitResponse;
        }

        // Also rate limit by IP if available (to prevent fingerprint spoofing): 3 per IP per day
        if (!string.IsNullOrEmpty(clientIp) && await _sampleEventService.IsIpRateLimitedAsync(clientIp))
        {
            _logger.LogWarning("IP rate limit exceeded for sample event creation. IP: {ClientIp}", clientIp);

            HttpResponseData ipRateLimitResponse = req.CreateResponse(HttpStatusCode.TooManyRequests);
            await ipRateLimitResponse.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                message = "Too many sample events created from your network. Please try again tomorrow."
            });
            return ipRateLimitResponse;
        }

        try
        {
            // Parse optional request body for timezone
            string? timeZoneId = null;
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    JsonDocument json = JsonDocument.Parse(body);
                    if (json.RootElement.TryGetProperty("timeZoneId", out JsonElement tzElement))
                    {
                        timeZoneId = tzElement.GetString();
                    }
                }
            }
            catch
            {
                // Ignore parse errors, use default timezone
            }

            // Create sample event with Lake District route
            List<SampleCheckpoint> checkpoints = GetDefaultCheckpoints();
            string routeJson = GetSampleRoute();

            SampleEventResult result = await _sampleEventService.CreateSampleEventAsync(checkpoints, routeJson, deviceFingerprint, clientIp, timeZoneId);

            _logger.LogInformation(
                "Created sample event {EventId} with {CheckpointCount} checkpoints and {MarshalCount} marshals. Expires at {ExpiresAt}",
                result.EventId, result.CheckpointCount, result.MarshalCount, result.ExpiresAt);

            HttpResponseData response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new
            {
                eventId = result.EventId,
                adminCode = result.AdminCode,
                expiresAt = result.ExpiresAt,
                lifetimeHours = Constants.SampleEventLifetimeHours,
                marshalCount = result.MarshalCount,
                checkpointCount = result.CheckpointCount
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sample event");

            HttpResponseData errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = "Failed to create sample event",
                message = "An unexpected error occurred. Please try again."
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// Recover an existing sample event by device fingerprint.
    /// Used when the user's localStorage was cleared but they still have the device fingerprint.
    /// </summary>
    [Function("RecoverSampleEvent")]
    public async Task<HttpResponseData> RecoverSampleEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sample-event/recover")] HttpRequestData req)
    {
        // Get device fingerprint from header
        string deviceFingerprint = req.Headers.TryGetValues("X-Device-Fingerprint", out IEnumerable<string>? values)
            ? values.FirstOrDefault() ?? ""
            : "";

        if (string.IsNullOrWhiteSpace(deviceFingerprint) || deviceFingerprint == "unknown")
        {
            HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteAsJsonAsync(new { error = "Device fingerprint is required" });
            return badRequestResponse;
        }

        _logger.LogInformation("Sample event recovery attempt for device: {DeviceFingerprint}", deviceFingerprint);

        SampleEventRecoveryResult? result = await _sampleEventService.GetSampleEventByDeviceFingerprintAsync(deviceFingerprint);

        if (result == null)
        {
            HttpResponseData notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new
            {
                error = "No sample event found",
                message = "No active sample event found for this device."
            });
            return notFoundResponse;
        }

        _logger.LogInformation("Recovered sample event {EventId} for device {DeviceFingerprint}", result.EventId, deviceFingerprint);

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            eventId = result.EventId,
            adminCode = result.AdminCode,
            expiresAt = result.ExpiresAt
        });
        return response;
    }

    /// <summary>
    /// Validate a sample event admin code and return the event ID.
    /// Used by the frontend to authenticate sample event access.
    /// </summary>
    [Function("ValidateSampleEventCode")]
    public async Task<HttpResponseData> ValidateSampleEventCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sample-event/validate/{code}")] HttpRequestData req,
        string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteAsJsonAsync(new { error = "Code is required" });
            return badRequestResponse;
        }

        string? eventId = await _sampleEventService.GetEventIdByAdminCodeAsync(code);

        if (eventId == null)
        {
            HttpResponseData notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new
            {
                error = "Invalid or expired code",
                message = "This sample event code is invalid or has expired."
            });
            return notFoundResponse;
        }

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { eventId });
        return response;
    }

    /// <summary>
    /// Get sample checkpoints along the Lake District route.
    /// </summary>
    private static List<SampleCheckpoint> GetDefaultCheckpoints()
    {
        return
        [
            new SampleCheckpoint
            {
                Name = "Start",
                Description = "Race start point with timing mat. Ensure runners cross the mat and don't cut the corner.",
                Latitude = 54.569606,
                Longitude = -2.862754,
                AreaName = "Start/Finish",
                RequiredMarshals = 2,
                StyleType = "start",
                StyleMapRotation = 25
            },
            new SampleCheckpoint
            {
                Name = "CP1",
                Description = "First turn off the main road onto the fell path. Watch for traffic when directing runners.",
                Latitude = 54.563762,
                Longitude = -2.876401,
                AreaName = "North of the course",
                RequiredMarshals = 2,
                StyleType = "arrow-slight-left",
                StyleMapRotation = -135
            },
            new SampleCheckpoint
            {
                Name = "CP2",
                Description = "Fell path checkpoint. Ground can be uneven - warn runners if conditions are slippery.",
                Latitude = 54.557867,
                Longitude = -2.876101,
                AreaName = "North of the course",
                RequiredMarshals = 2,
                StyleType = "default"
            },
            new SampleCheckpoint
            {
                Name = "CP3",
                Description = "Gate crossing point. Please ensure the gate is kept closed between runners to keep livestock secure.",
                Latitude = 54.556200,
                Longitude = -2.879233,
                AreaName = "North of the course",
                RequiredMarshals = 2,
                StyleType = "default"
            },
            new SampleCheckpoint
            {
                Name = "CP4",
                Description = "Sharp left turn onto the southern trail. This is a common wrong-turn point - be vigilant!",
                Latitude = 54.543412,
                Longitude = -2.874255,
                AreaName = "South of the course",
                RequiredMarshals = 2,
                StyleType = "arrow-left",
                StyleMapRotation = 180
            },
            new SampleCheckpoint
            {
                Name = "CP5",
                Description = "Remote checkpoint with limited mobile signal. Use radio for communications.",
                Latitude = 54.538186,
                Longitude = -2.870092,
                AreaName = "South of the course",
                RequiredMarshals = 2,
                StyleType = "arrow-left",
                StyleMapRotation = 180
            },
            new SampleCheckpoint
            {
                Name = "CP6",
                Description = "Scenic viewpoint and photo opportunity. Runners often stop here briefly.",
                Latitude = 54.541894,
                Longitude = -2.855372,
                AreaName = "South of the course",
                RequiredMarshals = 2,
                StyleType = "photo"
            },
            new SampleCheckpoint
            {
                Name = "CP7",
                Description = "Water station. Keep cups stocked and direct runners to the bin for used cups.",
                Latitude = 54.544781,
                Longitude = -2.856188,
                AreaName = "South of the course",
                RequiredMarshals = 2,
                StyleType = "cup"
            },
            new SampleCheckpoint
            {
                Name = "CP8",
                Description = "Path narrows here. If runners are bunching up, encourage faster runners to pass safely.",
                Latitude = 54.547493,
                Longitude = -2.856445,
                AreaName = "South of the course",
                RequiredMarshals = 2,
                StyleType = "arrow-straight",
                StyleMapRotation = -30
            },
            new SampleCheckpoint
            {
                Name = "CP9",
                Description = "Rejoin point from the southern loop. Encourage tired runners - they're on the home stretch!",
                Latitude = 54.559976,
                Longitude = -2.859149,
                AreaName = "North of the course",
                RequiredMarshals = 2,
                StyleType = "arrow-left"
            },
            new SampleCheckpoint
            {
                Name = "CP10",
                Description = "Road crossing point. Stop traffic to allow runners to cross safely in groups.",
                Latitude = 54.567702,
                Longitude = -2.860898,
                AreaName = "Start/Finish",
                RequiredMarshals = 2,
                StyleType = "cone"
            },
            new SampleCheckpoint
            {
                Name = "CP11",
                Description = "Final turn before the finish. Direct runners left towards the finish chute.",
                Latitude = 54.568647,
                Longitude = -2.863054,
                AreaName = "Start/Finish",
                RequiredMarshals = 2,
                StyleType = "arrow-left",
                StyleMapRotation = -45
            },
            new SampleCheckpoint
            {
                Name = "Finish line",
                Description = "Race finish with timing mat and medal collection. Ensure runners cross the mat before collecting medals.",
                Latitude = 54.568038,
                Longitude = -2.863913,
                AreaName = "Start/Finish",
                RequiredMarshals = 2,
                StyleType = "finish"
            }
        ];
    }

    /// <summary>
    /// Get the sample route JSON (Lake District loop).
    /// </summary>
    private static string GetSampleRoute()
    {
        return """[{"Lat":54.569640,"Lng":-2.862627},{"Lat":54.568272,"Lng":-2.865759},{"Lat":54.567962,"Lng":-2.867519},{"Lat":54.567365,"Lng":-2.868012},{"Lat":54.567041,"Lng":-2.868956},{"Lat":54.566544,"Lng":-2.869836},{"Lat":54.565848,"Lng":-2.872067},{"Lat":54.565238,"Lng":-2.873419},{"Lat":54.563783,"Lng":-2.876251},{"Lat":54.562453,"Lng":-2.876788},{"Lat":54.559903,"Lng":-2.876423},{"Lat":54.558871,"Lng":-2.876037},{"Lat":54.558461,"Lng":-2.876230},{"Lat":54.558175,"Lng":-2.876122},{"Lat":54.557080,"Lng":-2.877109},{"Lat":54.556296,"Lng":-2.879148},{"Lat":54.554555,"Lng":-2.879191},{"Lat":54.552403,"Lng":-2.877860},{"Lat":54.551147,"Lng":-2.876230},{"Lat":54.549496,"Lng":-2.875630},{"Lat":54.548948,"Lng":-2.875394},{"Lat":54.547953,"Lng":-2.874279},{"Lat":54.546734,"Lng":-2.873914},{"Lat":54.545029,"Lng":-2.873785},{"Lat":54.543423,"Lng":-2.874364},{"Lat":54.543361,"Lng":-2.873850},{"Lat":54.543063,"Lng":-2.873850},{"Lat":54.542714,"Lng":-2.872970},{"Lat":54.541818,"Lng":-2.871854},{"Lat":54.539317,"Lng":-2.870309},{"Lat":54.538198,"Lng":-2.870052},{"Lat":54.538260,"Lng":-2.868636},{"Lat":54.539044,"Lng":-2.862027},{"Lat":54.541247,"Lng":-2.855913},{"Lat":54.541894,"Lng":-2.855355},{"Lat":54.542491,"Lng":-2.855677},{"Lat":54.543685,"Lng":-2.854733},{"Lat":54.544419,"Lng":-2.855398},{"Lat":54.544606,"Lng":-2.856149},{"Lat":54.544992,"Lng":-2.855398},{"Lat":54.546161,"Lng":-2.855677},{"Lat":54.546870,"Lng":-2.855762},{"Lat":54.547405,"Lng":-2.856385},{"Lat":54.547841,"Lng":-2.857028},{"Lat":54.549010,"Lng":-2.857500},{"Lat":54.550668,"Lng":-2.858508},{"Lat":54.551613,"Lng":-2.859173},{"Lat":54.552683,"Lng":-2.858958},{"Lat":54.554250,"Lng":-2.859645},{"Lat":54.555482,"Lng":-2.859709},{"Lat":54.556503,"Lng":-2.859323},{"Lat":54.558816,"Lng":-2.859130},{"Lat":54.560048,"Lng":-2.859280},{"Lat":54.560159,"Lng":-2.860245},{"Lat":54.561792,"Lng":-2.860739},{"Lat":54.563358,"Lng":-2.860074},{"Lat":54.564565,"Lng":-2.860439},{"Lat":54.565224,"Lng":-2.860632},{"Lat":54.566567,"Lng":-2.860997},{"Lat":54.567151,"Lng":-2.860611},{"Lat":54.568345,"Lng":-2.861340},{"Lat":54.568458,"Lng":-2.861504},{"Lat":54.568638,"Lng":-2.862411},{"Lat":54.568660,"Lng":-2.863098},{"Lat":54.568004,"Lng":-2.863913}]""";
    }
}
