namespace VolunteerCheckin.Functions.Services;

public class GpsService
{
    private const double EarthRadiusMeters = 6371000;

    /// <summary>
    /// Calculate the distance between two GPS coordinates using the Haversine formula
    /// </summary>
    public virtual double CalculateDistance(double latitude1Degrees, double longitude1Degrees,
                                            double latitude2Degrees, double longitude2Degrees)
    {
        // Haversine formula to calculate distance between two GPS coordinates
        double δLatitude = DegreesToRadians(latitude2Degrees - latitude1Degrees);
        double δLongitude = DegreesToRadians(longitude2Degrees - longitude1Degrees);

        double a = Math.Sin(δLatitude / 2) * Math.Sin(δLatitude / 2) +
                Math.Cos(DegreesToRadians(latitude1Degrees)) * Math.Cos(DegreesToRadians(latitude2Degrees)) *
                Math.Sin(δLongitude / 2) * Math.Sin(δLongitude / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    /// <summary>
    /// Check if two GPS coordinates are within a specified radius
    /// </summary>
    public virtual bool IsWithinRadius(double latitude1Degrees, double longitude1Degrees,
                                       double latitude2Degrees, double longitude2Degrees,
                                       double radiusMeters)
    {
        double distance = CalculateDistance(latitude1Degrees, longitude1Degrees, latitude2Degrees, longitude2Degrees);
        return distance <= radiusMeters;
    }
}
