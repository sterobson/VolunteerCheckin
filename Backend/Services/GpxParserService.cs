using System.Xml.Linq;
using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Services;

public class GpxParserService
{
    public List<RoutePoint> ParseGpxFile(Stream fileStream)
    {
        List<RoutePoint> routePoints = [];

        if (fileStream.Length == 0)
        {
            return routePoints;
        }

        try
        {
            XDocument gpxDoc = XDocument.Load(fileStream);

            // GPX files use the GPX namespace
            XNamespace gpxNamespace = "http://www.topografix.com/GPX/1/1";

            // Handle files without namespace (some GPX files don't declare it)
            if (gpxDoc.Root?.Name.NamespaceName == string.Empty)
            {
                gpxNamespace = XNamespace.None;
            }

            // Extract track points from tracks
            IEnumerable<XElement> trackPoints = gpxDoc.Descendants(gpxNamespace + "trkpt");

            foreach (XElement trkpt in trackPoints)
            {
                string? latStr = trkpt.Attribute("lat")?.Value;
                string? lonStr = trkpt.Attribute("lon")?.Value;

                if (latStr != null && lonStr != null &&
                    double.TryParse(latStr, out double lat) &&
                    double.TryParse(lonStr, out double lon))
                {
                    routePoints.Add(new RoutePoint(lat, lon));
                }
            }

            // If no track points, try extracting route points
            if (routePoints.Count == 0)
            {
                IEnumerable<XElement> routePts = gpxDoc.Descendants(gpxNamespace + "rtept");

                foreach (XElement rtept in routePts)
                {
                    string? latStr = rtept.Attribute("lat")?.Value;
                    string? lonStr = rtept.Attribute("lon")?.Value;

                    if (latStr != null && lonStr != null &&
                        double.TryParse(latStr, out double lat) &&
                        double.TryParse(lonStr, out double lon))
                    {
                        routePoints.Add(new RoutePoint(lat, lon));
                    }
                }
            }

            // If still no points, try waypoints
            if (routePoints.Count == 0)
            {
                IEnumerable<XElement> waypoints = gpxDoc.Descendants(gpxNamespace + "wpt");

                foreach (XElement wpt in waypoints)
                {
                    string? latStr = wpt.Attribute("lat")?.Value;
                    string? lonStr = wpt.Attribute("lon")?.Value;

                    if (latStr != null && lonStr != null &&
                        double.TryParse(latStr, out double lat) &&
                        double.TryParse(lonStr, out double lon))
                    {
                        routePoints.Add(new RoutePoint(lat, lon));
                    }
                }
            }

            // Simplify the route to reduce the number of points
            if (routePoints.Count > 2)
            {
                int originalCount = routePoints.Count;
                List<RoutePoint> originalPoints = [.. routePoints];

                // Start with a small epsilon and increase if needed to stay under size limit
                double epsilon = 0.0001;
                routePoints = SimplifyRoute(originalPoints, epsilon);

                // If still too many points (rough estimate: 1500+ points might exceed 64KB)
                while (routePoints.Count > 1500 && epsilon < 0.01)
                {
                    epsilon *= 2; // Double the tolerance
                    routePoints = SimplifyRoute(originalPoints, epsilon);
                }

                Console.WriteLine($"GPX simplification: {originalCount} points → {routePoints.Count} points ({(int)((1 - (double)routePoints.Count / originalCount) * 100)}% reduction, epsilon={epsilon})");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse GPX file: {ex.Message}", ex);
        }

        return routePoints;
    }

    /// <summary>
    /// Simplifies a route using the Ramer-Douglas-Peucker algorithm
    /// </summary>
    private static List<RoutePoint> SimplifyRoute(List<RoutePoint> points, double epsilon = 0.0001)
    {
        if (points.Count < 3)
            return points;

        // Find the point with the maximum distance from the line between first and last
        double maxDistance = 0;
        int maxIndex = 0;

        RoutePoint start = points[0];
        RoutePoint end = points[points.Count - 1];

        for (int i = 1; i < points.Count - 1; i++)
        {
            double distance = PerpendicularDistance(points[i], start, end);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }

        // If max distance is greater than epsilon, recursively simplify
        if (maxDistance > epsilon)
        {
            // Recursive call on both sides
            List<RoutePoint> leftPoints = points.GetRange(0, maxIndex + 1);
            List<RoutePoint> rightPoints = points.GetRange(maxIndex, points.Count - maxIndex);

            List<RoutePoint> leftSimplified = SimplifyRoute(leftPoints, epsilon);
            List<RoutePoint> rightSimplified = SimplifyRoute(rightPoints, epsilon);

            // Combine results (remove duplicate middle point)
            leftSimplified.RemoveAt(leftSimplified.Count - 1);
            leftSimplified.AddRange(rightSimplified);

            return leftSimplified;
        }
        else
        {
            // All points between start and end can be removed
            return [start, end];
        }
    }

    /// <summary>
    /// Calculates the perpendicular distance from a point to a line segment
    /// </summary>
    private static double PerpendicularDistance(RoutePoint point, RoutePoint lineStart, RoutePoint lineEnd)
    {
        double dx = lineEnd.Lng - lineStart.Lng;
        double dy = lineEnd.Lat - lineStart.Lat;

        // Normalize
        double mag = Math.Sqrt(dx * dx + dy * dy);
        if (mag > 0.0)
        {
            dx /= mag;
            dy /= mag;
        }

        double pvx = point.Lng - lineStart.Lng;
        double pvy = point.Lat - lineStart.Lat;

        // Get dot product (project point onto line)
        double pvdot = dx * pvx + dy * pvy;

        // Scale line direction vector
        double dsx = pvdot * dx;
        double dsy = pvdot * dy;

        // Subtract from pv to get perpendicular vector
        double ax = pvx - dsx;
        double ay = pvy - dsy;

        return Math.Sqrt(ax * ax + ay * ay);
    }
}
