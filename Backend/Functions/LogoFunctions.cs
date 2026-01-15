using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Helpers;

namespace VolunteerCheckin.Functions.Functions;

public class LogoFunctions
{
    private readonly ILogger<LogoFunctions> _logger;
    private readonly IEventRepository _eventRepository;
    private readonly IUserEventMappingRepository _userEventMappingRepository;
    private readonly BlobStorageService _blobStorageService;

    private static readonly HashSet<string> _allowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/svg+xml",
        "image/gif",
        "image/webp"
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    public LogoFunctions(
        ILogger<LogoFunctions> logger,
        IEventRepository eventRepository,
        IUserEventMappingRepository userEventMappingRepository,
        BlobStorageService blobStorageService)
    {
        _logger = logger;
        _eventRepository = eventRepository;
        _userEventMappingRepository = userEventMappingRepository;
        _blobStorageService = blobStorageService;
    }

    private async Task<bool> IsUserAuthorizedForEvent(string eventId, string userEmail)
    {
        UserEventMappingEntity? mapping = await _userEventMappingRepository.GetAsync(eventId, userEmail);
        return mapping != null;
    }

    /// <summary>
    /// Get the logo image for an event.
    /// GET /events/{eventId}/logo
    /// </summary>
    [Function("GetEventLogo")]
    public async Task<IActionResult> GetEventLogo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/logo")] HttpRequest req,
        string eventId)
    {
        try
        {
            (byte[]? data, string? contentType) = await _blobStorageService.GetLogoAsync(eventId);

            if (data == null)
            {
                return new NotFoundResult();
            }

            return new FileContentResult(data, contentType ?? "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logo for event {EventId}", eventId);
            return new NotFoundResult();
        }
    }

    /// <summary>
    /// Upload a logo image for an event.
    /// POST /events/{eventId}/logo
    /// </summary>
    [Function("UploadEventLogo")]
#pragma warning disable MA0051 // File upload handling with validation and processing
    public async Task<IActionResult> UploadEventLogo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/logo")] HttpRequest req,
        string eventId)
    {
        try
        {
            (string? adminEmail, IActionResult? headerError) = FunctionHelpers.GetAdminEmailFromHeader(req);
            if (headerError != null) return headerError;

            if (!await IsUserAuthorizedForEvent(eventId, adminEmail!))
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to modify this event" });
            }

            // Verify event exists
            EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);
            if (eventEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Event not found" });
            }

            // Check content type
            string? contentType = req.ContentType?.Split(';')[0].Trim();
            if (string.IsNullOrEmpty(contentType) || !_allowedContentTypes.Contains(contentType))
            {
                return new BadRequestObjectResult(new
                {
                    message = "Invalid file type. Allowed types: PNG, JPEG, SVG, GIF, WebP"
                });
            }

            // Check file size
            if (req.ContentLength > MaxFileSizeBytes)
            {
                return new BadRequestObjectResult(new
                {
                    message = $"File too large. Maximum size is {MaxFileSizeBytes / (1024 * 1024)}MB"
                });
            }

            // Upload the logo and get a cache-busting version string
            string version = await _blobStorageService.UploadLogoAsync(eventId, req.Body, contentType);

            // Construct the API URL for serving the logo
            string logoUrl = $"/api/events/{eventId}/logo?v={version}";

            // Update the event with the logo URL
            eventEntity.BrandingLogoUrl = logoUrl;
            await _eventRepository.UpdateAsync(eventEntity);

            _logger.LogInformation("Logo uploaded for event {EventId}", eventId);

            return new OkObjectResult(new
            {
                logoUrl,
                message = "Logo uploaded successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading logo for event {EventId}", eventId);
            return new ObjectResult(new { message = $"Failed to upload logo: {ex.Message}" })
            {
                StatusCode = 500
            };
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Delete the logo for an event.
    /// DELETE /events/{eventId}/logo
    /// </summary>
    [Function("DeleteEventLogo")]
    public async Task<IActionResult> DeleteEventLogo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{eventId}/logo")] HttpRequest req,
        string eventId)
    {
        try
        {
            (string? adminEmail, IActionResult? headerError) = FunctionHelpers.GetAdminEmailFromHeader(req);
            if (headerError != null) return headerError;

            if (!await IsUserAuthorizedForEvent(eventId, adminEmail!))
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to modify this event" });
            }

            // Verify event exists
            EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);
            if (eventEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Event not found" });
            }

            // Delete the logo from blob storage
            await _blobStorageService.DeleteLogoAsync(eventId);

            // Clear the logo URL from the event
            eventEntity.BrandingLogoUrl = string.Empty;
            await _eventRepository.UpdateAsync(eventEntity);

            _logger.LogInformation("Logo deleted for event {EventId}", eventId);

            return new OkObjectResult(new { message = "Logo deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting logo for event {EventId}", eventId);
            return new ObjectResult(new { message = $"Failed to delete logo: {ex.Message}" })
            {
                StatusCode = 500
            };
        }
    }
}
