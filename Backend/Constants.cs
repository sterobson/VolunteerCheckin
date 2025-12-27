namespace VolunteerCheckin.Functions;

public static class Constants
{
    // Partition Keys
    public const string EventPartitionKey = "EVENT";
    public const string AdminPartitionKey = "ADMIN";

    // Roles
    public const string AdminRole = "Admin";

    // GPS Settings
    public const double CheckInRadiusMeters = 100;

    // Headers
    public const string AdminEmailHeader = "X-Admin-Email";
}
