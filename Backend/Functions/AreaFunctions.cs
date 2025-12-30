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
                    await _locationRepository.UpdateAsync(checkpoint);
                }

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
                Color = "#667eea",
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

        // Recalculate area assignments for each checkpoint
        foreach (LocationEntity checkpoint in checkpoints)
        {
            List<string> areaIds = Helpers.GeometryHelper.CalculateCheckpointAreas(
                checkpoint.Latitude,
                checkpoint.Longitude,
                areas,
                defaultArea.RowKey
            );

            checkpoint.AreaIdsJson = JsonSerializer.Serialize(areaIds);
            await _locationRepository.UpdateAsync(checkpoint);
        }

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

            // Get marshal details for each area lead
            IEnumerable<MarshalEntity> allMarshals = await _marshalRepository.GetByEventAsync(eventId);
            List<AreaLeadResponse> areaLeads = allMarshals
                .Where(m => areaLeadIds.Contains(m.MarshalId))
                .Select(m => new AreaLeadResponse(m.MarshalId, m.Name, m.Email))
                .ToList();

            return new OkObjectResult(areaLeads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting area leads");
            return new StatusCodeResult(500);
        }
    }
}
