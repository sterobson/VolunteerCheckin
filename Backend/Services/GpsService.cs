namespace VolunteerCheckin.Functions.Services;

public class GpsService
{
    private const double EarthRadiusMeters = 6371000;

    public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Haversine formula to calculate distance between two GPS coordinates
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    public static bool IsWithinRadius(double lat1, double lon1, double lat2, double lon2, double radiusMeters)
    {
        var distance = CalculateDistance(lat1, lon1, lat2, lon2);
        return distance <= radiusMeters;
    }
}
