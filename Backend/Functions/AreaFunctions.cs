using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Helpers;

namespace VolunteerCheckin.Functions.Functions;

public class AreaFunctions
{
    private readonly ILogger<AreaFunctions> _logger;
    private readonly IAreaRepository _areaRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IMarshalRepository _marshalRepository;

    public AreaFunctions(
        ILogger<AreaFunctions> logger,
        IAreaRepository areaRepository,
        ILocationRepository locationRepository,
        IMarshalRepository marshalRepository)
    {
        _logger = logger;
        _areaRepository = areaRepository;
        _locationRepository = locationRepository;
        _marshalRepository = marshalRepository;
    }

    [Function("CreateArea")]
    public async Task<IActionResult> CreateArea(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "areas")] HttpRequest req)
    {
        try
        {
            (CreateAreaRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<CreateAreaRequest>(req);
            if (error != null) return error;

            // Validate contacts reference valid marshals
            foreach (AreaContact contact in request!.Contacts)
            {
                MarshalEntity? marshal = await _marshalRepository.GetAsync(request.EventId, contact.MarshalId);
                if (marshal == null)
                {
                    return new BadRequestObjectResult(new { message = $"Marshal not found: {contact.MarshalId}" });
                }
            }

            AreaEntity areaEntity = new()
            {
                PartitionKey = request.EventId,
                RowKey = Guid.NewGuid().ToString(),
                EventId = request.EventId,
                Name = request.Name,
                Description = request.Description,
                ContactsJson = JsonSerializer.Serialize(request.Contacts),
                PolygonJson = JsonSerializer.Serialize(request.Polygon ?? []),
                IsDefault = false,
                DisplayOrder = 0
            };

            await _areaRepository.AddAsync(areaEntity);

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
            AreaResponse response = areaEntity.ToResponse(checkpointCount);

            return new OkObjectResult(response);
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

            // Get checkpoint counts for each area
            List<AreaResponse> areas = new();
            foreach (AreaEntity area in areaEntities)
            {
                int checkpointCount = await _locationRepository.CountByAreaAsync(eventId, area.RowKey);
                areas.Add(area.ToResponse(checkpointCount));
            }

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

            AreaEntity? areaEntity = await _areaRepository.GetAsync(eventId, areaId);

            if (areaEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Area not found" });
            }

            // Prevent renaming default area
            if (areaEntity.IsDefault && request!.Name != Constants.DefaultAreaName)
            {
                return new BadRequestObjectResult(new { message = "Cannot rename default area" });
            }

            // Validate contacts reference valid marshals
            foreach (AreaContact contact in request.Contacts)
            {
                MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, contact.MarshalId);
                if (marshal == null)
                {
                    return new BadRequestObjectResult(new { message = $"Marshal not found: {contact.MarshalId}" });
                }
            }

            areaEntity.Name = request.Name;
            areaEntity.Description = request.Description;
            areaEntity.ContactsJson = JsonSerializer.Serialize(request.Contacts);
            areaEntity.PolygonJson = JsonSerializer.Serialize(request.Polygon ?? []);
            areaEntity.DisplayOrder = request.DisplayOrder;

            await _areaRepository.UpdateAsync(areaEntity);

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
                return new BadRequestObjectResult(new { message = "Cannot delete default area" });
            }

            // Check if area has checkpoints
            int checkpointCount = await _locationRepository.CountByAreaAsync(eventId, areaId);
            if (checkpointCount > 0)
            {
                return new BadRequestObjectResult(new {
                    message = $"Cannot delete area with {checkpointCount} checkpoints. Please reassign or delete checkpoints first."
                });
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

    [Function("BulkAssignCheckpointsToArea")]
    public async Task<IActionResult> BulkAssignCheckpointsToArea(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "areas/bulk-assign/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            (BulkAssignCheckpointsRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<BulkAssignCheckpointsRequest>(req);
            if (error != null) return error;

            // Validate area exists
            AreaEntity? area = await _areaRepository.GetAsync(eventId, request!.AreaId);
            if (area == null)
            {
                return new BadRequestObjectResult(new { message = "Area not found" });
            }

            int updatedCount = 0;
            List<string> errors = new();

            foreach (string locationId in request.LocationIds)
            {
                try
                {
                    LocationEntity? location = await _locationRepository.GetAsync(eventId, locationId);
                    if (location == null)
                    {
                        errors.Add($"Location not found: {locationId}");
                        continue;
                    }

                    location.AreaId = request.AreaId;
                    await _locationRepository.UpdateAsync(location);
                    updatedCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to update location {locationId}: {ex.Message}");
                }
            }

            _logger.LogInformation($"Bulk assigned {updatedCount} checkpoints to area {request.AreaId}");

            return new OkObjectResult(new {
                updatedCount,
                totalRequested = request.LocationIds.Count,
                errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk assigning checkpoints");
            return new StatusCodeResult(500);
        }
    }

    [Function("MigrateLocationsToDefaultArea")]
    public async Task<IActionResult> MigrateLocationsToDefaultArea(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "areas/migrate/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Ensure default area exists
            AreaEntity defaultArea = await EnsureDefaultAreaExists(eventId);

            // Get all locations for event
            IEnumerable<LocationEntity> locations = await _locationRepository.GetByEventAsync(eventId);

            int migratedCount = 0;
            foreach (LocationEntity location in locations)
            {
                if (string.IsNullOrEmpty(location.AreaId))
                {
                    location.AreaId = defaultArea.RowKey;
                    await _locationRepository.UpdateAsync(location);
                    migratedCount++;
                }
            }

            _logger.LogInformation($"Migrated {migratedCount} locations to default area for event {eventId}");

            return new OkObjectResult(new {
                message = $"Successfully migrated {migratedCount} locations to default area",
                migratedCount,
                defaultAreaId = defaultArea.RowKey
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error migrating locations");
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

            // Get area name for responses
            AreaEntity? area = await _areaRepository.GetAsync(eventId, areaId);
            string? areaName = area?.Name;

            List<LocationResponse> locations = locationEntities
                .Select(l => l.ToResponse(areaName))
                .ToList();

            return new OkObjectResult(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting locations by area");
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
