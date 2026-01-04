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
                assignmentEntity.MarshalId,
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
                assignmentEntity.MarshalId,
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

            List<AssignmentResponse> assignments = assignmentEntities.Select(a => a.ToResponse()).ToList();

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
            // Load all data in parallel for efficiency
            Task<IEnumerable<LocationEntity>> locationsTask = _locationRepository.GetByEventAsync(eventId);
            Task<IEnumerable<AssignmentEntity>> assignmentsTask = _assignmentRepository.GetByEventAsync(eventId);
            Task<EventEntity?> eventTask = _eventRepository.GetAsync(eventId);
            Task<IEnumerable<AreaEntity>> areasTask = _areaRepository.GetByEventAsync(eventId);

            await Task.WhenAll(locationsTask, assignmentsTask, eventTask, areasTask);

            List<LocationEntity> locationsList = locationsTask.Result.ToList();
            IEnumerable<AssignmentEntity> assignments = assignmentsTask.Result;
            EventEntity? eventEntity = eventTask.Result;
            List<AreaEntity> areasList = areasTask.Result.ToList();

            // Check if any checkpoints are missing area assignments
            List<LocationEntity> checkpointsNeedingAreas = locationsList
                .Where(l => string.IsNullOrEmpty(l.AreaIdsJson) || l.AreaIdsJson == "[]")
                .ToList();

            if (checkpointsNeedingAreas.Count > 0)
            {
                _logger.LogInformation($"Found {checkpointsNeedingAreas.Count} checkpoints without area assignments. Calculating...");

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

                    checkpoint.AreaIdsJson = JsonSerializer.Serialize(areaIds);
                    updateTasks.Add(_locationRepository.UpdateAsync(checkpoint));
                }
                await Task.WhenAll(updateTasks);

                _logger.LogInformation($"Automatically assigned areas to {checkpointsNeedingAreas.Count} checkpoints");
            }

            // Calculate area checkpoint counts for style resolution tie-breaking
            Dictionary<string, int> areaCheckpointCounts = [];
            foreach (LocationEntity location in locationsList)
            {
                List<string> locationAreaIds = JsonSerializer.Deserialize<List<string>>(location.AreaIdsJson ?? "[]") ?? [];
                foreach (string areaId in locationAreaIds)
                {
                    areaCheckpointCounts[areaId] = areaCheckpointCounts.GetValueOrDefault(areaId, 0) + 1;
                }
            }

            // Group assignments by location ID once (O(N) instead of O(N*M))
            Dictionary<string, List<AssignmentEntity>> assignmentsByLocation = assignments
                .GroupBy(a => a.LocationId)
                .ToDictionary(g => g.Key, g => g.ToList());

            List<LocationStatusResponse> locationStatuses = [];

            foreach (LocationEntity location in locationsList)
            {
                List<AssignmentResponse> locationAssignments = assignmentsByLocation
                    .GetValueOrDefault(location.RowKey, [])
                    .Select(a => a.ToResponse())
                    .ToList();

                int checkedInCount = locationAssignments.Count(a => a.IsCheckedIn);

                List<string> areaIds = JsonSerializer.Deserialize<List<string>>(location.AreaIdsJson ?? "[]") ?? [];

                // Resolve checkpoint style from hierarchy
                ResolvedCheckpointStyle resolvedStyle = ResolveCheckpointStyle(
                    location, areaIds, eventEntity, areasList, areaCheckpointCounts);

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
                    location.EndTime,
                    areaIds,
                    location.StyleType ?? "default",
                    location.StyleColor ?? string.Empty,
                    location.StyleBackgroundShape ?? string.Empty,
                    location.StyleBackgroundColor ?? string.Empty,
                    location.StyleBorderColor ?? string.Empty,
                    location.StyleIconColor ?? string.Empty,
                    location.StyleSize ?? string.Empty,
                    resolvedStyle.Type,
                    resolvedStyle.Color,
                    resolvedStyle.BackgroundShape,
                    resolvedStyle.BackgroundColor,
                    resolvedStyle.BorderColor,
                    resolvedStyle.IconColor,
                    resolvedStyle.Size
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

    /// <summary>
    /// Resolved checkpoint style containing all style properties
    /// </summary>
    private record ResolvedCheckpointStyle(
        string Type,
        string Color,
        string BackgroundShape,
        string BackgroundColor,
        string BorderColor,
        string IconColor,
        string Size
    );

    /// <summary>
    /// Resolves the effective checkpoint style from the hierarchy:
    /// 1. Checkpoint's own style (if not "default")
    /// 2. Area's style (if not "default") - prefers area with fewest checkpoints if multiple
    /// 3. Event's default style (if not "default")
    /// 4. Returns "default" to use status-based colored circles
    /// Each property is resolved independently through the hierarchy.
    /// </summary>
    private static ResolvedCheckpointStyle ResolveCheckpointStyle(
        LocationEntity location,
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
            sortedMatchedAreas = matchedAreas.ToList();
        }

        // Resolve Type: checkpoint -> area -> event
        if (!string.IsNullOrEmpty(location.StyleType) && location.StyleType != "default")
        {
            type = location.StyleType;
        }
        else if (sortedMatchedAreas != null)
        {
            AreaEntity? styledArea = sortedMatchedAreas.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.CheckpointStyleType) && a.CheckpointStyleType != "default");
            if (styledArea != null)
            {
                type = styledArea.CheckpointStyleType;
            }
            else if (eventEntity != null && !string.IsNullOrEmpty(eventEntity.DefaultCheckpointStyleType) &&
                     eventEntity.DefaultCheckpointStyleType != "default")
            {
                type = eventEntity.DefaultCheckpointStyleType;
            }
        }
        else if (eventEntity != null && !string.IsNullOrEmpty(eventEntity.DefaultCheckpointStyleType) &&
                 eventEntity.DefaultCheckpointStyleType != "default")
        {
            type = eventEntity.DefaultCheckpointStyleType;
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
            !string.IsNullOrEmpty(location.StyleBackgroundColor) ? location.StyleBackgroundColor : location.StyleColor,
            a => !string.IsNullOrEmpty(a.CheckpointStyleBackgroundColor) ? a.CheckpointStyleBackgroundColor : a.CheckpointStyleColor,
            !string.IsNullOrEmpty(eventEntity?.DefaultCheckpointStyleBackgroundColor) ? eventEntity.DefaultCheckpointStyleBackgroundColor : eventEntity?.DefaultCheckpointStyleColor);

        backgroundShape = ResolveProperty(
            location.StyleBackgroundShape,
            a => a.CheckpointStyleBackgroundShape,
            eventEntity?.DefaultCheckpointStyleBackgroundShape);

        backgroundColor = ResolveProperty(
            location.StyleBackgroundColor,
            a => a.CheckpointStyleBackgroundColor,
            eventEntity?.DefaultCheckpointStyleBackgroundColor);

        borderColor = ResolveProperty(
            location.StyleBorderColor,
            a => a.CheckpointStyleBorderColor,
            eventEntity?.DefaultCheckpointStyleBorderColor);

        iconColor = ResolveProperty(
            location.StyleIconColor,
            a => a.CheckpointStyleIconColor,
            eventEntity?.DefaultCheckpointStyleIconColor);

        size = ResolveProperty(
            location.StyleSize,
            a => a.CheckpointStyleSize,
            eventEntity?.DefaultCheckpointStyleSize);

        return new ResolvedCheckpointStyle(type, color, backgroundShape, backgroundColor, borderColor, iconColor, size);
    }
}
