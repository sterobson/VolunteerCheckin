using System.Text.RegularExpressions;

namespace VolunteerCheckin.Functions.Helpers;

public static partial class Validators
{
    // Email validation regex
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    // What3Words validation regex
    [GeneratedRegex(@"^[a-z]{1,20}[./][a-z]{1,20}[./][a-z]{1,20}$")]
    private static partial Regex What3WordsRegex();

    /// <summary>
    /// Validates GPS latitude coordinate.
    /// Valid range: -90 to 90 degrees.
    /// </summary>
    public static bool IsValidLatitude(double latitude)
    {
        return latitude >= -90 && latitude <= 90;
    }

    /// <summary>
    /// Validates GPS longitude coordinate.
    /// Valid range: -180 to 180 degrees.
    /// </summary>
    public static bool IsValidLongitude(double longitude)
    {
        return longitude >= -180 && longitude <= 180;
    }

    /// <summary>
    /// Validates both latitude and longitude coordinates.
    /// </summary>
    public static bool IsValidCoordinates(double latitude, double longitude)
    {
        return IsValidLatitude(latitude) && IsValidLongitude(longitude);
    }

    /// <summary>
    /// Validates email address format.
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return EmailRegex().IsMatch(email);
    }

    /// <summary>
    /// Validates What3Words format.
    /// Format: word.word.word or word/word/word where each word is lowercase letters (1-20 characters).
    /// </summary>
    public static bool IsValidWhat3Words(string? what3Words)
    {
        if (string.IsNullOrWhiteSpace(what3Words))
        {
            return true; // Optional field
        }

        if (!What3WordsRegex().IsMatch(what3Words))
        {
            return false;
        }

        // Ensure consistent separator (all dots or all slashes)
        bool hasDots = what3Words.Contains('.');
        bool hasSlashes = what3Words.Contains('/');
        return hasDots != hasSlashes; // XOR - can't have both
    }

    /// <summary>
    /// Validates that a value is positive (greater than 0).
    /// </summary>
    public static bool IsPositive(int value)
    {
        return value > 0;
    }

    /// <summary>
    /// Validates that a value is non-negative (greater than or equal to 0).
    /// </summary>
    public static bool IsNonNegative(int value)
    {
        return value >= 0;
    }
}
