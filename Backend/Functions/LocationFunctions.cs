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

namespace VolunteerCheckin.Functions.Functions;

public class LocationFunctions
{
    private readonly ILogger<LocationFunctions> _logger;
    private readonly TableStorageService _tableStorage;
    private readonly CsvParserService _csvParser;

    public LocationFunctions(ILogger<LocationFunctions> logger, TableStorageService tableStorage, CsvParserService csvParser)
    {
        _logger = logger;
        _tableStorage = tableStorage;
        _csvParser = csvParser;
    }

    private static bool IsValidWhat3Words(string? what3Words)
    {
        if (string.IsNullOrWhiteSpace(what3Words))
        {
            return true; // Optional field
        }

        // Pattern: word.word.word or word/word/word where word is lowercase alpha 1-20 chars
        string pattern = @"^[a-z]{1,20}[./][a-z]{1,20}[./][a-z]{1,20}$";
        if (!Regex.IsMatch(what3Words, pattern))
        {
            return false;
        }

        // Ensure consistent separator (all dots or all slashes)
        bool hasDots = what3Words.Contains('.');
        bool hasSlashes = what3Words.Contains('/');
        return hasDots != hasSlashes; // XOR - can't have both
    }

    [Function("CreateLocation")]
    public async Task<IActionResult> CreateLocation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locations")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CreateLocationRequest? request = JsonSerializer.Deserialize<CreateLocationRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            if (!IsValidWhat3Words(request.What3Words))
            {
                return new BadRequestObjectResult(new { message = "Invalid What3Words format. Must be in format word.word.word or word/word/word where each word is lowercase letters (1-20 characters)" });
            }

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
                What3Words = request.What3Words ?? string.Empty
            };

            TableClient table = _tableStorage.GetLocationsTable();
            await table.AddEntityAsync(locationEntity);

            LocationResponse response = new(
                locationEntity.RowKey,
                locationEntity.EventId,
                locationEntity.Name,
                locationEntity.Description,
                locationEntity.Latitude,
                locationEntity.Longitude,
                locationEntity.RequiredMarshals,
                locationEntity.CheckedInCount,
                locationEntity.What3Words
            );

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
            TableClient table = _tableStorage.GetLocationsTable();
            Response<LocationEntity> locationEntity = await table.GetEntityAsync<LocationEntity>(eventId, locationId);

            LocationResponse response = new(
                locationEntity.Value.RowKey,
                locationEntity.Value.EventId,
                locationEntity.Value.Name,
                locationEntity.Value.Description,
                locationEntity.Value.Latitude,
                locationEntity.Value.Longitude,
                locationEntity.Value.RequiredMarshals,
                locationEntity.Value.CheckedInCount,
                locationEntity.Value.What3Words
            );

            return new OkObjectResult(response);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Location not found" });
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
            TableClient table = _tableStorage.GetLocationsTable();
            List<LocationResponse> locations = [];

            await foreach (LocationEntity locationEntity in table.QueryAsync<LocationEntity>(l => l.PartitionKey == eventId))
            {
                locations.Add(new LocationResponse(
                    locationEntity.RowKey,
                    locationEntity.EventId,
                    locationEntity.Name,
                    locationEntity.Description,
                    locationEntity.Latitude,
                    locationEntity.Longitude,
                    locationEntity.RequiredMarshals,
                    locationEntity.CheckedInCount,
                    locationEntity.What3Words
                ));
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
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CreateLocationRequest? request = JsonSerializer.Deserialize<CreateLocationRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            if (!IsValidWhat3Words(request.What3Words))
            {
                return new BadRequestObjectResult(new { message = "Invalid What3Words format. Must be in format word.word.word or word/word/word where each word is lowercase letters (1-20 characters)" });
            }

            TableClient table = _tableStorage.GetLocationsTable();
            Response<LocationEntity> locationEntity = await table.GetEntityAsync<LocationEntity>(eventId, locationId);

            locationEntity.Value.Name = request.Name;
            locationEntity.Value.Description = request.Description;
            locationEntity.Value.Latitude = request.Latitude;
            locationEntity.Value.Longitude = request.Longitude;
            locationEntity.Value.RequiredMarshals = request.RequiredMarshals;
            locationEntity.Value.What3Words = request.What3Words ?? string.Empty;

            await table.UpdateEntityAsync(locationEntity.Value, locationEntity.Value.ETag);

            LocationResponse response = new(
                locationEntity.Value.RowKey,
                locationEntity.Value.EventId,
                locationEntity.Value.Name,
                locationEntity.Value.Description,
                locationEntity.Value.Latitude,
                locationEntity.Value.Longitude,
                locationEntity.Value.RequiredMarshals,
                locationEntity.Value.CheckedInCount,
                locationEntity.Value.What3Words
            );

            _logger.LogInformation($"Location updated: {locationId}");

            return new OkObjectResult(response);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Location not found" });
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
            TableClient table = _tableStorage.GetLocationsTable();
            await table.DeleteEntityAsync(eventId, locationId);

            _logger.LogInformation($"Location deleted: {locationId}");

            return new NoContentResult();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Location not found" });
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

            TableClient locationsTable = _tableStorage.GetLocationsTable();
            TableClient assignmentsTable = _tableStorage.GetAssignmentsTable();

            // Delete existing locations if requested
            if (deleteExisting)
            {
                await foreach (LocationEntity location in locationsTable.QueryAsync<LocationEntity>(l => l.PartitionKey == eventId))
                {
                    await locationsTable.DeleteEntityAsync(eventId, location.RowKey);
                }

                await foreach (AssignmentEntity assignment in assignmentsTable.QueryAsync<AssignmentEntity>(a => a.PartitionKey == eventId))
                {
                    await assignmentsTable.DeleteEntityAsync(eventId, assignment.RowKey);
                }
            }

            // Load existing locations to check for duplicates
            Dictionary<string, LocationEntity> existingLocations = [];
            await foreach (LocationEntity location in locationsTable.QueryAsync<LocationEntity>(l => l.PartitionKey == eventId))
            {
                existingLocations[location.Name.ToLower()] = location;
            }

            // Create or update locations and assignments
            int locationsCreated = 0;
            int locationsUpdated = 0;
            int assignmentsCreated = 0;

            foreach (CsvParserService.LocationCsvRow row in parseResult.Locations)
            {
                try
                {
                    LocationEntity? locationEntity;
                    bool isUpdate = existingLocations.TryGetValue(row.Name.ToLower(), out LocationEntity? existing);

                    if (isUpdate && existing != null)
                    {
                        // Update existing location
                        locationEntity = existing;

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
                            // Delete existing assignments for this location
                            await foreach (AssignmentEntity assignment in assignmentsTable.QueryAsync<AssignmentEntity>(
                                a => a.PartitionKey == eventId && a.LocationId == locationEntity.RowKey))
                            {
                                await assignmentsTable.DeleteEntityAsync(eventId, assignment.RowKey);
                            }

                            // Create new assignments
                            TableClient marshalsTable = _tableStorage.GetMarshalsTable();
                            foreach (string marshalName in row.MarshalNames)
                            {
                                // Find or create marshal
                                string marshalId = await FindOrCreateMarshal(marshalsTable, eventId, marshalName);

                                AssignmentEntity assignmentEntity = new()
                                {
                                    PartitionKey = eventId,
                                    RowKey = Guid.NewGuid().ToString(),
                                    EventId = eventId,
                                    LocationId = locationEntity.RowKey,
                                    MarshalId = marshalId,
                                    MarshalName = marshalName
                                };

                                await assignmentsTable.AddEntityAsync(assignmentEntity);
                                assignmentsCreated++;
                            }

                            locationEntity.RequiredMarshals = row.MarshalNames.Count;
                        }

                        await locationsTable.UpdateEntityAsync(locationEntity, locationEntity.ETag);
                        locationsUpdated++;
                    }
                    else
                    {
                        // Create new location
                        locationEntity = new LocationEntity
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

                        await locationsTable.AddEntityAsync(locationEntity);
                        locationsCreated++;

                        // Create assignments for each marshal
                        TableClient marshalsTable = _tableStorage.GetMarshalsTable();
                        foreach (string marshalName in row.MarshalNames)
                        {
                            // Find or create marshal
                            string marshalId = await FindOrCreateMarshal(marshalsTable, eventId, marshalName);

                            AssignmentEntity assignmentEntity = new()
                            {
                                PartitionKey = eventId,
                                RowKey = Guid.NewGuid().ToString(),
                                EventId = eventId,
                                LocationId = locationEntity.RowKey,
                                MarshalId = marshalId,
                                MarshalName = marshalName
                            };

                            await assignmentsTable.AddEntityAsync(assignmentEntity);
                            assignmentsCreated++;
                        }
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
                await foreach (LocationEntity location in locationsTable.QueryAsync<LocationEntity>(l => l.PartitionKey == eventId))
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

    private async Task<string> FindOrCreateMarshal(TableClient marshalsTable, string eventId, string marshalName)
    {
        // Try to find existing marshal by name
        await foreach (MarshalEntity m in marshalsTable.QueryAsync<MarshalEntity>(m => m.PartitionKey == eventId))
        {
            if (m.Name.Equals(marshalName, StringComparison.OrdinalIgnoreCase))
            {
                return m.MarshalId;
            }
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
        await marshalsTable.AddEntityAsync(newMarshal);
        _logger.LogInformation($"Created new marshal from CSV: {newMarshalId} - {marshalName}");

        return newMarshalId;
    }
}
