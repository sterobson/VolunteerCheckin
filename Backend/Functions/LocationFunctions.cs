using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure;
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

    [Function("CreateLocation")]
    public async Task<IActionResult> CreateLocation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locations")] HttpRequest req)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CreateLocationRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            var locationEntity = new LocationEntity
            {
                PartitionKey = request.EventId,
                RowKey = Guid.NewGuid().ToString(),
                EventId = request.EventId,
                Name = request.Name,
                Description = request.Description,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RequiredMarshals = request.RequiredMarshals
            };

            var table = _tableStorage.GetLocationsTable();
            await table.AddEntityAsync(locationEntity);

            var response = new LocationResponse(
                locationEntity.RowKey,
                locationEntity.EventId,
                locationEntity.Name,
                locationEntity.Description,
                locationEntity.Latitude,
                locationEntity.Longitude,
                locationEntity.RequiredMarshals,
                locationEntity.CheckedInCount
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
            var table = _tableStorage.GetLocationsTable();
            var locationEntity = await table.GetEntityAsync<LocationEntity>(eventId, locationId);

            var response = new LocationResponse(
                locationEntity.Value.RowKey,
                locationEntity.Value.EventId,
                locationEntity.Value.Name,
                locationEntity.Value.Description,
                locationEntity.Value.Latitude,
                locationEntity.Value.Longitude,
                locationEntity.Value.RequiredMarshals,
                locationEntity.Value.CheckedInCount
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
            var table = _tableStorage.GetLocationsTable();
            var locations = new List<LocationResponse>();

            await foreach (var locationEntity in table.QueryAsync<LocationEntity>(l => l.PartitionKey == eventId))
            {
                locations.Add(new LocationResponse(
                    locationEntity.RowKey,
                    locationEntity.EventId,
                    locationEntity.Name,
                    locationEntity.Description,
                    locationEntity.Latitude,
                    locationEntity.Longitude,
                    locationEntity.RequiredMarshals,
                    locationEntity.CheckedInCount
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
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CreateLocationRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            var table = _tableStorage.GetLocationsTable();
            var locationEntity = await table.GetEntityAsync<LocationEntity>(eventId, locationId);

            locationEntity.Value.Name = request.Name;
            locationEntity.Value.Description = request.Description;
            locationEntity.Value.Latitude = request.Latitude;
            locationEntity.Value.Longitude = request.Longitude;
            locationEntity.Value.RequiredMarshals = request.RequiredMarshals;

            await table.UpdateEntityAsync(locationEntity.Value, locationEntity.Value.ETag);

            var response = new LocationResponse(
                locationEntity.Value.RowKey,
                locationEntity.Value.EventId,
                locationEntity.Value.Name,
                locationEntity.Value.Description,
                locationEntity.Value.Latitude,
                locationEntity.Value.Longitude,
                locationEntity.Value.RequiredMarshals,
                locationEntity.Value.CheckedInCount
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
            var table = _tableStorage.GetLocationsTable();
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

            var csvFile = req.Form.Files[0];
            if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return new BadRequestObjectResult(new { message = "File must be a .csv file" });
            }

            // Parse the CSV file
            CsvParserService.CsvParseResult parseResult;
            using (var stream = csvFile.OpenReadStream())
            {
                parseResult = _csvParser.ParseLocationsCsv(stream);
            }

            if (parseResult.Locations.Count == 0 && parseResult.Errors.Count > 0)
            {
                return new BadRequestObjectResult(new ImportLocationsResponse(0, 0, parseResult.Errors));
            }

            var locationsTable = _tableStorage.GetLocationsTable();
            var assignmentsTable = _tableStorage.GetAssignmentsTable();

            // Delete existing locations if requested
            if (deleteExisting)
            {
                await foreach (var location in locationsTable.QueryAsync<LocationEntity>(l => l.PartitionKey == eventId))
                {
                    await locationsTable.DeleteEntityAsync(eventId, location.RowKey);
                }

                await foreach (var assignment in assignmentsTable.QueryAsync<AssignmentEntity>(a => a.PartitionKey == eventId))
                {
                    await assignmentsTable.DeleteEntityAsync(eventId, assignment.RowKey);
                }
            }

            // Create locations and assignments
            int locationsCreated = 0;
            int assignmentsCreated = 0;

            foreach (var row in parseResult.Locations)
            {
                try
                {
                    // Create location
                    var locationEntity = new LocationEntity
                    {
                        PartitionKey = eventId,
                        RowKey = Guid.NewGuid().ToString(),
                        EventId = eventId,
                        Name = row.Name,
                        Description = string.Empty,
                        Latitude = row.Latitude,
                        Longitude = row.Longitude,
                        RequiredMarshals = row.MarshalNames.Count > 0 ? row.MarshalNames.Count : 1
                    };

                    await locationsTable.AddEntityAsync(locationEntity);
                    locationsCreated++;

                    // Create assignments for each marshal
                    foreach (var marshalName in row.MarshalNames)
                    {
                        var assignmentEntity = new AssignmentEntity
                        {
                            PartitionKey = eventId,
                            RowKey = Guid.NewGuid().ToString(),
                            EventId = eventId,
                            LocationId = locationEntity.RowKey,
                            MarshalName = marshalName
                        };

                        await assignmentsTable.AddEntityAsync(assignmentEntity);
                        assignmentsCreated++;
                    }
                }
                catch (Exception ex)
                {
                    parseResult.Errors.Add($"Failed to create location '{row.Name}': {ex.Message}");
                }
            }

            _logger.LogInformation($"Imported {locationsCreated} locations and {assignmentsCreated} assignments for event {eventId}");

            return new OkObjectResult(new ImportLocationsResponse(locationsCreated, assignmentsCreated, parseResult.Errors));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing locations");
            return new StatusCodeResult(500);
        }
    }
}
