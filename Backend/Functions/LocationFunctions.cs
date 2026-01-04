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

    public LocationFunctions(
        ILogger<LocationFunctions> logger,
        ILocationRepository locationRepository,
        IMarshalRepository marshalRepository,
        IAssignmentRepository assignmentRepository,
        IChecklistItemRepository checklistItemRepository,
        INoteRepository noteRepository,
        IAreaRepository areaRepository)
    {
        _logger = logger;
        _locationRepository = locationRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _checklistItemRepository = checklistItemRepository;
        _noteRepository = noteRepository;
        _areaRepository = areaRepository;
    }

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

            LocationEntity locationEntity = new()
            {
                PartitionKey = request.EventId,
                RowKey = Guid.NewGuid().ToString(),
                EventId = request.EventId,
                Name = request.Name,
                Description = request.Description,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RequiredMarshals = request.RequiredMarshals,
                What3Words = request.What3Words ?? string.Empty,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                AreaIdsJson = JsonSerializer.Serialize(areaIds)
            };

            await _locationRepository.AddAsync(locationEntity);

            string locationId = locationEntity.RowKey;

            // Create pending checklist items scoped to this checkpoint
            if (request.PendingNewChecklistItems != null && request.PendingNewChecklistItems.Count > 0)
            {
                string adminEmail = req.Headers[Constants.AdminEmailHeader].ToString();
                int displayOrder = 0;

                foreach (PendingChecklistItem pendingItem in request.PendingNewChecklistItems)
                {
                    if (string.IsNullOrWhiteSpace(pendingItem.Text)) continue;

                    string itemId = Guid.NewGuid().ToString();
                    List<ScopeConfiguration> scopeConfig =
                    [
                        new ScopeConfiguration { Scope = "EveryoneAtCheckpoints", ItemType = "Checkpoint", Ids = [locationId] }
                    ];

                    ChecklistItemEntity checklistEntity = new()
                    {
                        PartitionKey = request.EventId,
                        RowKey = itemId,
                        EventId = request.EventId,
                        ItemId = itemId,
                        Text = InputSanitizer.SanitizeDescription(pendingItem.Text),
                        ScopeConfigurationsJson = JsonSerializer.Serialize(scopeConfig),
                        DisplayOrder = displayOrder++,
                        IsRequired = false,
                        CreatedByAdminEmail = adminEmail,
                        CreatedDate = DateTime.UtcNow
                    };

                    await _checklistItemRepository.AddAsync(checklistEntity);
                    _logger.LogInformation($"Checklist item {itemId} created for new location {locationId}");
                }
            }

            // Create pending notes scoped to this checkpoint
            if (request.PendingNewNotes != null && request.PendingNewNotes.Count > 0)
            {
                string adminEmail = req.Headers[Constants.AdminEmailHeader].ToString();
                int displayOrder = 0;

                foreach (PendingNote pendingNote in request.PendingNewNotes)
                {
                    if (string.IsNullOrWhiteSpace(pendingNote.Title) && string.IsNullOrWhiteSpace(pendingNote.Content)) continue;

                    string noteId = Guid.NewGuid().ToString();
                    List<ScopeConfiguration> scopeConfig =
                    [
                        new ScopeConfiguration { Scope = "EveryoneAtCheckpoints", ItemType = "Checkpoint", Ids = [locationId] }
                    ];

                    NoteEntity noteEntity = new()
                    {
                        PartitionKey = request.EventId,
                        RowKey = noteId,
                        EventId = request.EventId,
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
                    _logger.LogInformation($"Note {noteId} created for new location {locationId}");
                }
            }

            LocationResponse response = locationEntity.ToResponse();

            _logger.LogInformation($"Location created: {locationEntity.RowKey}");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location");
            return new StatusCodeResult(500);
        }
    }

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
            List<LocationEntity> locationsList = locationEntities.ToList();

            // Check if any checkpoints are missing area assignments
            List<LocationEntity> checkpointsNeedingAreas = locationsList
                .Where(l => string.IsNullOrEmpty(l.AreaIdsJson) || l.AreaIdsJson == "[]")
                .ToList();

            if (checkpointsNeedingAreas.Count > 0)
            {
                _logger.LogInformation($"Found {checkpointsNeedingAreas.Count} checkpoints without area assignments. Calculating...");

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

                _logger.LogInformation($"Automatically assigned areas to {checkpointsNeedingAreas.Count} checkpoints");
            }

            List<LocationResponse> locations = locationsList
                .Select(l => l.ToResponse())
                .ToList();

            return new OkObjectResult(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting locations");
            return new StatusCodeResult(500);
        }
    }

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

            locationEntity.Name = request.Name;
            locationEntity.Description = request.Description;
            locationEntity.Latitude = request.Latitude;
            locationEntity.Longitude = request.Longitude;
            locationEntity.RequiredMarshals = request.RequiredMarshals;
            locationEntity.What3Words = request.What3Words ?? string.Empty;
            locationEntity.StartTime = request.StartTime;
            locationEntity.EndTime = request.EndTime;
            // Update checkpoint style if provided
            if (request.StyleType != null) locationEntity.StyleType = request.StyleType;
            if (request.StyleColor != null) locationEntity.StyleColor = request.StyleColor;
            if (request.StyleBackgroundShape != null) locationEntity.StyleBackgroundShape = request.StyleBackgroundShape;
            if (request.StyleBackgroundColor != null) locationEntity.StyleBackgroundColor = request.StyleBackgroundColor;
            if (request.StyleBorderColor != null) locationEntity.StyleBorderColor = request.StyleBorderColor;
            if (request.StyleIconColor != null) locationEntity.StyleIconColor = request.StyleIconColor;
            if (request.StyleSize != null) locationEntity.StyleSize = request.StyleSize;
            // Update terminology if provided
            if (request.PeopleTerm != null) locationEntity.PeopleTerm = request.PeopleTerm;

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

            _logger.LogInformation($"Location updated: {locationId}");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location");
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteLocation")]
    public async Task<IActionResult> DeleteLocation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "locations/{eventId}/{locationId}")] HttpRequest req,
        string eventId,
        string locationId)
    {
        try
        {
            await _locationRepository.DeleteAsync(eventId, locationId);

            _logger.LogInformation($"Location deleted: {locationId}");

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting location");
            return new StatusCodeResult(500);
        }
    }

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
            using (Stream stream = csvFile.OpenReadStream())
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

            _logger.LogInformation($"Imported {locationsCreated} new, updated {locationsUpdated} existing locations, and created {assignmentsCreated} assignments for event {eventId}");

            return new OkObjectResult(new ImportLocationsResponse(locationsCreated + locationsUpdated, assignmentsCreated, parseResult.Errors));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing locations");
            return new StatusCodeResult(500);
        }
    }

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

        _logger.LogInformation($"Created new marshal from CSV: {newMarshalId} - {marshalName}");

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
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
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

            _logger.LogInformation($"Bulk updated {updatedCount} location times for event {eventId}");

            return new OkObjectResult(new { updatedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk updating location times");
            return new StatusCodeResult(500);
        }
    }

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
            _logger.LogInformation($"Created default area for event {eventId}");
        }

        return defaultArea;
    }
}
