using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace VolunteerCheckin.Functions.Services;

public class BlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private const string LogosContainer = "event-logos";
    private bool _containerInitialized;

    public BlobStorageService(string connectionString)
    {
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    private async Task<BlobContainerClient> GetOrCreateContainerAsync()
    {
        BlobContainerClient container = _blobServiceClient.GetBlobContainerClient(LogosContainer);

        if (!_containerInitialized)
        {
            await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
            _containerInitialized = true;
        }

        return container;
    }

    /// <summary>
    /// Uploads a logo image for an event and returns the public URL.
    /// </summary>
    /// <param name="eventId">The event ID</param>
    /// <param name="imageData">The image data as a stream</param>
    /// <param name="contentType">The content type (e.g., image/png)</param>
    /// <returns>The public URL of the uploaded logo</returns>
    public async Task<string> UploadLogoAsync(string eventId, Stream imageData, string contentType)
    {
        BlobContainerClient container = await GetOrCreateContainerAsync();

        // Use eventId as blob name to ensure one logo per event
        string blobName = $"{eventId}/logo{GetExtensionFromContentType(contentType)}";
        BlobClient blob = container.GetBlobClient(blobName);

        // Delete existing blob if it exists (to handle format changes)
        await DeleteExistingLogosAsync(container, eventId);

        await blob.UploadAsync(imageData, new BlobHttpHeaders { ContentType = contentType });

        return blob.Uri.ToString();
    }

    /// <summary>
    /// Deletes the logo for an event.
    /// </summary>
    public async Task DeleteLogoAsync(string eventId)
    {
        BlobContainerClient container = await GetOrCreateContainerAsync();
        await DeleteExistingLogosAsync(container, eventId);
    }

    private static async Task DeleteExistingLogosAsync(BlobContainerClient container, string eventId)
    {
        // Delete any existing logos for this event (handles format changes)
        await foreach (BlobItem blob in container.GetBlobsAsync(prefix: $"{eventId}/"))
        {
            await container.DeleteBlobIfExistsAsync(blob.Name);
        }
    }

    private static string GetExtensionFromContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/svg+xml" => ".svg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => ".png"
        };
    }
}
