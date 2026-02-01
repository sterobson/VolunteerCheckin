using System.Text.Json;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Services;

/// <summary>
/// Service for geometry-related calculations, particularly for route proximity detection.
/// </summary>
public static class GeometryService
{
    private const double DefaultProximityMetres = 25.0;
    private const double EarthRadiusMetres = 6371000.0;

    /// <summary>
    /// Calculate distance between two points using Haversine formula.
    /// </summary>
    public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double phi1 = lat1 * Math.PI / 180;
        double phi2 = lat2 * Math.PI / 180;
        double deltaPhi = (lat2 - lat1) * Math.PI / 180;
        double deltaLambda = (lon2 - lon1) * Math.PI / 180;

        double a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                   Math.Cos(phi1) * Math.Cos(phi2) *
                   Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMetres * c;
    }

    /// <summary>
    /// Calculate minimum distance from a point to a line segment.
    /// </summary>
    public static double DistanceToLineSegment(
        double pointLat, double pointLon,
        double segStartLat, double segStartLon,
        double segEndLat, double segEndLon)
    {
        // Vector from segment start to point
        double dx = pointLon - segStartLon;
        double dy = pointLat - segStartLat;

        // Vector from segment start to segment end
        double sx = segEndLon - segStartLon;
        double sy = segEndLat - segStartLat;

        // Segment length squared
        double segLengthSq = sx * sx + sy * sy;

        if (segLengthSq == 0)
        {
            // Segment is a point
            return CalculateDistance(pointLat, pointLon, segStartLat, segStartLon);
        }

        // Project point onto segment line, clamped to [0, 1]
        double t = Math.Max(0, Math.Min(1, (dx * sx + dy * sy) / segLengthSq));

        // Closest point on segment
        double closestLon = segStartLon + t * sx;
        double closestLat = segStartLat + t * sy;

        return CalculateDistance(pointLat, pointLon, closestLat, closestLon);
    }

    /// <summary>
    /// Calculate minimum distance from a point to a route (polyline).
    /// </summary>
    public static double DistanceToRoute(double lat, double lng, List<RoutePoint> route)
    {
        if (route == null || route.Count == 0)
        {
            return double.MaxValue;
        }

        if (route.Count == 1)
        {
            return CalculateDistance(lat, lng, route[0].Lat, route[0].Lng);
        }

        double minDistance = double.MaxValue;

        for (int i = 0; i < route.Count - 1; i++)
        {
            double dist = DistanceToLineSegment(
                lat, lng,
                route[i].Lat, route[i].Lng,
                route[i + 1].Lat, route[i + 1].Lng);

            if (dist < minDistance)
            {
                minDistance = dist;
            }
        }

        return minDistance;
    }

    /// <summary>
    /// Find all layers with routes within the specified distance of a point.
    /// </summary>
    public static List<string> FindLayersWithinDistance(
        double lat, double lng,
        IEnumerable<LayerEntity> layers,
        double distanceMetres = DefaultProximityMetres)
    {
        List<string> nearbyLayerIds = [];

        foreach (LayerEntity layer in layers)
        {
            if (string.IsNullOrEmpty(layer.GpxRouteJson) || layer.GpxRouteJson == "[]")
            {
                continue;
            }

            List<RoutePoint>? route = JsonSerializer.Deserialize<List<RoutePoint>>(layer.GpxRouteJson);
            if (route == null || route.Count == 0)
            {
                continue;
            }

            double distance = DistanceToRoute(lat, lng, route);
            if (distance <= distanceMetres)
            {
                nearbyLayerIds.Add(layer.RowKey);
            }
        }

        return nearbyLayerIds;
    }

    /// <summary>
    /// Recalculate layer assignments for all auto-mode checkpoints in an event.
    /// </summary>
    public static async Task RecalculateAutoLayerAssignments(
        string eventId,
        ILocationRepository locationRepository,
        ILayerRepository layerRepository,
        double distanceMetres = DefaultProximityMetres)
    {
        // Get all layers and locations for this event
        IEnumerable<LayerEntity> layers = await layerRepository.GetByEventAsync(eventId);
        IEnumerable<LocationEntity> locations = await locationRepository.GetByEventAsync(eventId);

        List<LayerEntity> layersList = [.. layers];
        List<Task> updateTasks = [];

        foreach (LocationEntity location in locations)
        {
            // Get payload for checking/updating layer settings
            LocationPayload payload = location.GetPayload();

            // Only recalculate for auto-mode checkpoints
            if (payload.LayerAssignmentMode != "auto")
            {
                continue;
            }

            // Calculate new layer IDs
            List<string> nearbyLayerIds = FindLayersWithinDistance(
                location.Latitude, location.Longitude, layersList, distanceMetres);

            // Only update if changed
            List<string> currentLayerIds = payload.LayerIds ?? [];
            if (!nearbyLayerIds.SequenceEqual(currentLayerIds))
            {
                payload.LayerIds = nearbyLayerIds;
                location.SetPayload(payload);
                updateTasks.Add(locationRepository.UpdateAsync(location));
            }
        }

        if (updateTasks.Count > 0)
        {
            await Task.WhenAll(updateTasks);
        }
    }
}
