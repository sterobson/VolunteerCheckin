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

public class AssignmentFunctions
{
    private readonly ILogger<AssignmentFunctions> _logger;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IAreaRepository _areaRepository;
    private readonly IEventRepository _eventRepository;

    public AssignmentFunctions(
        ILogger<AssignmentFunctions> logger,
        IAssignmentRepository assignmentRepository,
        IMarshalRepository marshalRepository,
        ILocationRepository locationRepository,
        IAreaRepository areaRepository,
        IEventRepository eventRepository)
    {
        _logger = logger;
        _assignmentRepository = assignmentRepository;
        _marshalRepository = marshalRepository;
        _locationRepository = locationRepository;
        _areaRepository = areaRepository;
        _eventRepository = eventRepository;
    }

#pragma warning disable MA0051
    [Function("CreateAssignment")]
    public async Task<IActionResult> CreateAssignment(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "assignments")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CreateAssignmentRequest? request = JsonSerializer.Deserialize<CreateAssignmentRequest>(body, FunctionHelpers.JsonOptions);

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
                    _logger.LogInformation("Created new marshal: {MarshalId} - {MarshalName}", marshalId, marshalName);
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

            AssignmentResponse response = assignmentEntity.ToResponse();

            _logger.LogInformation("Assignment created: {AssignmentId}", assignmentEntity.RowKey);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating assignment");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

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

            AssignmentResponse response = assignmentEntity.ToResponse();

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

            List<AssignmentResponse> assignments = [.. assignmentEntities.Select(a => a.ToResponse())];

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

            _logger.LogInformation("Assignment deleted: {AssignmentId}", assignmentId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting assignment");
            return new StatusCodeResult(500);
        }
    }

    [Function("CleanupOrphanedAssignments")]
    public async Task<IActionResult> CleanupOrphanedAssignments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "assignments/{eventId}/cleanup-orphaned")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Get all assignments and locations for the event
            Task<IEnumerable<AssignmentEntity>> assignmentsTask = _assignmentRepository.GetByEventAsync(eventId);
            Task<IEnumerable<LocationEntity>> locationsTask = _locationRepository.GetByEventAsync(eventId);

            await Task.WhenAll(assignmentsTask, locationsTask);

            List<AssignmentEntity> assignments = [.. await assignmentsTask];
            HashSet<string> locationIds = (await locationsTask).Select(l => l.RowKey).ToHashSet();

            // Find orphaned assignments (where location no longer exists)
            List<AssignmentEntity> orphanedAssignments = [.. assignments
                .Where(a => !locationIds.Contains(a.LocationId))];

            // Delete orphaned assignments
            foreach (AssignmentEntity orphaned in orphanedAssignments)
            {
                await _assignmentRepository.DeleteAsync(eventId, orphaned.RowKey);
                _logger.LogInformation("Deleted orphaned assignment: {AssignmentId} (Marshal: {MarshalName}, LocationId: {LocationId})", orphaned.RowKey, orphaned.MarshalName, orphaned.LocationId);
            }

            _logger.LogInformation("Cleanup complete. Deleted {DeletedCount} orphaned assignments for event {EventId}", orphanedAssignments.Count, eventId);

            return new OkObjectResult(new
            {
                message = $"Cleanup complete",
                deletedCount = orphanedAssignments.Count,
                deletedAssignments = orphanedAssignments.Select(a => new
                {
                    assignmentId = a.RowKey,
                    marshalId = a.MarshalId,
                    marshalName = a.MarshalName,
                    locationId = a.LocationId
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up orphaned assignments");
            return new StatusCodeResult(500);
        }
    }

#pragma warning disable MA0051
    [Function("GetEventStatus")]
    public async Task<IActionResult> GetEventStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/status")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Load all data in parallel for efficiency
            Task<IEnumerable<LocationEntity>> locationsTask = _locationRepository.GetByEventAsync(eventId);
            Task<IEnumerable<AssignmentEntity>> assignmentsTask = _assignmentRepository.GetByEventAsync(eventId);
            Task<EventEntity?> eventTask = _eventRepository.GetAsync(eventId);
            Task<IEnumerable<AreaEntity>> areasTask = _areaRepository.GetByEventAsync(eventId);

            await Task.WhenAll(locationsTask, assignmentsTask, eventTask, areasTask);

            List<LocationEntity> locationsList = [.. await locationsTask];
            IEnumerable<AssignmentEntity> assignments = await assignmentsTask;
            EventEntity? eventEntity = await eventTask;
            List<AreaEntity> areasList = [.. await areasTask];

            // Check if any checkpoints are missing area assignments
            List<LocationEntity> checkpointsNeedingAreas = [.. locationsList
                .Where(l => l.GetPayload().AreaIds.Count == 0)];

            if (checkpointsNeedingAreas.Count > 0)
            {
                _logger.LogInformation("Found {CheckpointCount} checkpoints without area assignments. Calculating...", checkpointsNeedingAreas.Count);

                // Find or create default area
                AreaEntity? defaultArea = areasList.FirstOrDefault(a => a.IsDefault);
                if (defaultArea == null)
                {
                    // Create default area if it doesn't exist
                    defaultArea = new AreaEntity
                    {
                        PartitionKey = eventId,
                        RowKey = Guid.NewGuid().ToString(),
                        EventId = eventId,
                        Name = Constants.DefaultAreaName,
                        Description = Constants.DefaultAreaDescription,
                        Color = "#667eea",
                        ContactsJson = "[]",
                        PolygonJson = "[]",
                        IsDefault = true,
                        DisplayOrder = 0
                    };
                    await _areaRepository.AddAsync(defaultArea);
                    areasList.Add(defaultArea);
                }

                // Calculate and update area assignments for each checkpoint (parallel updates)
                List<Task> updateTasks = [];
                foreach (LocationEntity checkpoint in checkpointsNeedingAreas)
                {
                    List<string> areaIds = GeometryHelper.CalculateCheckpointAreas(
                        checkpoint.Latitude,
                        checkpoint.Longitude,
                        areasList,
                        defaultArea.RowKey
                    );

                    LocationPayload checkpointPayload = checkpoint.GetPayload();
                    checkpointPayload.AreaIds = areaIds;
                    checkpoint.SetPayload(checkpointPayload);
                    updateTasks.Add(_locationRepository.UpdateAsync(checkpoint));
                }
                await Task.WhenAll(updateTasks);

                _logger.LogInformation("Automatically assigned areas to {CheckpointCount} checkpoints", checkpointsNeedingAreas.Count);
            }

            // Calculate area checkpoint counts for style resolution tie-breaking
            Dictionary<string, int> areaCheckpointCounts = [];
            foreach (LocationEntity location in locationsList)
            {
                List<string> locationAreaIds = location.GetPayload().AreaIds;
                foreach (string areaId in locationAreaIds)
                {
                    areaCheckpointCounts[areaId] = areaCheckpointCounts.GetValueOrDefault(areaId, 0) + 1;
                }
            }

            // Group assignments by location ID once (O(N) instead of O(N*M))
            Dictionary<string, List<AssignmentEntity>> assignmentsByLocation = assignments
                .GroupBy(a => a.LocationId)
                .ToDictionary(g => g.Key, g => (List<AssignmentEntity>)[.. g]);

            List<LocationStatusResponse> locationStatuses = [];

            foreach (LocationEntity location in locationsList)
            {
                List<AssignmentResponse> locationAssignments = [.. assignmentsByLocation
                    .GetValueOrDefault(location.RowKey, [])
                    .Select(a => a.ToResponse())];

                int checkedInCount = locationAssignments.Count(a => a.IsCheckedIn);

                LocationPayload locationPayload = location.GetPayload();
                List<string> areaIds = locationPayload.AreaIds;

                // Resolve checkpoint style from hierarchy
                ResolvedCheckpointStyle resolvedStyle = ResolveCheckpointStyle(
                    locationPayload, areaIds, eventEntity, areasList, areaCheckpointCounts);

                // Get location update scope configurations from payload
                List<ScopeConfiguration> locationUpdateScopeConfigs = locationPayload.Dynamic.UpdateScopes;

                // Get layer IDs from payload (null = all layers for mode "all")
                List<string>? layerIds = locationPayload.LayerIds;

                // Get layer assignment mode from payload
                string layerAssignmentMode = locationPayload.LayerAssignmentMode;
                if (string.IsNullOrEmpty(layerAssignmentMode))
                {
                    if (layerIds == null)
                    {
                        layerAssignmentMode = "all";
                    }
                    else
                    {
                        layerAssignmentMode = "specific";
                    }
                }

                locationStatuses.Add(new LocationStatusResponse(
                    location.RowKey,
                    location.Name,
                    location.Description,
                    location.Latitude,
                    location.Longitude,
                    location.RequiredMarshals,
                    checkedInCount,
                    locationAssignments,
                    locationPayload.What3Words,
                    location.StartTime,
                    location.EndTime,
                    areaIds,
                    locationPayload.Style.Type ?? "default",
                    locationPayload.Style.Color ?? string.Empty,
                    locationPayload.Style.BackgroundShape ?? string.Empty,
                    locationPayload.Style.BackgroundColor ?? string.Empty,
                    locationPayload.Style.BorderColor ?? string.Empty,
                    locationPayload.Style.IconColor ?? string.Empty,
                    locationPayload.Style.Size ?? string.Empty,
                    locationPayload.Style.MapRotation ?? string.Empty,
                    resolvedStyle.Type,
                    resolvedStyle.Color,
                    resolvedStyle.BackgroundShape,
                    resolvedStyle.BackgroundColor,
                    resolvedStyle.BorderColor,
                    resolvedStyle.IconColor,
                    resolvedStyle.Size,
                    resolvedStyle.MapRotation,
                    locationPayload.Terminology.PeopleTerm ?? string.Empty,
                    locationPayload.Terminology.CheckpointTerm ?? string.Empty,
                    location.IsDynamic,
                    locationUpdateScopeConfigs,
                    locationPayload.Dynamic.LastUpdate,
                    layerAssignmentMode,
                    layerIds
                ));
            }

            EventStatusResponse response = new(eventId, locationStatuses);

            // Return normalized or full response based on X-Debug header
            if (EventStatusHelper.IsDebugRequest(req))
            {
                return new OkObjectResult(response);
            }
            return new OkObjectResult(EventStatusHelper.BuildNormalizedResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event status");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Resolved checkpoint style containing all style properties
    /// </summary>
    private sealed record ResolvedCheckpointStyle(
        string Type,
        string Color,
        string BackgroundShape,
        string BackgroundColor,
        string BorderColor,
        string IconColor,
        string Size,
        string MapRotation
    );

    /// <summary>
    /// Resolves the effective checkpoint style from the hierarchy:
    /// 1. Checkpoint's own style (if not "default")
    /// 2. Area's style (if not "default") - prefers area with fewest checkpoints if multiple
    /// 3. Event's default style (if not "default")
    /// 4. Returns "default" to use status-based colored circles
    /// Each property is resolved independently through the hierarchy.
    /// </summary>
#pragma warning disable MA0051
    private static ResolvedCheckpointStyle ResolveCheckpointStyle(
        LocationPayload payload,
        List<string> areaIds,
        EventEntity? eventEntity,
        List<AreaEntity> areas,
        Dictionary<string, int> areaCheckpointCounts)
    {
        // Start with defaults
        string type = "default";
        string color = string.Empty;
        string backgroundShape = string.Empty;
        string backgroundColor = string.Empty;
        string borderColor = string.Empty;
        string iconColor = string.Empty;
        string size = string.Empty;
        string mapRotation = string.Empty;

        // Get event default styles from payload (not flat properties) for v2 support
        LocationStylingPayload? eventStyle = eventEntity?.GetPayload().Styling.Locations;

        // Get matched areas sorted by checkpoint count for consistent resolution
        List<AreaEntity>? sortedMatchedAreas = null;
        if (areas != null && areaIds.Count > 0)
        {
            IEnumerable<AreaEntity> matchedAreas = areas.Where(a => areaIds.Contains(a.RowKey));
            if (areaCheckpointCounts != null)
            {
                matchedAreas = matchedAreas.OrderBy(a =>
                    areaCheckpointCounts.TryGetValue(a.RowKey, out int count) ? count : int.MaxValue);
            }
            sortedMatchedAreas = [.. matchedAreas];
        }

        // Resolve Type: checkpoint -> area -> event
        // 'custom' means "has custom properties but inherit icon type" - treat like 'default'
        bool IsValidIconType(string? styleType) =>
            !string.IsNullOrEmpty(styleType) && styleType != "default" && styleType != "custom";

        if (IsValidIconType(payload.Style.Type))
        {
            type = payload.Style.Type;
        }
        else if (sortedMatchedAreas != null)
        {
            AreaEntity? styledArea = sortedMatchedAreas.FirstOrDefault(a =>
                IsValidIconType(a.GetPayload().Style.Type));
            if (styledArea != null)
            {
                type = styledArea.GetPayload().Style.Type;
            }
            else if (eventStyle != null && IsValidIconType(eventStyle.DefaultType))
            {
                type = eventStyle.DefaultType;
            }
        }
        else if (eventStyle != null && IsValidIconType(eventStyle.DefaultType))
        {
            type = eventStyle.DefaultType;
        }

        // Helper to resolve a string property through hierarchy
        string ResolveProperty(
            string? checkpointValue,
            Func<AreaEntity, string?> areaGetter,
            string? eventValue)
        {
            if (!string.IsNullOrEmpty(checkpointValue)) return checkpointValue;
            if (sortedMatchedAreas != null)
            {
                AreaEntity? area = sortedMatchedAreas.FirstOrDefault(a => !string.IsNullOrEmpty(areaGetter(a)));
                if (area != null) return areaGetter(area)!;
            }
            if (!string.IsNullOrEmpty(eventValue)) return eventValue;
            return string.Empty;
        }

        // Resolve each property through the hierarchy
        // Color - use StyleColor for backward compatibility, then StyleBackgroundColor
        color = ResolveProperty(
            !string.IsNullOrEmpty(payload.Style.BackgroundColor) ? payload.Style.BackgroundColor : payload.Style.Color,
            a => { AreaStylePayload s = a.GetPayload().Style; return !string.IsNullOrEmpty(s.BackgroundColor) ? s.BackgroundColor : s.Color; },
            !string.IsNullOrEmpty(eventStyle?.DefaultBackgroundColor) ? eventStyle.DefaultBackgroundColor : eventStyle?.DefaultColor);

        backgroundShape = ResolveProperty(
            payload.Style.BackgroundShape,
            a => a.GetPayload().Style.BackgroundShape,
            eventStyle?.DefaultBackgroundShape);

        backgroundColor = ResolveProperty(
            payload.Style.BackgroundColor,
            a => a.GetPayload().Style.BackgroundColor,
            eventStyle?.DefaultBackgroundColor);

        borderColor = ResolveProperty(
            payload.Style.BorderColor,
            a => a.GetPayload().Style.BorderColor,
            eventStyle?.DefaultBorderColor);

        iconColor = ResolveProperty(
            payload.Style.IconColor,
            a => a.GetPayload().Style.IconColor,
            eventStyle?.DefaultIconColor);

        size = ResolveProperty(
            payload.Style.Size,
            a => a.GetPayload().Style.Size,
            eventStyle?.DefaultSize);

        mapRotation = ResolveProperty(
            payload.Style.MapRotation,
            a => a.GetPayload().Style.MapRotation,
            eventStyle?.DefaultMapRotation);

        return new ResolvedCheckpointStyle(type, color, backgroundShape, backgroundColor, borderColor, iconColor, size, mapRotation);
    }
#pragma warning restore MA0051
}
