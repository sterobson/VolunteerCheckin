using Azure;
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

public class LayerFunctions
{
    private readonly ILogger<LayerFunctions> _logger;
    private readonly ILayerRepository _layerRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IEventRepository _eventRepository;
    private readonly ClaimsService _claimsService;

    public LayerFunctions(
        ILogger<LayerFunctions> logger,
        ILayerRepository layerRepository,
        ILocationRepository locationRepository,
        IEventRepository eventRepository,
        ClaimsService claimsService)
    {
        _logger = logger;
        _layerRepository = layerRepository;
        _locationRepository = locationRepository;
        _eventRepository = eventRepository;
        _claimsService = claimsService;
    }

    [Function("CreateLayer")]
    public async Task<IActionResult> CreateLayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/layers")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Check authorization via claims (supports sample codes)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            if (!claims.CanModifyEvent)
            {
                return new UnauthorizedObjectResult(new { message = "You do not have permission to modify this event" });
            }

            (CreateLayerRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<CreateLayerRequest>(req);
            if (error != null) return error;

            // Get existing layers to determine display order
            IEnumerable<LayerEntity> existingLayers = await _layerRepository.GetByEventAsync(eventId);
            int maxDisplayOrder = existingLayers.Any() ? existingLayers.Max(l => l.DisplayOrder) : -1;

            LayerEntity layerEntity = new()
            {
                PartitionKey = eventId,
                RowKey = Guid.NewGuid().ToString(),
                EventId = eventId,
                Name = request!.Name,
                DisplayOrder = maxDisplayOrder + 1,
                GpxRouteJson = request.Route != null ? JsonSerializer.Serialize(request.Route) : "[]",
                RouteColor = request.RouteColor ?? string.Empty,
                RouteStyle = request.RouteStyle ?? string.Empty,
                RouteWeight = request.RouteWeight
            };

            await _layerRepository.AddAsync(layerEntity);

            // If layer has a route, recalculate auto-mode checkpoint assignments
            if (request.Route?.Count > 0)
            {
                await GeometryService.RecalculateAutoLayerAssignments(
                    eventId, _locationRepository, _layerRepository);
            }

            int checkpointCount = await CountCheckpointsByLayerAsync(eventId, layerEntity.RowKey);
            LayerResponse response = layerEntity.ToResponse(checkpointCount);

            _logger.LogInformation("Layer created: {LayerId} for event {EventId}", layerEntity.RowKey, eventId);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating layer");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetLayersByEvent")]
    public async Task<IActionResult> GetLayersByEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/layers")] HttpRequest req,
        string eventId)
    {
        try
        {
            IEnumerable<LayerEntity> layerEntities = await _layerRepository.GetByEventAsync(eventId);
            List<LayerEntity> layersList = [.. layerEntities];

            // Migration: If event has a route but no layers, auto-create a layer from the event route
            if (layersList.Count == 0)
            {
                EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);
                if (eventEntity != null && !string.IsNullOrEmpty(eventEntity.GpxRouteJson) && eventEntity.GpxRouteJson != "[]")
                {
                    // Use deterministic ID for migrated layer to prevent duplicate creation
                    // if two requests race to migrate the same event
                    const string migratedLayerId = "migrated-main-route";

                    // Step 1: Create the layer with the route data (critical - must succeed)
                    LayerEntity migratedLayer = new()
                    {
                        PartitionKey = eventId,
                        RowKey = migratedLayerId,
                        EventId = eventId,
                        Name = "Main Route",
                        DisplayOrder = 0,
                        GpxRouteJson = eventEntity.GpxRouteJson,
                        RouteColor = eventEntity.RouteColor,
                        RouteStyle = eventEntity.RouteStyle,
                        RouteWeight = eventEntity.RouteWeight
                    };

                    try
                    {
                        await _layerRepository.AddAsync(migratedLayer);
                        layersList.Add(migratedLayer);
                        _logger.LogInformation("Migrated event route to layer for event {EventId}", eventId);

                        // Step 2: Clear legacy route data from event (cleanup - non-critical)
                        // If this fails, data is duplicated but not lost. Next migration check
                        // will see layers exist and skip, leaving legacy data orphaned but harmless.
                        try
                        {
                            eventEntity.GpxRouteJson = "[]";
                            eventEntity.RouteColor = string.Empty;
                            eventEntity.RouteStyle = string.Empty;
                            eventEntity.RouteWeight = null;
                            await _eventRepository.UpdateAsync(eventEntity);
                            _logger.LogInformation("Cleared legacy route data from event {EventId}", eventId);
                        }
                        catch (Exception cleanupEx)
                        {
                            // Non-critical: layer was created successfully, route data is safe
                            _logger.LogWarning(cleanupEx, "Failed to clear legacy route data from event {EventId}, will retry on next access", eventId);
                        }
                    }
                    catch (RequestFailedException ex) when (ex.Status == 409)
                    {
                        // Layer already exists (another request migrated it) - fetch it instead
                        LayerEntity? existingLayer = await _layerRepository.GetAsync(eventId, migratedLayerId);
                        if (existingLayer != null)
                        {
                            layersList.Add(existingLayer);
                        }
                        _logger.LogInformation("Layer migration already completed by another request for event {EventId}", eventId);
                    }
                }
            }

            // Preload all locations and calculate checkpoint counts in memory
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
            Dictionary<string, int> checkpointCountsByLayer = CalculateCheckpointCountsByLayer([.. allLocations]);

            List<LayerResponse> layers = [.. layersList
                .Select(layer => layer.ToResponse(checkpointCountsByLayer.GetValueOrDefault(layer.RowKey, 0)))];

            return new OkObjectResult(layers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting layers for event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }

    [Function("GetLayer")]
    public async Task<IActionResult> GetLayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/layers/{layerId}")] HttpRequest req,
        string eventId,
        string layerId)
    {
        try
        {
            LayerEntity? layerEntity = await _layerRepository.GetAsync(eventId, layerId);

            if (layerEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Layer not found" });
            }

            int checkpointCount = await CountCheckpointsByLayerAsync(eventId, layerId);
            LayerResponse response = layerEntity.ToResponse(checkpointCount);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting layer {LayerId}", layerId);
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateLayer")]
    public async Task<IActionResult> UpdateLayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "events/{eventId}/layers/{layerId}")] HttpRequest req,
        string eventId,
        string layerId)
    {
        try
        {
            // Check authorization via claims (supports sample codes)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            if (!claims.CanModifyEvent)
            {
                return new UnauthorizedObjectResult(new { message = "You do not have permission to modify this event" });
            }

            (UpdateLayerRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<UpdateLayerRequest>(req);
            if (error != null) return error;
            if (request == null) return new BadRequestObjectResult(new { message = "Request body is required" });

            LayerEntity? layerEntity = await _layerRepository.GetAsync(eventId, layerId);

            if (layerEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Layer not found" });
            }

            // Update properties if provided
            if (request.Name != null) layerEntity.Name = request.Name;
            if (request.DisplayOrder != null) layerEntity.DisplayOrder = request.DisplayOrder.Value;
            if (request.Route != null) layerEntity.GpxRouteJson = JsonSerializer.Serialize(request.Route);
            if (request.RouteColor != null) layerEntity.RouteColor = request.RouteColor;
            if (request.RouteStyle != null) layerEntity.RouteStyle = request.RouteStyle;
            if (request.RouteWeight != null) layerEntity.RouteWeight = request.RouteWeight;

            await _layerRepository.UpdateAsync(layerEntity);

            // If route changed, recalculate auto-mode checkpoint assignments
            if (request.Route != null)
            {
                await GeometryService.RecalculateAutoLayerAssignments(
                    eventId, _locationRepository, _layerRepository);
            }

            int checkpointCount = await CountCheckpointsByLayerAsync(eventId, layerId);
            LayerResponse response = layerEntity.ToResponse(checkpointCount);

            _logger.LogInformation("Layer updated: {LayerId}", layerId);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating layer {LayerId}", layerId);
            return new StatusCodeResult(500);
        }
    }

#pragma warning disable MA0051
    [Function("DeleteLayer")]
    public async Task<IActionResult> DeleteLayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{eventId}/layers/{layerId}")] HttpRequest req,
        string eventId,
        string layerId)
    {
        try
        {
            // Check authorization via claims (supports sample codes)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            if (!claims.CanModifyEvent)
            {
                return new UnauthorizedObjectResult(new { message = "You do not have permission to modify this event" });
            }

            LayerEntity? layerEntity = await _layerRepository.GetAsync(eventId, layerId);

            if (layerEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Layer not found" });
            }

            // Check if any checkpoints exclusively belong to this layer
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
            List<LocationEntity> exclusiveCheckpoints = [];

            foreach (LocationEntity location in allLocations)
            {
                LocationPayload payload = location.GetPayload();
                List<string>? layerIds = payload.LayerIds;
                if (layerIds == null || layerIds.Count == 0) continue;

                // If this checkpoint only belongs to the layer being deleted, it would become orphaned
                if (layerIds.Count == 1 && layerIds[0] == layerId)
                {
                    exclusiveCheckpoints.Add(location);
                }
            }

            if (exclusiveCheckpoints.Count > 0)
            {
                string checkpointNames = string.Join(", ", exclusiveCheckpoints.Take(5).Select(c => c.Name));
                if (exclusiveCheckpoints.Count > 5)
                {
                    checkpointNames += $" and {exclusiveCheckpoints.Count - 5} more";
                }

                return new BadRequestObjectResult(new
                {
                    message = $"Cannot delete layer because {exclusiveCheckpoints.Count} checkpoint(s) exclusively belong to it: {checkpointNames}. " +
                              "Please reassign these checkpoints to other layers first or set them to 'All layers'."
                });
            }

            // Remove this layer from any checkpoints that reference it (but have other layers too)
            List<Task> updateTasks = [];
            foreach (LocationEntity location in allLocations)
            {
                LocationPayload payload = location.GetPayload();
                List<string>? layerIds = payload.LayerIds;
                if (layerIds == null || layerIds.Count == 0) continue;

                if (layerIds.Contains(layerId))
                {
                    layerIds.Remove(layerId);
                    // If no layers left, set to null (all layers)
                    payload.LayerIds = layerIds.Count > 0 ? layerIds : null;
                    location.SetPayload(payload);
                    updateTasks.Add(_locationRepository.UpdateAsync(location));
                }
            }

            if (updateTasks.Count > 0)
            {
                await Task.WhenAll(updateTasks);
                _logger.LogInformation("Removed layer {LayerId} from {CheckpointCount} checkpoints", layerId, updateTasks.Count);
            }

            await _layerRepository.DeleteAsync(eventId, layerId);

            // Recalculate auto-mode checkpoint assignments after layer deletion
            await GeometryService.RecalculateAutoLayerAssignments(
                eventId, _locationRepository, _layerRepository);

            _logger.LogInformation("Layer deleted: {LayerId}", layerId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting layer {LayerId}", layerId);
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    [Function("ReorderLayers")]
    public async Task<IActionResult> ReorderLayers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/layers/reorder")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Check authorization via claims (supports sample codes)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            if (!claims.CanModifyEvent)
            {
                return new UnauthorizedObjectResult(new { message = "You do not have permission to modify this event" });
            }

            (ReorderLayersRequest? request, IActionResult? error) = await FunctionHelpers.TryDeserializeRequestAsync<ReorderLayersRequest>(req);
            if (error != null) return error;
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "Request body with items is required" });
            }

            // Get all layers and update their display orders in parallel
            List<Task> updateTasks = [];
            foreach (ReorderItem item in request.Items)
            {
                LayerEntity? layer = await _layerRepository.GetAsync(eventId, item.Id);
                if (layer != null && layer.DisplayOrder != item.DisplayOrder)
                {
                    layer.DisplayOrder = item.DisplayOrder;
                    updateTasks.Add(_layerRepository.UpdateAsync(layer));
                }
            }

            await Task.WhenAll(updateTasks);

            _logger.LogInformation("Reordered {LayerCount} layers for event {EventId}", updateTasks.Count, eventId);

            // Return updated layer list
            IEnumerable<LayerEntity> layerEntities = await _layerRepository.GetByEventAsync(eventId);
            IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
            Dictionary<string, int> checkpointCountsByLayer = CalculateCheckpointCountsByLayer([.. allLocations]);

            List<LayerResponse> layers = [.. layerEntities
                .Select(layer => layer.ToResponse(checkpointCountsByLayer.GetValueOrDefault(layer.RowKey, 0)))];

            return new OkObjectResult(layers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering layers for event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }

#pragma warning disable MA0051
    [Function("UploadGpxToLayer")]
    public async Task<IActionResult> UploadGpxToLayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/layers/{layerId}/upload-gpx")] HttpRequest req,
        string eventId,
        string layerId)
    {
        try
        {
            // Check authorization via claims (supports sample codes)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            if (!claims.CanModifyEvent)
            {
                return new UnauthorizedObjectResult(new { message = "You do not have permission to modify this event" });
            }

            // Get the uploaded file
            if (req.Form.Files.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "No file uploaded" });
            }

            IFormFile gpxFile = req.Form.Files[0];
            if (!gpxFile.FileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
            {
                return new BadRequestObjectResult(new { message = "File must be a .gpx file" });
            }

            // Parse the GPX file
            List<RoutePoint> route;
            await using (Stream stream = gpxFile.OpenReadStream())
            {
                route = GpxParserService.ParseGpxFile(stream);
            }

            if (route.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "No route points found in GPX file" });
            }

            // Get or create the layer
            LayerEntity? layerEntity = await _layerRepository.GetAsync(eventId, layerId);

            if (layerEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Layer not found" });
            }

            string routeJson = JsonSerializer.Serialize(route);

            // Check if the route is too large
            if (System.Text.Encoding.UTF8.GetByteCount(routeJson) > 60000) // Leave some buffer under 64KB
            {
                return new BadRequestObjectResult(new { message = $"Route is too large ({route.Count} points). The GPX file has too many points even after simplification. Try a simpler route or fewer track points." });
            }

            layerEntity.GpxRouteJson = routeJson;
            await _layerRepository.UpdateAsync(layerEntity);

            // Recalculate auto-mode checkpoint assignments after GPX upload
            await GeometryService.RecalculateAutoLayerAssignments(
                eventId, _locationRepository, _layerRepository);

            int checkpointCount = await CountCheckpointsByLayerAsync(eventId, layerId);
            LayerResponse response = layerEntity.ToResponse(checkpointCount);

            _logger.LogInformation("GPX route uploaded for layer {LayerId} in event {EventId}: {RouteCount} points (simplified)", layerId, eventId, route.Count);

            return new OkObjectResult(new { success = true, message = $"Route uploaded successfully with {route.Count} points (simplified)", layer = response });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid GPX file");
            return new BadRequestObjectResult(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading GPX route to layer");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Counts checkpoints that belong to a specific layer.
    /// Checkpoints with null LayerIdsJson (all layers) are counted for all layers.
    /// </summary>
    private async Task<int> CountCheckpointsByLayerAsync(string eventId, string layerId)
    {
        IEnumerable<LocationEntity> allLocations = await _locationRepository.GetByEventAsync(eventId);
        int count = 0;

        foreach (LocationEntity location in allLocations)
        {
            LocationPayload payload = location.GetPayload();
            List<string>? layerIds = payload.LayerIds;

            // null = all layers, so this checkpoint belongs to this layer
            if (layerIds == null)
            {
                count++;
                continue;
            }

            if (layerIds.Contains(layerId))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Calculates checkpoint counts for each layer from a list of locations.
    /// </summary>
    private static Dictionary<string, int> CalculateCheckpointCountsByLayer(List<LocationEntity> locations)
    {
        Dictionary<string, int> counts = new();
        HashSet<string> allLayerIds = [];

        // First pass: collect all layer IDs from checkpoints
        foreach (LocationEntity location in locations)
        {
            LocationPayload payload = location.GetPayload();
            List<string>? layerIds = payload.LayerIds;
            if (layerIds != null)
            {
                foreach (string layerId in layerIds)
                {
                    allLayerIds.Add(layerId);
                }
            }
        }

        // Second pass: count checkpoints per layer
        foreach (LocationEntity location in locations)
        {
            LocationPayload payload = location.GetPayload();
            List<string>? layerIds = payload.LayerIds;

            if (layerIds == null)
            {
                // null = all layers - increment count for all known layers
                foreach (string layerId in allLayerIds)
                {
                    counts[layerId] = counts.GetValueOrDefault(layerId, 0) + 1;
                }
            }
            else
            {
                foreach (string layerId in layerIds)
                {
                    counts[layerId] = counts.GetValueOrDefault(layerId, 0) + 1;
                }
            }
        }

        return counts;
    }
}
