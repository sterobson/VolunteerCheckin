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
    private readonly CsvParserService _csvParser;
    private readonly IAreaRepository _areaRepository;

    public LocationFunctions(
        ILogger<LocationFunctions> logger,
        ILocationRepository locationRepository,
        IMarshalRepository marshalRepository,
        IAssignmentRepository assignmentRepository,
        CsvParserService csvParser,
        IAreaRepository areaRepository)
    {
        _logger = logger;
        _locationRepository = locationRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _csvParser = csvParser;
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
                AreaId = defaultArea.RowKey
            };

            await _locationRepository.AddAsync(locationEntity);

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

            // Ensure default area exists if there are locations
            AreaEntity? defaultArea = null;
            if (locationEntities.Any())
            {
                defaultArea = await EnsureDefaultAreaExists(eventId);
            }

            // Get all areas for name lookup
            IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(eventId);
            Dictionary<string, string> areaNames = areas.ToDictionary(a => a.RowKey, a => a.Name);

            List<LocationResponse> locations = new();
            foreach (LocationEntity location in locationEntities)
            {
                // Auto-assign to default area if AreaId is null
                if (string.IsNullOrEmpty(location.AreaId) && defaultArea != null)
                {
                    location.AreaId = defaultArea.RowKey;
                    await _locationRepository.UpdateAsync(location);
                }

                string? areaName = location.AreaId != null && areaNames.TryGetValue(location.AreaId, out string? name)
                    ? name
                    : null;

                locations.Add(location.ToResponse(areaName));
            }

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
                        int newAssignments = await UpdateExistingLocation(existing, row, eventId);
                        assignmentsCreated += newAssignments;
                        locationsUpdated++;
                    }
                    else
                    {
                        int newAssignments = await CreateNewLocation(row, eventId);
                        assignmentsCreated += newAssignments;
                        locationsCreated++;
                    }
                }
                catch (Exception ex)
                {
                    parseResult.Errors.Add($"Failed to process location '{row.Name}': {ex.Message}");
                }
            }

            // Warn about new locations with missing or zero lat/long
            if (deleteExisting || locationsCreated > 0)
            {
                IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
                foreach (LocationEntity location in allLocations)
                {
                    if ((location.Latitude == 0 && location.Longitude == 0) || double.IsNaN(location.Latitude) || double.IsNaN(location.Longitude))
                    {
                        parseResult.Errors.Add($"Warning: Location '{location.Name}' has no valid coordinates (lat: {location.Latitude}, long: {location.Longitude})");
                    }
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

    private async Task<string> FindOrCreateMarshal(string eventId, string marshalName)
    {
        // Try to find existing marshal by name
        MarshalEntity? existingMarshal = await _marshalRepository.FindByNameAsync(eventId, marshalName);
        if (existingMarshal != null)
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
        _logger.LogInformation($"Created new marshal from CSV: {newMarshalId} - {marshalName}");

        return newMarshalId;
    }

    private async Task<int> UpdateExistingLocation(
        LocationEntity locationEntity,
        CsvParserService.LocationCsvRow row,
        string eventId)
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
            assignmentsCreated = await ReplaceLocationAssignments(locationEntity.RowKey, row.MarshalNames, eventId);
            locationEntity.RequiredMarshals = row.MarshalNames.Count;
        }

        await _locationRepository.UpdateAsync(locationEntity);

        return assignmentsCreated;
    }

    private async Task<int> CreateNewLocation(
        CsvParserService.LocationCsvRow row,
        string eventId)
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

        int assignmentsCreated = await CreateAssignmentsForLocation(locationEntity.RowKey, row.MarshalNames, eventId);

        return assignmentsCreated;
    }

    private async Task<int> ReplaceLocationAssignments(
        string locationId,
        List<string> marshalNames,
        string eventId)
    {
        // Delete existing assignments for this location
        await _assignmentRepository.DeleteAllByLocationAsync(eventId, locationId);

        // Create new assignments
        return await CreateAssignmentsForLocation(locationId, marshalNames, eventId);
    }

    private async Task<int> CreateAssignmentsForLocation(
        string locationId,
        List<string> marshalNames,
        string eventId)
    {
        int assignmentsCreated = 0;

        foreach (string marshalName in marshalNames)
        {
            // Find or create marshal
            string marshalId = await FindOrCreateMarshal(eventId, marshalName);

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
                    await _locationRepository.UpdateAsync(location);
                    updatedCount++;
                }
            }

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
