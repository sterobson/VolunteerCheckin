using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Helpers;

public static class GeometryHelper
{
    /// <summary>
    /// Determines if a point is inside a polygon using the ray casting algorithm
    /// </summary>
    /// <param name="point">The point to test (lat, lng)</param>
    /// <param name="polygon">The polygon vertices</param>
    /// <returns>True if the point is inside the polygon</returns>
    public static bool IsPointInPolygon(RoutePoint point, List<RoutePoint> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return false;
        }

        bool inside = false;
        int j = polygon.Count - 1;

        for (int i = 0; i < polygon.Count; j = i++)
        {
            RoutePoint pi = polygon[i];
            RoutePoint pj = polygon[j];

            if ((pi.Lng > point.Lng) != (pj.Lng > point.Lng) &&
                point.Lat < (pj.Lat - pi.Lat) * (point.Lng - pi.Lng) / (pj.Lng - pi.Lng) + pi.Lat)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// Calculate which areas a checkpoint belongs to based on its location
    /// </summary>
    /// <param name="latitude">Checkpoint latitude</param>
    /// <param name="longitude">Checkpoint longitude</param>
    /// <param name="areas">All areas for the event</param>
    /// <param name="defaultAreaId">The default area ID (for checkpoints not in any polygon)</param>
    /// <returns>List of area IDs the checkpoint belongs to</returns>
    public static List<string> CalculateCheckpointAreas(
        double latitude,
        double longitude,
        IEnumerable<AreaEntity> areas,
        string defaultAreaId)
    {
        List<string> areaIds = new();
        RoutePoint checkpointPoint = new(latitude, longitude);

        foreach (AreaEntity area in areas)
        {
            // Skip the default area - it's handled separately
            if (area.IsDefault)
            {
                continue;
            }

            // Parse the polygon
            List<RoutePoint> polygon = System.Text.Json.JsonSerializer.Deserialize<List<RoutePoint>>(area.PolygonJson) ?? [];

            if (polygon.Count > 0 && IsPointInPolygon(checkpointPoint, polygon))
            {
                areaIds.Add(area.RowKey);
            }
        }

        // If checkpoint is not in any polygon, add it to the default area
        if (areaIds.Count == 0)
        {
            areaIds.Add(defaultAreaId);
        }

        return areaIds;
    }
}
