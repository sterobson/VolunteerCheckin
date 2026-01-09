using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Helpers;

namespace VolunteerCheckin.Functions.Functions;

public class LocationFunctions
{
    private readonly ILogger<LocationFunctions> _logger;
    private readonly ILocationRepository _locationRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IChecklistItemRepository _checklistItemRepository;
    private readonly INoteRepository _noteRepository;
    private readonly IAreaRepository _areaRepository;
    private readonly IEventRoleRepository _eventRoleRepository;
    private readonly ClaimsService _claimsService;

    public LocationFunctions(
        ILogger<LocationFunctions> logger,
        ILocationRepository locationRepository,
        IMarshalRepository marshalRepository,
        IAssignmentRepository assignmentRepository,
        IChecklistItemRepository checklistItemRepository,
        INoteRepository noteRepository,
        IAreaRepository areaRepository,
        IEventRoleRepository eventRoleRepository,
        ClaimsService claimsService)
    {
        _logger = logger;
        _locationRepository = locationRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _checklistItemRepository = checklistItemRepository;
        _noteRepository = noteRepository;
        _areaRepository = areaRepository;
        _eventRoleRepository = eventRoleRepository;
        _claimsService = claimsService;
    }

#pragma warning disable MA0051
    [Function("CreateLocation")]
    public async Task<IActionResult> CreateLocation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locations")] HttpRequest req)
    {
        try
        {
            (CreateLocationRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<CreateLocationRequest>(req);
            if (error != null) return error;

            // Validate GPS coordinates
            if (!Validators.IsValidCoordinates(request!.Latitude, request.Longitude))
            {
                return new BadRequestObjectResult(new { message = "Invalid GPS coordinates. Latitude must be between -90 and 90, longitude must be between -180 and 180." });
            }

            // Validate What3Words format
            if (!Validators.IsValidWhat3Words(request.What3Words))
            {
                return new BadRequestObjectResult(new { message = "Invalid What3Words format. Must be in format word.word.word or word/word/word where each word is lowercase letters (1-20 characters)" });
            }

            // Validate required marshals count
            if (!Validators.IsNonNegative(request.RequiredMarshals))
            {
                return new BadRequestObjectResult(new { message = "Required marshals count must be non-negative" });
            }

            // Ensure default area exists and get its ID
            AreaEntity defaultArea = await EnsureDefaultAreaExists(request.EventId);

            // Get all areas for this event to calculate which ones the checkpoint belongs to
            IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(request.EventId);
            List<string> areaIds = GeometryHelper.CalculateCheckpointAreas(
                request.Latitude,
                request.Longitude,
                areas,
                defaultArea.RowKey
            );

            string locationId = Guid.NewGuid().ToString();

            // Replace THIS_CHECKPOINT placeholder with actual location ID in scope configurations
            List<ScopeConfiguration> locationUpdateScopes = request.LocationUpdateScopeConfigurations ?? [];
            foreach (ScopeConfiguration scope in locationUpdateScopes)
            {
                for (int i = 0; i < scope.Ids.Count; i++)
                {
                    if (scope.Ids[i] == Constants.ThisCheckpoint)
                    {
                        scope.Ids[i] = locationId;
                    }
                }
            }

            LocationEntity locationEntity = new()
            {
                PartitionKey = request.EventId,
                RowKey = locationId,
                EventId = request.EventId,
                Name = request.Name,
                Description = request.Description,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RequiredMarshals = request.RequiredMarshals,
                What3Words = request.What3Words ?? string.Empty,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                AreaIdsJson = JsonSerializer.Serialize(areaIds),
                // Checkpoint style
                StyleType = request.StyleType ?? "default",
                StyleColor = request.StyleColor ?? string.Empty,
                StyleBackgroundShape = request.StyleBackgroundShape ?? string.Empty,
                StyleBackgroundColor = request.StyleBackgroundColor ?? string.Empty,
                StyleBorderColor = request.StyleBorderColor ?? string.Empty,
                StyleIconColor = request.StyleIconColor ?? string.Empty,
                StyleSize = request.StyleSize ?? string.Empty,
                StyleMapRotation = request.StyleMapRotation ?? string.Empty,
                // Terminology
                PeopleTerm = request.PeopleTerm ?? string.Empty,
                CheckpointTerm = request.CheckpointTerm ?? string.Empty,
                // Dynamic checkpoint settings
                IsDynamic = request.IsDynamic,
                LocationUpdateScopeJson = JsonSerializer.Serialize(locationUpdateScopes)
            };

            await _locationRepository.AddAsync(locationEntity);

            // Create pending checklist items and notes scoped to this checkpoint
            string adminEmail = req.Headers[Constants.AdminEmailHeader].ToString();
            await CreatePendingChecklistItems(request.EventId, locationId, request.PendingNewChecklistItems, adminEmail);
            await CreatePendingNotes(request.EventId, locationId, request.PendingNewNotes, adminEmail);

            LocationResponse response = locationEntity.ToResponse();

            _logger.LogInformation("Location created: {LocationId}", locationEntity.RowKey);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    [Function("GetLocation")]
    public async Task<IActionResult> GetLocation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "locations/{eventId}/{locationId}")] HttpRequest req,
        string eventId,
        string locationId)
    {
        try
        {
            LocationEntity? locationEntity = await _locationRepository.GetAsync(eventId, locationId);

            if (locationEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Location not found" });
            }

            LocationResponse response = locationEntity.ToResponse();

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting location");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetLocationsByEvent")]
    public async Task<IActionResult> GetLocationsByEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/locations")] HttpRequest req,
        string eventId)
    {
        try
        {
            IEnumerable<LocationEntity> locationEntities = await _locationRepository.GetByEventAsync(eventId);
            List<LocationEntity> locationsList = [.. locationEntities];

            // Check if any checkpoints are missing area assignments
            List<LocationEntity> checkpointsNeedingAreas = [.. locationsList
                .Where(l => string.IsNullOrEmpty(l.AreaIdsJson) || l.AreaIdsJson == "[]")];

            if (checkpointsNeedingAreas.Count > 0)
            {
                _logger.LogInformation("Found {CheckpointCount} checkpoints without area assignments. Calculating...", checkpointsNeedingAreas.Count);

                // Load areas once for all calculations
                AreaEntity defaultArea = await EnsureDefaultAreaExists(eventId);
                IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(eventId);

                // Calculate and update area assignments for each checkpoint
                foreach (LocationEntity checkpoint in checkpointsNeedingAreas)
                {
                    List<string> areaIds = GeometryHelper.CalculateCheckpointAreas(
                        checkpoint.Latitude,
                        checkpoint.Longitude,
                        areas,
                        defaultArea.RowKey
                    );

                    checkpoint.AreaIdsJson = JsonSerializer.Serialize(areaIds);
                    await _locationRepository.UpdateAsync(checkpoint);
                }

                _logger.LogInformation("Automatically assigned areas to {CheckpointCount} checkpoints", checkpointsNeedingAreas.Count);
            }

            List<LocationResponse> locations = [.. locationsList
                .Select(l => l.ToResponse())];

            return new OkObjectResult(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting locations");
            return new StatusCodeResult(500);
        }
    }

#pragma warning disable MA0051
    [Function("UpdateLocation")]
    public async Task<IActionResult> UpdateLocation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "locations/{eventId}/{locationId}")] HttpRequest req,
        string eventId,
        string locationId)
    {
        try
        {
            (CreateLocationRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<CreateLocationRequest>(req);
            if (error != null) return error;

            // Validate GPS coordinates
            if (!Validators.IsValidCoordinates(request!.Latitude, request.Longitude))
            {
                return new BadRequestObjectResult(new { message = "Invalid GPS coordinates. Latitude must be between -90 and 90, longitude must be between -180 and 180." });
            }

            // Validate What3Words format
            if (!Validators.IsValidWhat3Words(request.What3Words))
            {
                return new BadRequestObjectResult(new { message = "Invalid What3Words format. Must be in format word.word.word or word/word/word where each word is lowercase letters (1-20 characters)" });
            }

            // Validate required marshals count
            if (!Validators.IsNonNegative(request.RequiredMarshals))
            {
                return new BadRequestObjectResult(new { message = "Required marshals count must be non-negative" });
            }

            LocationEntity? locationEntity = await _locationRepository.GetAsync(eventId, locationId);

            if (locationEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Location not found" });
            }

            // Update core properties
            locationEntity.Name = request.Name;
            locationEntity.Description = request.Description;
            locationEntity.Latitude = request.Latitude;
            locationEntity.Longitude = request.Longitude;
            locationEntity.RequiredMarshals = request.RequiredMarshals;
            locationEntity.What3Words = request.What3Words ?? string.Empty;
            locationEntity.StartTime = request.StartTime;
            locationEntity.EndTime = request.EndTime;

            // Update style and terminology (only non-null values)
            locationEntity.ApplyStyleUpdates(
                styleType: request.StyleType,
                styleColor: request.StyleColor,
                styleBackgroundShape: request.StyleBackgroundShape,
                styleBackgroundColor: request.StyleBackgroundColor,
                styleBorderColor: request.StyleBorderColor,
                styleIconColor: request.StyleIconColor,
                styleSize: request.StyleSize,
                styleMapRotation: request.StyleMapRotation,
                peopleTerm: request.PeopleTerm,
                checkpointTerm: request.CheckpointTerm
            );

            // Update dynamic checkpoint settings
            locationEntity.IsDynamic = request.IsDynamic;
            if (request.LocationUpdateScopeConfigurations != null)
            {
                locationEntity.LocationUpdateScopeJson = JsonSerializer.Serialize(request.LocationUpdateScopeConfigurations);
            }

            // Recalculate area assignments based on new location
            AreaEntity defaultArea = await EnsureDefaultAreaExists(eventId);
            IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(eventId);
            List<string> areaIds = GeometryHelper.CalculateCheckpointAreas(
                request.Latitude,
                request.Longitude,
                areas,
                defaultArea.RowKey
            );
            locationEntity.AreaIdsJson = JsonSerializer.Serialize(areaIds);

            await _locationRepository.UpdateAsync(locationEntity);

            LocationResponse response = locationEntity.ToResponse();

            _logger.LogInformation("Location updated: {LocationId}", locationId);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    [Function("DeleteLocation")]
    public async Task<IActionResult> DeleteLocation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "locations/{eventId}/{locationId}")] HttpRequest req,
        string eventId,
        string locationId)
    {
        try
        {
            // Delete all assignments for this location first to avoid orphaned records
            await _assignmentRepository.DeleteAllByLocationAsync(eventId, locationId);

            await _locationRepository.DeleteAsync(eventId, locationId);

            _logger.LogInformation("Location deleted: {LocationId} (including all assignments)", locationId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting location");
            return new StatusCodeResult(500);
        }
    }

#pragma warning disable MA0051
    [Function("ImportLocations")]
    public async Task<IActionResult> ImportLocations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locations/import/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Get deleteExisting parameter from query string
            bool deleteExisting = req.Query["deleteExisting"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase);

            // Get the uploaded file
            if (req.Form.Files.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "No file uploaded" });
            }

            IFormFile csvFile = req.Form.Files[0];
            if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return new BadRequestObjectResult(new { message = "File must be a .csv file" });
            }

            // Parse the CSV file
            CsvParserService.CsvParseResult parseResult;
            await using (Stream stream = csvFile.OpenReadStream())
            {
                parseResult = CsvParserService.ParseLocationsCsv(stream);
            }

            if (parseResult.Locations.Count == 0 && parseResult.Errors.Count > 0)
            {
                return new BadRequestObjectResult(new ImportLocationsResponse(0, 0, parseResult.Errors));
            }

            // Delete existing locations if requested
            if (deleteExisting)
            {
                await _locationRepository.DeleteAllByEventAsync(eventId);
                await _assignmentRepository.DeleteAllByEventAsync(eventId);
            }

            // Load existing locations to check for duplicates
            IEnumerable<LocationEntity> existingLocationsList = await _locationRepository.GetByEventAsync(eventId);
            Dictionary<string, LocationEntity> existingLocations = existingLocationsList
                .ToDictionary(l => l.Name.ToLower(), l => l);

            // Preload all marshals for efficient lookup during import
            IEnumerable<MarshalEntity> existingMarshalsList = await _marshalRepository.GetByEventAsync(eventId);
            Dictionary<string, MarshalEntity> marshalCache = existingMarshalsList
                .ToDictionary(m => m.Name.ToLower(), m => m);

            // Create or update locations and assignments
            int locationsCreated = 0;
            int locationsUpdated = 0;
            int assignmentsCreated = 0;

            foreach (CsvParserService.LocationCsvRow row in parseResult.Locations)
            {
                try
                {
                    bool isUpdate = existingLocations.TryGetValue(row.Name.ToLower(), out LocationEntity? existing);

                    if (isUpdate && existing != null)
                    {
                        int newAssignments = await UpdateExistingLocation(existing, row, eventId, marshalCache);
                        assignmentsCreated += newAssignments;
                        locationsUpdated++;
                    }
                    else
                    {
                        int newAssignments = await CreateNewLocation(row, eventId, marshalCache);
                        assignmentsCreated += newAssignments;
                        locationsCreated++;
                    }

                    // Validate coordinates during import (no extra DB fetch needed)
                    // Use tolerance for floating point comparison (coordinates at exactly 0,0 are unlikely valid locations)
                    const double tolerance = 0.0001;
                    if ((Math.Abs(row.Latitude) < tolerance && Math.Abs(row.Longitude) < tolerance) || double.IsNaN(row.Latitude) || double.IsNaN(row.Longitude))
                    {
                        parseResult.Errors.Add($"Warning: Location '{row.Name}' has no valid coordinates (lat: {row.Latitude}, long: {row.Longitude})");
                    }
                }
                catch (Exception ex)
                {
                    parseResult.Errors.Add($"Failed to process location '{row.Name}': {ex.Message}");
                }
            }

            _logger.LogInformation("Imported {LocationsCreated} new, updated {LocationsUpdated} existing locations, and created {AssignmentsCreated} assignments for event {EventId}", locationsCreated, locationsUpdated, assignmentsCreated, eventId);

            return new OkObjectResult(new ImportLocationsResponse(locationsCreated + locationsUpdated, assignmentsCreated, parseResult.Errors));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing locations");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    private async Task<string> FindOrCreateMarshal(string eventId, string marshalName, Dictionary<string, MarshalEntity> marshalCache)
    {
        // Try to find existing marshal in cache (O(1) lookup)
        string normalizedName = marshalName.ToLower();
        if (marshalCache.TryGetValue(normalizedName, out MarshalEntity? existingMarshal))
        {
            return existingMarshal.MarshalId;
        }

        // Create new marshal if not found
        string newMarshalId = Guid.NewGuid().ToString();
        MarshalEntity newMarshal = new()
        {
            PartitionKey = eventId,
            RowKey = newMarshalId,
            EventId = eventId,
            MarshalId = newMarshalId,
            Name = marshalName
        };
        await _marshalRepository.AddAsync(newMarshal);

        // Add to cache for subsequent lookups within this import
        marshalCache[normalizedName] = newMarshal;

        _logger.LogInformation("Created new marshal from CSV: {MarshalId} - {MarshalName}", newMarshalId, marshalName);

        return newMarshalId;
    }

    private async Task<int> UpdateExistingLocation(
        LocationEntity locationEntity,
        CsvParserService.LocationCsvRow row,
        string eventId,
        Dictionary<string, MarshalEntity> marshalCache)
    {
        int assignmentsCreated = 0;

        // Only update lat/long if CSV has values
        if (row.HasLatLong)
        {
            locationEntity.Latitude = row.Latitude;
            locationEntity.Longitude = row.Longitude;
        }

        // Update What3Words if CSV has value
        if (!string.IsNullOrWhiteSpace(row.What3Words))
        {
            locationEntity.What3Words = row.What3Words;
        }

        if (!string.IsNullOrWhiteSpace(row.Description))
        {
            locationEntity.Description = row.Description;
        }

        // Update marshals only if CSV has marshal names
        if (row.MarshalNames.Count > 0)
        {
            assignmentsCreated = await ReplaceLocationAssignments(locationEntity.RowKey, row.MarshalNames, eventId, marshalCache);
            locationEntity.RequiredMarshals = row.MarshalNames.Count;
        }

        await _locationRepository.UpdateAsync(locationEntity);

        return assignmentsCreated;
    }

    private async Task<int> CreateNewLocation(
        CsvParserService.LocationCsvRow row,
        string eventId,
        Dictionary<string, MarshalEntity> marshalCache)
    {
        LocationEntity locationEntity = new()
        {
            PartitionKey = eventId,
            RowKey = Guid.NewGuid().ToString(),
            EventId = eventId,
            Name = row.Name,
            Description = row.Description,
            Latitude = row.Latitude,
            Longitude = row.Longitude,
            RequiredMarshals = row.MarshalNames.Count > 0 ? row.MarshalNames.Count : 1,
            What3Words = row.What3Words
        };

        await _locationRepository.AddAsync(locationEntity);

        int assignmentsCreated = await CreateAssignmentsForLocation(locationEntity.RowKey, row.MarshalNames, eventId, marshalCache);

        return assignmentsCreated;
    }

    private async Task<int> ReplaceLocationAssignments(
        string locationId,
        List<string> marshalNames,
        string eventId,
        Dictionary<string, MarshalEntity> marshalCache)
    {
        // Delete existing assignments for this location
        await _assignmentRepository.DeleteAllByLocationAsync(eventId, locationId);

        // Create new assignments
        return await CreateAssignmentsForLocation(locationId, marshalNames, eventId, marshalCache);
    }

    private async Task<int> CreateAssignmentsForLocation(
        string locationId,
        List<string> marshalNames,
        string eventId,
        Dictionary<string, MarshalEntity> marshalCache)
    {
        int assignmentsCreated = 0;

        foreach (string marshalName in marshalNames)
        {
            // Find or create marshal using cached lookup
            string marshalId = await FindOrCreateMarshal(eventId, marshalName, marshalCache);

            AssignmentEntity assignmentEntity = new()
            {
                PartitionKey = eventId,
                RowKey = Guid.NewGuid().ToString(),
                EventId = eventId,
                LocationId = locationId,
                MarshalId = marshalId,
                MarshalName = marshalName
            };

            await _assignmentRepository.AddAsync(assignmentEntity);
            assignmentsCreated++;
        }

        return assignmentsCreated;
    }

    [Function("BulkUpdateLocationTimes")]
    public async Task<IActionResult> BulkUpdateLocationTimes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locations/bulk-update-times/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            BulkUpdateLocationTimesRequest? request = JsonSerializer.Deserialize<BulkUpdateLocationTimesRequest>(
                body,
                FunctionHelpers.JsonOptions
            );

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            IEnumerable<LocationEntity> locationEntities = await _locationRepository.GetByEventAsync(eventId);

            // Prepare updates and execute in parallel
            List<Task> updateTasks = [];
            int updatedCount = 0;

            foreach (LocationEntity location in locationEntities)
            {
                bool updated = false;

                if (location.StartTime.HasValue)
                {
                    location.StartTime = location.StartTime.Value.Add(request.TimeDelta);
                    updated = true;
                }

                if (location.EndTime.HasValue)
                {
                    location.EndTime = location.EndTime.Value.Add(request.TimeDelta);
                    updated = true;
                }

                if (updated)
                {
                    updateTasks.Add(_locationRepository.UpdateAsync(location));
                    updatedCount++;
                }
            }

            // Execute all updates in parallel
            await Task.WhenAll(updateTasks);

            _logger.LogInformation("Bulk updated {UpdatedCount} location times for event {EventId}", updatedCount, eventId);

            return new OkObjectResult(new { updatedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk updating location times");
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Updates the location of a dynamic checkpoint (for lead car/sweep vehicle tracking)
    /// </summary>
#pragma warning disable MA0051
    [Function("UpdateCheckpointLocation")]
    public async Task<IActionResult> UpdateCheckpointLocation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locations/{eventId}/{locationId}/update-position")] HttpRequest req,
        string eventId,
        string locationId)
    {
        try
        {
            // Extract session token from Authorization header
            string? sessionToken = req.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authorization header required" });
            }

            // Validate session and get claims
            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            (UpdateCheckpointLocationRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<UpdateCheckpointLocationRequest>(req);
            if (error != null) return error;

            // Validate GPS coordinates
            if (!Validators.IsValidCoordinates(request!.Latitude, request.Longitude))
            {
                return new BadRequestObjectResult(new { message = "Invalid GPS coordinates" });
            }

            // Get the checkpoint
            LocationEntity? checkpoint = await _locationRepository.GetAsync(eventId, locationId);
            if (checkpoint == null)
            {
                return new NotFoundObjectResult(new { message = "Checkpoint not found" });
            }

            // Verify checkpoint is dynamic
            if (!checkpoint.IsDynamic)
            {
                return new BadRequestObjectResult(new { message = "This checkpoint is not configured as a dynamic checkpoint" });
            }

            // Check if user has permission to update this checkpoint's location
            bool hasPermission = await CanUserUpdateCheckpointLocation(claims, checkpoint, eventId);
            if (!hasPermission)
            {
                return new UnauthorizedObjectResult(new { message = "You don't have permission to update this checkpoint's location" });
            }

            // If source is another checkpoint, get its coordinates
            double newLatitude = request.Latitude;
            double newLongitude = request.Longitude;

            if (request.SourceType == "checkpoint" && !string.IsNullOrEmpty(request.SourceCheckpointId))
            {
                LocationEntity? sourceCheckpoint = await _locationRepository.GetAsync(eventId, request.SourceCheckpointId);
                if (sourceCheckpoint == null)
                {
                    return new BadRequestObjectResult(new { message = "Source checkpoint not found" });
                }
                newLatitude = sourceCheckpoint.Latitude;
                newLongitude = sourceCheckpoint.Longitude;
            }

            // Update the checkpoint location
            checkpoint.Latitude = newLatitude;
            checkpoint.Longitude = newLongitude;
            checkpoint.LastLocationUpdate = DateTime.UtcNow;
            checkpoint.LastUpdatedByPersonId = claims.PersonId;

            // Recalculate area assignments based on new location
            AreaEntity defaultArea = await EnsureDefaultAreaExists(eventId);
            IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(eventId);
            List<string> areaIds = GeometryHelper.CalculateCheckpointAreas(
                newLatitude,
                newLongitude,
                areas,
                defaultArea.RowKey
            );
            checkpoint.AreaIdsJson = JsonSerializer.Serialize(areaIds);

            await _locationRepository.UpdateAsync(checkpoint);

            _logger.LogInformation("Dynamic checkpoint {LocationId} location updated by {PersonId} to ({Latitude}, {Longitude})", locationId, claims.PersonId, newLatitude, newLongitude);

            return new OkObjectResult(new UpdateCheckpointLocationResponse(
                true,
                locationId,
                newLatitude,
                newLongitude,
                checkpoint.LastLocationUpdate.Value,
                "Location updated successfully"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating checkpoint location");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Gets all dynamic checkpoints for an event (for polling)
    /// </summary>
    [Function("GetDynamicCheckpoints")]
    public async Task<IActionResult> GetDynamicCheckpoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/dynamic-checkpoints")] HttpRequest req,
        string eventId)
    {
        try
        {
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);

            List<DynamicCheckpointResponse> dynamicCheckpoints = [.. allLocations
                .Where(l => l.IsDynamic)
                .Select(l => new DynamicCheckpointResponse(
                    l.RowKey,
                    l.Name,
                    l.Latitude,
                    l.Longitude,
                    l.LastLocationUpdate,
                    l.LastUpdatedByPersonId
                ))];

            return new OkObjectResult(dynamicCheckpoints);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dynamic checkpoints");
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Checks if a user has permission to update a checkpoint's location based on scope configurations
    /// </summary>
#pragma warning disable MA0051
    private async Task<bool> CanUserUpdateCheckpointLocation(UserClaims claims, LocationEntity checkpoint, string eventId)
    {
        // Event admins and system admins always have permission
        if (claims.IsSystemAdmin || claims.IsEventAdmin)
        {
            return true;
        }

        // Parse the scope configurations
        List<ScopeConfiguration> scopeConfigs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(checkpoint.LocationUpdateScopeJson) ?? [];

        // If no scopes configured, no one can update (except admins, handled above)
        if (scopeConfigs.Count == 0)
        {
            return false;
        }

        // Get the marshal for this user if they have one
        if (string.IsNullOrEmpty(claims.MarshalId))
        {
            return false;
        }

        // Get marshal's assignments to determine areas and checkpoints they're assigned to
        MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, claims.MarshalId);
        if (marshal == null)
        {
            return false;
        }

        IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, claims.MarshalId);
        List<string> assignedLocationIds = [.. assignments.Select(a => a.LocationId)];

        // Get the areas the marshal is assigned to
        IEnumerable<LocationEntity> assignedLocations = await Task.WhenAll(
            assignedLocationIds.Select(id => _locationRepository.GetAsync(eventId, id))
        ).ContinueWith(t => t.Result.Where(l => l != null).Cast<LocationEntity>());

        HashSet<string> assignedAreaIds = [];
        foreach (LocationEntity loc in assignedLocations)
        {
            List<string> locAreaIds = JsonSerializer.Deserialize<List<string>>(loc.AreaIdsJson ?? "[]") ?? [];
            foreach (string areaId in locAreaIds)
            {
                assignedAreaIds.Add(areaId);
            }
        }

        // Get areas where this marshal is a lead (from EventRoles)
        // Note: marshal is already fetched above (line 879)
        List<string> areaLeadForAreaIds = [];
        if (marshal != null && !string.IsNullOrEmpty(marshal.PersonId))
        {
            IEnumerable<EventRoleEntity> roles = await _eventRoleRepository.GetByPersonAndEventAsync(marshal.PersonId, eventId);
            EventRoleEntity? areaLeadRole = roles.FirstOrDefault(r => r.Role == Constants.RoleEventAreaLead);
            if (areaLeadRole != null)
            {
                areaLeadForAreaIds = JsonSerializer.Deserialize<List<string>>(areaLeadRole.AreaIdsJson) ?? [];
            }
        }

        // Create marshal context for scope evaluation
        ScopeEvaluator.MarshalContext marshalContext = new(
            claims.MarshalId,
            assignedAreaIds.ToList(),
            assignedLocationIds,
            areaLeadForAreaIds
        );

        // Build checkpoint lookup (just this checkpoint for now)
        Dictionary<string, LocationEntity> checkpointLookup = new()
        {
            { checkpoint.RowKey, checkpoint }
        };

        // Evaluate the scope configurations
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            scopeConfigs,
            marshalContext,
            checkpointLookup
        );

        return result.IsRelevant;
    }
#pragma warning restore MA0051

    // Helper method to ensure default area exists
    private async Task<AreaEntity> EnsureDefaultAreaExists(string eventId)
    {
        AreaEntity? defaultArea = await _areaRepository.GetDefaultAreaAsync(eventId);

        if (defaultArea == null)
        {
            defaultArea = new AreaEntity
            {
                PartitionKey = eventId,
                RowKey = Guid.NewGuid().ToString(),
                EventId = eventId,
                Name = Constants.DefaultAreaName,
                Description = Constants.DefaultAreaDescription,
                ContactsJson = "[]",
                PolygonJson = "[]",
                IsDefault = true,
                DisplayOrder = 0
            };

            await _areaRepository.AddAsync(defaultArea);
            _logger.LogInformation("Created default area for event {EventId}", eventId);
        }

        return defaultArea;
    }

    /// <summary>
    /// Create checklist items scoped to a specific checkpoint from pending items.
    /// </summary>
    private async Task CreatePendingChecklistItems(
        string eventId,
        string locationId,
        List<PendingChecklistItem>? pendingItems,
        string adminEmail)
    {
        if (pendingItems == null || pendingItems.Count == 0) return;

        int displayOrder = 0;
        foreach (PendingChecklistItem pendingItem in pendingItems)
        {
            if (string.IsNullOrWhiteSpace(pendingItem.Text)) continue;

            string itemId = Guid.NewGuid().ToString();
            List<ScopeConfiguration> scopeConfig =
            [
                new ScopeConfiguration { Scope = "EveryoneAtCheckpoints", ItemType = "Checkpoint", Ids = [locationId] }
            ];

            ChecklistItemEntity checklistEntity = new()
            {
                PartitionKey = eventId,
                RowKey = itemId,
                EventId = eventId,
                ItemId = itemId,
                Text = InputSanitizer.SanitizeDescription(pendingItem.Text),
                ScopeConfigurationsJson = JsonSerializer.Serialize(scopeConfig),
                DisplayOrder = displayOrder++,
                IsRequired = false,
                CreatedByAdminEmail = adminEmail,
                CreatedDate = DateTime.UtcNow
            };

            await _checklistItemRepository.AddAsync(checklistEntity);
            _logger.LogInformation("Checklist item {ItemId} created for location {LocationId}", itemId, locationId);
        }
    }

    /// <summary>
    /// Create notes scoped to a specific checkpoint from pending notes.
    /// </summary>
    private async Task CreatePendingNotes(
        string eventId,
        string locationId,
        List<PendingNote>? pendingNotes,
        string adminEmail)
    {
        if (pendingNotes == null || pendingNotes.Count == 0) return;

        int displayOrder = 0;
        foreach (PendingNote pendingNote in pendingNotes)
        {
            if (string.IsNullOrWhiteSpace(pendingNote.Title) && string.IsNullOrWhiteSpace(pendingNote.Content)) continue;

            string noteId = Guid.NewGuid().ToString();
            List<ScopeConfiguration> scopeConfig =
            [
                new ScopeConfiguration { Scope = "EveryoneAtCheckpoints", ItemType = "Checkpoint", Ids = [locationId] }
            ];

            NoteEntity noteEntity = new()
            {
                PartitionKey = eventId,
                RowKey = noteId,
                EventId = eventId,
                NoteId = noteId,
                Title = InputSanitizer.SanitizeName(pendingNote.Title ?? "Untitled"),
                Content = InputSanitizer.SanitizeDescription(pendingNote.Content ?? string.Empty),
                ScopeConfigurationsJson = JsonSerializer.Serialize(scopeConfig),
                DisplayOrder = displayOrder++,
                Priority = Constants.NotePriorityNormal,
                Category = string.Empty,
                IsPinned = false,
                CreatedByPersonId = string.Empty,
                CreatedByName = !string.IsNullOrEmpty(adminEmail) ? adminEmail : "Admin",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _noteRepository.AddAsync(noteEntity);
            _logger.LogInformation("Note {NoteId} created for location {LocationId}", noteId, locationId);
        }
    }
}
