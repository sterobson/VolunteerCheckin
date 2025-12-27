using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Helpers;

public static class FunctionHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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
}
