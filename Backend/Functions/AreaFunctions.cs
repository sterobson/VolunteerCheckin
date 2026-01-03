using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Helpers;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Functions;

public class AreaFunctions
{
    private readonly ILogger<AreaFunctions> _logger;
    private readonly IAreaRepository _areaRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IChecklistItemRepository _checklistItemRepository;
    private readonly IChecklistCompletionRepository _checklistCompletionRepository;
    private readonly ClaimsService _claimsService;
    private readonly ContactPermissionService _contactPermissionService;

    public AreaFunctions(
        ILogger<AreaFunctions> logger,
        IAreaRepository areaRepository,
        ILocationRepository locationRepository,
        IMarshalRepository marshalRepository,
        IAssignmentRepository assignmentRepository,
        IChecklistItemRepository checklistItemRepository,
        IChecklistCompletionRepository checklistCompletionRepository,
        ClaimsService claimsService,
        ContactPermissionService contactPermissionService)
    {
        _logger = logger;
        _areaRepository = areaRepository;
        _locationRepository = locationRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _checklistItemRepository = checklistItemRepository;
        _checklistCompletionRepository = checklistCompletionRepository;
        _claimsService = claimsService;
        _contactPermissionService = contactPermissionService;
    }

    [Function("CreateArea")]
    public async Task<IActionResult> CreateArea(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "areas")] HttpRequest req)
    {
        try
        {
            (CreateAreaRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<CreateAreaRequest>(req);
            if (error != null) return error;

            // Validate contacts reference valid marshals (batch load instead of N queries)
            if (request!.Contacts.Count > 0)
            {
                IEnumerable<MarshalEntity> allMarshals = await _marshalRepository.GetByEventAsync(request.EventId);
                HashSet<string> validMarshalIds = allMarshals.Select(m => m.MarshalId).ToHashSet();

                foreach (AreaContact contact in request.Contacts)
                {
                    if (!validMarshalIds.Contains(contact.MarshalId))
                    {
                        return new BadRequestObjectResult(new { message = $"Marshal not found: {contact.MarshalId}" });
                    }
                }
            }

            AreaEntity areaEntity = new()
            {
                PartitionKey = request.EventId,
                RowKey = Guid.NewGuid().ToString(),
                EventId = request.EventId,
                Name = request.Name,
                Description = request.Description,
                Color = request.Color,
                ContactsJson = JsonSerializer.Serialize(request.Contacts),
                PolygonJson = JsonSerializer.Serialize(request.Polygon ?? []),
                IsDefault = false,
                DisplayOrder = 0
            };

            await _areaRepository.AddAsync(areaEntity);

            // If a polygon was provided, recalculate checkpoint assignments
            if (request.Polygon != null && request.Polygon.Count > 0)
            {
                await RecalculateCheckpointAreas(request.EventId);
            }

            // Get checkpoint count
            int checkpointCount = await _locationRepository.CountByAreaAsync(request.EventId, areaEntity.RowKey);
            AreaResponse response = areaEntity.ToResponse(checkpointCount);

            _logger.LogInformation($"Area created: {areaEntity.RowKey}");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating area");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetArea")]
    public async Task<IActionResult> GetArea(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "areas/{eventId}/{areaId}")] HttpRequest req,
        string eventId,
        string areaId)
    {
        try
        {
            AreaEntity? areaEntity = await _areaRepository.GetAsync(eventId, areaId);

            if (areaEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Area not found" });
            }

            int checkpointCount = await _locationRepository.CountByAreaAsync(eventId, areaId);
            AreaResponse baseResponse = areaEntity.ToResponse(checkpointCount);

            // Enrich contacts with phone/email from marshal data
            if (baseResponse.Contacts.Count > 0)
            {
                IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
                Dictionary<string, MarshalEntity> marshalLookup = marshals.ToDictionary(m => m.MarshalId);

                List<AreaContact> enrichedContacts = baseResponse.Contacts
                    .Select(c => {
                        marshalLookup.TryGetValue(c.MarshalId, out MarshalEntity? marshal);
                        return new AreaContact(
                            c.MarshalId,
                            c.MarshalName,
                            c.Role,
                            marshal?.PhoneNumber,
                            marshal?.Email
                        );
                    })
                    .ToList();

                // Create new response with enriched contacts
                AreaResponse response = new AreaResponse(
                    baseResponse.Id,
                    baseResponse.EventId,
                    baseResponse.Name,
                    baseResponse.Description,
                    baseResponse.Color,
                    enrichedContacts,
                    baseResponse.Polygon,
                    baseResponse.IsDefault,
                    baseResponse.DisplayOrder,
                    baseResponse.CheckpointCount,
                    baseResponse.CreatedDate
                );

                return new OkObjectResult(response);
            }

            return new OkObjectResult(baseResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting area");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetAreasByEvent")]
    public async Task<IActionResult> GetAreasByEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/areas")] HttpRequest req,
        string eventId)
    {
        try
        {
            IEnumerable<AreaEntity> areaEntities = await _areaRepository.GetByEventAsync(eventId);

            // Preload all locations and calculate checkpoint counts in memory (1 query instead of N)
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
            Dictionary<string, int> checkpointCountsByArea = new();

            foreach (LocationEntity location in allLocations)
            {
                List<string> areaIds = JsonSerializer.Deserialize<List<string>>(location.AreaIdsJson) ?? [];
                foreach (string areaId in areaIds)
                {
                    checkpointCountsByArea[areaId] = checkpointCountsByArea.GetValueOrDefault(areaId, 0) + 1;
                }
            }

            List<AreaResponse> areas = areaEntities
                .Select(area => area.ToResponse(checkpointCountsByArea.GetValueOrDefault(area.RowKey, 0)))
                .ToList();

            return new OkObjectResult(areas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting areas");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateArea")]
    public async Task<IActionResult> UpdateArea(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "areas/{eventId}/{areaId}")] HttpRequest req,
        string eventId,
        string areaId)
    {
        try
        {
            (UpdateAreaRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<UpdateAreaRequest>(req);
            if (error != null) return error;
            if (request == null) return new BadRequestObjectResult(new { message = "Request body is required" });

            AreaEntity? areaEntity = await _areaRepository.GetAsync(eventId, areaId);

            if (areaEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Area not found" });
            }

            // Prevent renaming default area
            if (areaEntity.IsDefault && request.Name != Constants.DefaultAreaName)
            {
                return new BadRequestObjectResult(new { message = "Cannot rename default area" });
            }

            // Validate contacts reference valid marshals (batch load instead of N queries)
            if (request.Contacts.Count > 0)
            {
                IEnumerable<MarshalEntity> allMarshals = await _marshalRepository.GetByEventAsync(eventId);
                HashSet<string> validMarshalIds = allMarshals.Select(m => m.MarshalId).ToHashSet();

                foreach (AreaContact contact in request.Contacts)
                {
                    if (!validMarshalIds.Contains(contact.MarshalId))
                    {
                        return new BadRequestObjectResult(new { message = $"Marshal not found: {contact.MarshalId}" });
                    }
                }
            }

            areaEntity.Name = request.Name;
            areaEntity.Description = request.Description;
            areaEntity.Color = request.Color;
            areaEntity.ContactsJson = JsonSerializer.Serialize(request.Contacts);
            areaEntity.PolygonJson = JsonSerializer.Serialize(request.Polygon ?? []);
            areaEntity.DisplayOrder = request.DisplayOrder;

            await _areaRepository.UpdateAsync(areaEntity);

            // Recalculate checkpoint assignments for all checkpoints in this event
            // since the area boundary may have changed
            await RecalculateCheckpointAreas(eventId);

            int checkpointCount = await _locationRepository.CountByAreaAsync(eventId, areaId);
            AreaResponse response = areaEntity.ToResponse(checkpointCount);

            _logger.LogInformation($"Area updated: {areaId}");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating area");
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteArea")]
    public async Task<IActionResult> DeleteArea(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "areas/{eventId}/{areaId}")] HttpRequest req,
        string eventId,
        string areaId)
    {
        try
        {
            AreaEntity? areaEntity = await _areaRepository.GetAsync(eventId, areaId);

            if (areaEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Area not found" });
            }

            // Prevent deletion of default area
            if (areaEntity.IsDefault)
            {
                _logger.LogWarning($"Attempt to delete default area rejected: {areaId} in event {eventId}");
                return new BadRequestObjectResult(new { message = "Cannot delete default area" });
            }

            // Get all checkpoints in this area
            IEnumerable<LocationEntity> checkpointsInArea = await _locationRepository.GetByAreaAsync(eventId, areaId);

            if (checkpointsInArea.Any())
            {
                // Get the default area
                AreaEntity? defaultArea = await _areaRepository.GetDefaultAreaAsync(eventId);
                if (defaultArea == null)
                {
                    _logger.LogError($"Default area not found for event {eventId}");
                    return new StatusCodeResult(500);
                }

                // Remove this area from all checkpoints, and assign to default if no other areas
                // Prepare all updates first, then execute in parallel
                List<Task> updateTasks = [];
                foreach (LocationEntity checkpoint in checkpointsInArea)
                {
                    List<string> areaIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(checkpoint.AreaIdsJson) ?? [];

                    // Remove the area being deleted
                    areaIds.Remove(areaId);

                    // If no areas left, assign to default area
                    if (areaIds.Count == 0)
                    {
                        areaIds.Add(defaultArea.RowKey);
                    }

                    // Update the checkpoint with new area assignments
                    checkpoint.AreaIdsJson = System.Text.Json.JsonSerializer.Serialize(areaIds);
                    updateTasks.Add(_locationRepository.UpdateAsync(checkpoint));
                }

                // Execute all updates in parallel
                await Task.WhenAll(updateTasks);

                _logger.LogInformation($"Reassigned {checkpointsInArea.Count()} checkpoints from area {areaId}");
            }

            await _areaRepository.DeleteAsync(eventId, areaId);

            _logger.LogInformation($"Area deleted: {areaId}");

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting area");
            return new StatusCodeResult(500);
        }
    }

    [Function("RecalculateCheckpointAreasEndpoint")]
    public async Task<IActionResult> RecalculateCheckpointAreasEndpoint(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "areas/recalculate/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            await RecalculateCheckpointAreas(eventId);

            return new OkObjectResult(new {
                message = $"Successfully recalculated checkpoint area assignments for event {eventId}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating checkpoint areas");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetLocationsByArea")]
    public async Task<IActionResult> GetLocationsByArea(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "areas/{eventId}/{areaId}/locations")] HttpRequest req,
        string eventId,
        string areaId)
    {
        try
        {
            IEnumerable<LocationEntity> locationEntities = await _locationRepository.GetByAreaAsync(eventId, areaId);

            List<LocationResponse> locations = locationEntities
                .Select(l => l.ToResponse())
                .ToList();

            return new OkObjectResult(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting locations by area");
            return new StatusCodeResult(500);
        }
    }

    // Helper method to recalculate checkpoint area assignments for an event
    private async Task RecalculateCheckpointAreas(string eventId)
    {
        // Get all areas and checkpoints
        IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(eventId);
        IEnumerable<LocationEntity> checkpoints = await _locationRepository.GetByEventAsync(eventId);
        AreaEntity? defaultArea = await _areaRepository.GetDefaultAreaAsync(eventId);

        if (defaultArea == null)
        {
            _logger.LogWarning($"No default area found for event {eventId} during recalculation");
            return;
        }

        // Recalculate area assignments for each checkpoint and execute updates in parallel
        List<Task> updateTasks = [];
        foreach (LocationEntity checkpoint in checkpoints)
        {
            List<string> areaIds = Helpers.GeometryHelper.CalculateCheckpointAreas(
                checkpoint.Latitude,
                checkpoint.Longitude,
                areas,
                defaultArea.RowKey
            );

            checkpoint.AreaIdsJson = JsonSerializer.Serialize(areaIds);
            updateTasks.Add(_locationRepository.UpdateAsync(checkpoint));
        }

        await Task.WhenAll(updateTasks);

        _logger.LogInformation($"Recalculated area assignments for {checkpoints.Count()} checkpoints in event {eventId}");
    }

    [Function("AddAreaLead")]
    public async Task<IActionResult> AddAreaLead(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "areas/{eventId}/{areaId}/leads")] HttpRequest req,
        string eventId,
        string areaId)
    {
        try
        {
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

            // Only event admins can add area leads
            if (!claims.IsEventAdmin && !claims.IsSystemAdmin)
            {
                return new ForbidResult();
            }

            string body = await new StreamReader(req.Body).ReadToEndAsync();
            AddAreaLeadRequest? request = JsonSerializer.Deserialize<AddAreaLeadRequest>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.MarshalId))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidRequest });
            }

            AreaEntity? area = await _areaRepository.GetAsync(eventId, areaId);
            if (area == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorAreaNotFound });
            }

            // Verify marshal exists
            MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, request.MarshalId);
            if (marshal == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorMarshalNotFound });
            }

            // Get current area leads
            List<string> areaLeadIds = JsonSerializer.Deserialize<List<string>>(area.AreaLeadMarshalIdsJson) ?? [];

            // Check if already an area lead
            if (areaLeadIds.Contains(request.MarshalId))
            {
                return new BadRequestObjectResult(new { message = "Marshal is already an area lead for this area" });
            }

            // Add to area leads
            areaLeadIds.Add(request.MarshalId);
            area.AreaLeadMarshalIdsJson = JsonSerializer.Serialize(areaLeadIds);

            await _areaRepository.UpdateAsync(area);

            _logger.LogInformation($"Added marshal {request.MarshalId} as area lead for area {areaId}");

            // Event admins can see contact details
            return new OkObjectResult(new AreaLeadResponse(
                marshal.MarshalId,
                marshal.Name,
                marshal.Email
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding area lead");
            return new StatusCodeResult(500);
        }
    }

    [Function("RemoveAreaLead")]
    public async Task<IActionResult> RemoveAreaLead(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "areas/{eventId}/{areaId}/leads/{marshalId}")] HttpRequest req,
        string eventId,
        string areaId,
        string marshalId)
    {
        try
        {
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

            // Only event admins can remove area leads
            if (!claims.IsEventAdmin && !claims.IsSystemAdmin)
            {
                return new ForbidResult();
            }

            AreaEntity? area = await _areaRepository.GetAsync(eventId, areaId);
            if (area == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorAreaNotFound });
            }

            // Get current area leads
            List<string> areaLeadIds = JsonSerializer.Deserialize<List<string>>(area.AreaLeadMarshalIdsJson) ?? [];

            // Check if marshal is an area lead
            if (!areaLeadIds.Contains(marshalId))
            {
                return new NotFoundObjectResult(new { message = "Marshal is not an area lead for this area" });
            }

            // Remove from area leads
            areaLeadIds.Remove(marshalId);
            area.AreaLeadMarshalIdsJson = JsonSerializer.Serialize(areaLeadIds);

            await _areaRepository.UpdateAsync(area);

            _logger.LogInformation($"Removed marshal {marshalId} as area lead from area {areaId}");

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing area lead");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetAreaLeads")]
    public async Task<IActionResult> GetAreaLeads(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "areas/{eventId}/{areaId}/leads")] HttpRequest req,
        string eventId,
        string areaId)
    {
        try
        {
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

            AreaEntity? area = await _areaRepository.GetAsync(eventId, areaId);
            if (area == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorAreaNotFound });
            }

            // Get area lead IDs
            List<string> areaLeadIds = JsonSerializer.Deserialize<List<string>>(area.AreaLeadMarshalIdsJson) ?? [];

            if (areaLeadIds.Count == 0)
            {
                return new OkObjectResult(Array.Empty<AreaLeadResponse>());
            }

            // Get contact permissions for this user
            ContactPermissions permissions = await _contactPermissionService.GetContactPermissionsAsync(claims, eventId);

            // Get marshal details for each area lead
            IEnumerable<MarshalEntity> allMarshals = await _marshalRepository.GetByEventAsync(eventId);
            List<AreaLeadResponse> areaLeads = allMarshals
                .Where(m => areaLeadIds.Contains(m.MarshalId))
                .Select(m => new AreaLeadResponse(
                    m.MarshalId,
                    m.Name,
                    _contactPermissionService.CanViewContactDetails(permissions, m.MarshalId) ? m.Email : string.Empty
                ))
                .ToList();

            return new OkObjectResult(areaLeads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting area leads");
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Efficient endpoint for area lead dashboard.
    /// Returns all checkpoints in the lead's areas with marshals, check-in status, and outstanding tasks.
    /// Single API call to minimize latency.
    /// </summary>
    [Function("GetAreaLeadDashboard")]
    public async Task<IActionResult> GetAreaLeadDashboard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/area-lead-dashboard")] HttpRequest req,
        string eventId)
    {
        try
        {
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

            // Get area IDs this user leads
            List<string> leadAreaIds = claims.EventRoles
                .Where(r => r.Role == Constants.RoleEventAreaLead)
                .SelectMany(r => r.AreaIds)
                .Distinct()
                .ToList();

            if (leadAreaIds.Count == 0)
            {
                return new OkObjectResult(new AreaLeadDashboardResponse([], []));
            }

            // Batch load all data in parallel for efficiency
            Task<IEnumerable<AreaEntity>> areasTask = _areaRepository.GetByEventAsync(eventId);
            Task<IEnumerable<LocationEntity>> locationsTask = _locationRepository.GetByEventAsync(eventId);
            Task<IEnumerable<MarshalEntity>> marshalsTask = _marshalRepository.GetByEventAsync(eventId);
            Task<IEnumerable<AssignmentEntity>> assignmentsTask = _assignmentRepository.GetByEventAsync(eventId);
            Task<IEnumerable<ChecklistItemEntity>> checklistItemsTask = _checklistItemRepository.GetByEventAsync(eventId);
            Task<IEnumerable<ChecklistCompletionEntity>> completionsTask = _checklistCompletionRepository.GetByEventAsync(eventId);

            await Task.WhenAll(areasTask, locationsTask, marshalsTask, assignmentsTask, checklistItemsTask, completionsTask);

            IEnumerable<AreaEntity> areas = areasTask.Result;
            List<LocationEntity> locationsList = locationsTask.Result.ToList();
            IEnumerable<MarshalEntity> marshals = marshalsTask.Result;
            IEnumerable<AssignmentEntity> assignments = assignmentsTask.Result;
            IEnumerable<ChecklistItemEntity> checklistItems = checklistItemsTask.Result;
            IEnumerable<ChecklistCompletionEntity> completions = completionsTask.Result;

            // Check if any checkpoints are missing area assignments and auto-calculate
            List<LocationEntity> checkpointsNeedingAreas = locationsList
                .Where(l => string.IsNullOrEmpty(l.AreaIdsJson) || l.AreaIdsJson == "[]")
                .ToList();

            if (checkpointsNeedingAreas.Count > 0)
            {
                _logger.LogInformation("Found {Count} checkpoints without area assignments in area lead dashboard. Calculating...", checkpointsNeedingAreas.Count);

                // Ensure default area exists
                AreaEntity? defaultArea = areas.FirstOrDefault(a => a.IsDefault);
                if (defaultArea == null)
                {
                    defaultArea = new AreaEntity
                    {
                        PartitionKey = eventId,
                        RowKey = Guid.NewGuid().ToString(),
                        EventId = eventId,
                        Name = "Default Area",
                        Color = "#808080",
                        IsDefault = true,
                        DisplayOrder = 0
                    };
                    await _areaRepository.AddAsync(defaultArea);
                    // Refresh areas list
                    areas = await _areaRepository.GetByEventAsync(eventId);
                }

                // Calculate and update area assignments for each checkpoint
                List<Task> updateTasks = [];
                foreach (LocationEntity checkpoint in checkpointsNeedingAreas)
                {
                    List<string> calculatedAreaIds = Helpers.GeometryHelper.CalculateCheckpointAreas(
                        checkpoint.Latitude,
                        checkpoint.Longitude,
                        areas,
                        defaultArea.RowKey
                    );
                    checkpoint.AreaIdsJson = JsonSerializer.Serialize(calculatedAreaIds);
                    updateTasks.Add(_locationRepository.UpdateAsync(checkpoint));
                }
                await Task.WhenAll(updateTasks);
                _logger.LogInformation("Automatically assigned areas to {Count} checkpoints", checkpointsNeedingAreas.Count);
            }

            // Create lookups for efficient access
            Dictionary<string, MarshalEntity> marshalLookup = marshals.ToDictionary(m => m.MarshalId);
            Dictionary<string, AreaEntity> areaLookup = areas.ToDictionary(a => a.RowKey);

            // Get checkpoints in lead's areas
            List<LocationEntity> checkpointsInAreas = locationsList
                .Where(loc => {
                    if (string.IsNullOrEmpty(loc.AreaIdsJson))
                        return false;
                    List<string> locAreaIds = JsonSerializer.Deserialize<List<string>>(loc.AreaIdsJson) ?? [];
                    return locAreaIds.Any(areaId => leadAreaIds.Contains(areaId));
                })
                .OrderBy(loc => loc.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            HashSet<string> checkpointIds = checkpointsInAreas.Select(c => c.RowKey).ToHashSet();

            // Get assignments for these checkpoints
            List<AssignmentEntity> relevantAssignments = assignments
                .Where(a => checkpointIds.Contains(a.LocationId))
                .ToList();

            // Get marshal IDs from assignments
            HashSet<string> marshalIds = relevantAssignments.Select(a => a.MarshalId).ToHashSet();

            // Build completion lookup (key: itemId_contextType_contextId)
            // Only include non-deleted completions
            Dictionary<string, ChecklistCompletionEntity> completionLookup = new();
            foreach (ChecklistCompletionEntity completion in completions.Where(c => !c.IsDeleted))
            {
                string key = $"{completion.ChecklistItemId}_{completion.CompletionContextType}_{completion.CompletionContextId}";
                completionLookup[key] = completion;
            }

            // Calculate outstanding tasks for checkpoints and marshals
            Dictionary<string, List<AreaLeadTaskInfo>> tasksByCheckpoint = new();
            Dictionary<string, List<AreaLeadTaskInfo>> tasksByMarshal = new();

            foreach (ChecklistItemEntity item in checklistItems)
            {
                List<ScopeConfiguration> configs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(item.ScopeConfigurationsJson) ?? [];

                foreach (ScopeConfiguration config in configs)
                {
                    // Check if this scope applies to our checkpoints or marshals
                    if (config.Scope == Constants.ChecklistScopeEveryoneAtCheckpoints ||
                        config.Scope == Constants.ChecklistScopeOnePerCheckpoint)
                    {
                        // Checkpoint-scoped tasks
                        foreach (string checkpointId in checkpointIds)
                        {
                            if (config.Ids.Contains(Constants.AllCheckpoints) || config.Ids.Contains(checkpointId))
                            {
                                string completionKey = $"{item.ItemId}_{Constants.ChecklistContextCheckpoint}_{checkpointId}";
                                bool isCompleted = completionLookup.ContainsKey(completionKey);

                                if (!isCompleted)
                                {
                                    if (!tasksByCheckpoint.ContainsKey(checkpointId))
                                        tasksByCheckpoint[checkpointId] = [];

                                    tasksByCheckpoint[checkpointId].Add(new AreaLeadTaskInfo(
                                        item.ItemId,
                                        item.Text,
                                        config.Scope,
                                        Constants.ChecklistContextCheckpoint,
                                        checkpointId,
                                        null
                                    ));
                                }
                            }
                        }
                    }
                    else if (config.Scope == Constants.ChecklistScopeSpecificPeople)
                    {
                        // Personal tasks for marshals
                        // Check if ALL_MARSHALS is used - if so, apply to all marshals in our checkpoints
                        bool appliesToAll = config.Ids.Contains(Constants.AllMarshals);

                        foreach (string marshalId in marshalIds)
                        {
                            // Check if this task applies to this marshal
                            bool appliesToMarshal = appliesToAll || config.Ids.Contains(marshalId);

                            if (appliesToMarshal)
                            {
                                string completionKey = $"{item.ItemId}_{Constants.ChecklistContextPersonal}_{marshalId}";
                                bool isCompleted = completionLookup.ContainsKey(completionKey);

                                if (!isCompleted)
                                {
                                    if (!tasksByMarshal.ContainsKey(marshalId))
                                        tasksByMarshal[marshalId] = [];

                                    tasksByMarshal[marshalId].Add(new AreaLeadTaskInfo(
                                        item.ItemId,
                                        item.Text,
                                        config.Scope,
                                        Constants.ChecklistContextPersonal,
                                        marshalId,
                                        marshalId
                                    ));
                                }
                            }
                        }
                    }
                }
            }

            // Build checkpoint data
            List<AreaLeadCheckpointInfo> checkpointInfos = [];
            foreach (LocationEntity checkpoint in checkpointsInAreas)
            {
                List<string> checkpointAreaIds = JsonSerializer.Deserialize<List<string>>(checkpoint.AreaIdsJson) ?? [];
                string? areaName = checkpointAreaIds.Count > 0 && areaLookup.TryGetValue(checkpointAreaIds[0], out AreaEntity? area)
                    ? area.Name
                    : null;

                // Get marshals at this checkpoint
                List<AssignmentEntity> checkpointAssignments = relevantAssignments
                    .Where(a => a.LocationId == checkpoint.RowKey)
                    .ToList();

                List<AreaLeadMarshalInfo> marshalInfos = [];
                foreach (AssignmentEntity assignment in checkpointAssignments)
                {
                    if (marshalLookup.TryGetValue(assignment.MarshalId, out MarshalEntity? marshal))
                    {
                        List<AreaLeadTaskInfo> marshalTasks = tasksByMarshal.GetValueOrDefault(assignment.MarshalId, []);

                        marshalInfos.Add(new AreaLeadMarshalInfo(
                            marshal.MarshalId,
                            assignment.RowKey, // AssignmentId
                            marshal.Name,
                            marshal.Email,
                            marshal.PhoneNumber,
                            assignment.IsCheckedIn,
                            assignment.CheckInTime,
                            assignment.CheckInMethod,
                            marshal.LastAccessedDate,
                            marshalTasks.Count,
                            marshalTasks
                        ));
                    }
                }

                List<AreaLeadTaskInfo> checkpointTasks = tasksByCheckpoint.GetValueOrDefault(checkpoint.RowKey, []);

                checkpointInfos.Add(new AreaLeadCheckpointInfo(
                    checkpoint.RowKey,
                    checkpoint.Name,
                    checkpoint.Description,
                    checkpoint.Latitude,
                    checkpoint.Longitude,
                    areaName,
                    checkpointAreaIds,
                    marshalInfos,
                    checkpointTasks.Count,
                    checkpointTasks
                ));
            }

            // Build area info
            List<AreaLeadAreaInfo> areaInfos = areas
                .Where(a => leadAreaIds.Contains(a.RowKey))
                .Select(a => new AreaLeadAreaInfo(a.RowKey, a.Name, a.Color))
                .ToList();

            return new OkObjectResult(new AreaLeadDashboardResponse(areaInfos, checkpointInfos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting area lead dashboard for event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }
}
