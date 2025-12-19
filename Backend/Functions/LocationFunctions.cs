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

    public LocationFunctions(ILogger<LocationFunctions> logger, TableStorageService tableStorage)
    {
        _logger = logger;
        _tableStorage = tableStorage;
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "locations/event/{eventId}")] HttpRequest req,
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
}
