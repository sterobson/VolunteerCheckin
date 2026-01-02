using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Helpers;

public static class FunctionHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Extracts the session token from either the session cookie or Authorization header.
    /// Supports both cookie-based auth (browser) and Bearer token auth (API clients).
    /// </summary>
    public static string? GetSessionToken(HttpRequest req)
    {
        // Try cookie first (browser sessions)
        string? sessionToken = req.Cookies["session"];

        // Fall back to Authorization header (API clients)
        if (string.IsNullOrEmpty(sessionToken))
        {
            string? authHeader = req.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                sessionToken = authHeader.Substring(7); // Length of "Bearer "
            }
        }

        return string.IsNullOrWhiteSpace(sessionToken) ? null : sessionToken;
    }

    /// <summary>
    /// Deserializes the request body to the specified type.
    /// Returns the deserialized object if successful, otherwise returns a BadRequest result.
    /// </summary>
    public static async Task<(T? Data, IActionResult? Error)> TryDeserializeRequestAsync<T>(HttpRequest req) where T : class
    {
        string body = await new StreamReader(req.Body).ReadToEndAsync();
        T? request = JsonSerializer.Deserialize<T>(body, JsonOptions);

        if (request == null)
        {
            return (null, new BadRequestObjectResult(new { message = "Invalid request" }));
        }

        return (request, null);
    }

    /// <summary>
    /// Converts a datetime from a specific timezone to UTC.
    /// If the timezone is invalid, falls back to treating the datetime as UTC.
    /// </summary>
    public static DateTime ConvertToUtc(DateTime dateTime, string timeZoneId)
    {
        try
        {
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            DateTime unspecifiedDateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecifiedDateTime, timeZone);
        }
        catch
        {
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Extracts and validates the admin email from the request header.
    /// Returns the email if present, otherwise returns an error result.
    /// </summary>
    public static (string? Email, IActionResult? Error) GetAdminEmailFromHeader(HttpRequest req)
    {
        string? adminEmail = req.Headers[Constants.AdminEmailHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return (null, new BadRequestObjectResult(new { message = "Admin email header is required" }));
        }

        return (adminEmail, null);
    }

    /// <summary>
    /// Extracts pagination parameters from query string.
    /// Returns page number (1-based) and page size (max 100).
    /// </summary>
    public static (int Page, int PageSize) GetPaginationParams(HttpRequest req)
    {
        int page = 1;
        int pageSize = 20;

        if (int.TryParse(req.Query["page"], out int parsedPage) && parsedPage > 0)
        {
            page = parsedPage;
        }

        if (int.TryParse(req.Query["pageSize"], out int parsedPageSize) && parsedPageSize > 0)
        {
            pageSize = Math.Min(parsedPageSize, 100); // Max 100 items per page
        }

        return (page, pageSize);
    }

    /// <summary>
    /// Creates a paginated response from a collection.
    /// </summary>
    public static PaginatedResponse<T> CreatePaginatedResponse<T>(IEnumerable<T> allItems, int page, int pageSize)
    {
        List<T> allItemsList = allItems.ToList();
        int totalCount = allItemsList.Count;
        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        List<T> paginatedItems = allItemsList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PaginatedResponse<T>(
            paginatedItems,
            page,
            pageSize,
            totalCount,
            totalPages
        );
    }

    /// <summary>
    /// Creates a standardized error response.
    /// Set includeDetails to true in development environments to include exception details.
    /// </summary>
    public static ObjectResult CreateErrorResponse(Exception ex, string message, bool includeDetails = false)
    {
        ErrorResponse errorResponse = new(
            message,
            includeDetails ? ex.Message : null,
            includeDetails ? ex.GetType().Name : null
        );

        return new ObjectResult(errorResponse)
        {
            StatusCode = 500
        };
    }

    /// <summary>
    /// Executes an async operation with retry logic using exponential backoff.
    /// Useful for operations that may transiently fail (database updates, network calls).
    /// </summary>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="baseDelayMs">Base delay in milliseconds between retries (default: 100)</param>
    /// <returns>True if operation succeeded, false if all retries failed</returns>
    public static async Task<bool> ExecuteWithRetryAsync(Func<Task> operation, int maxRetries = 3, int baseDelayMs = 100)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await operation();
                return true;
            }
            catch (Exception) when (attempt < maxRetries)
            {
                // Exponential backoff: 100ms, 200ms, 400ms...
                int delay = baseDelayMs * (1 << attempt);
                await Task.Delay(delay);
            }
        }
        return false;
    }

    /// <summary>
    /// Executes an async operation with retry logic, returning the result or throwing on failure.
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3, int baseDelayMs = 100)
    {
        Exception? lastException = null;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastException = ex;
                int delay = baseDelayMs * (1 << attempt);
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }
        throw lastException ?? new InvalidOperationException("Operation failed after retries");
    }
}
