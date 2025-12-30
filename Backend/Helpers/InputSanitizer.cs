using System.Text.RegularExpressions;

namespace VolunteerCheckin.Functions.Helpers;

public static partial class InputSanitizer
{
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 2000;
    private const int MaxNotesLength = 5000;
    private const int MaxEmailLength = 254; // RFC 5321
    private const int MaxPhoneLength = 30;
    private const int MaxWhat3WordsLength = 50;

    /// <summary>
    /// Sanitizes a string by removing potentially harmful HTML/script content
    /// and trimming to maximum length
    /// </summary>
    public static string SanitizeString(string? input, int maxLength = 1000)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Trim whitespace
        string sanitized = input.Trim();

        // Remove HTML tags
        sanitized = HtmlTagRegex().Replace(sanitized, string.Empty);

        // Remove script content (belt and suspenders)
        sanitized = ScriptTagRegex().Replace(sanitized, string.Empty);

        // Truncate to max length
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized.Substring(0, maxLength);
        }

        return sanitized;
    }

    /// <summary>
    /// Sanitizes a name field (max 200 characters)
    /// </summary>
    public static string SanitizeName(string? name)
    {
        return SanitizeString(name, MaxNameLength);
    }

    /// <summary>
    /// Sanitizes a description field (max 2000 characters)
    /// </summary>
    public static string SanitizeDescription(string? description)
    {
        return SanitizeString(description, MaxDescriptionLength);
    }

    /// <summary>
    /// Sanitizes a notes field (max 5000 characters)
    /// </summary>
    public static string SanitizeNotes(string? notes)
    {
        return SanitizeString(notes, MaxNotesLength);
    }

    /// <summary>
    /// Sanitizes an email field (max 254 characters) and validates format
    /// </summary>
    public static string? SanitizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        string sanitized = email.Trim().ToLowerInvariant();

        if (sanitized.Length > MaxEmailLength)
        {
            return null;
        }

        return Validators.IsValidEmail(sanitized) ? sanitized : null;
    }

    /// <summary>
    /// Sanitizes a phone number field (max 30 characters)
    /// </summary>
    public static string SanitizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        // Keep only digits, spaces, dashes, parentheses, and plus sign
        string sanitized = PhoneCharactersRegex().Replace(phone.Trim(), string.Empty);

        if (sanitized.Length > MaxPhoneLength)
        {
            sanitized = sanitized.Substring(0, MaxPhoneLength);
        }

        return sanitized;
    }

    /// <summary>
    /// Sanitizes What3Words field (max 50 characters)
    /// </summary>
    public static string? SanitizeWhat3Words(string? what3Words)
    {
        if (string.IsNullOrWhiteSpace(what3Words))
        {
            return null;
        }

        string sanitized = what3Words.Trim().ToLowerInvariant();

        if (sanitized.Length > MaxWhat3WordsLength)
        {
            return null;
        }

        return Validators.IsValidWhat3Words(sanitized) ? sanitized : null;
    }

    // Regex patterns using source generators for performance
    [GeneratedRegex(@"<[^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"<script[^>]*>.*?</script>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptTagRegex();

    [GeneratedRegex(@"[^0-9\s\-\(\)\+]")]
    private static partial Regex PhoneCharactersRegex();
}
