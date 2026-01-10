using SkiaSharp;

namespace VolunteerCheckin.Functions.Helpers;

/// <summary>
/// Provides image resizing functionality to reduce file sizes.
/// </summary>
public static class ImageResizer
{
    private const int DefaultMaxSizeBytes = 500 * 1024; // 500 KB

    /// <summary>
    /// Content types that can be resized (raster images only).
    /// SVG (vector) and GIF (animated) cannot be resized.
    /// </summary>
    public static bool CanResize(string contentType)
    {
        string lower = contentType.ToLowerInvariant();
        return lower is "image/png" or "image/jpeg" or "image/jpg" or "image/webp";
    }

    /// <summary>
    /// Resizes an image if it exceeds the maximum size.
    /// Returns the original data if already small enough or if resizing would increase size.
    /// </summary>
    /// <param name="imageData">The original image data</param>
    /// <param name="contentType">The content type of the image</param>
    /// <param name="maxSizeBytes">Maximum size in bytes (default 500KB)</param>
    /// <returns>Tuple of (resized image bytes, content type)</returns>
    public static (byte[] bytes, string contentType) ResizeIfNeeded(byte[] imageData, string contentType, int maxSizeBytes = DefaultMaxSizeBytes)
    {
        // If already under the limit, return the original
        if (imageData.Length <= maxSizeBytes)
        {
            return (imageData, contentType);
        }

        // Decode the image
        SKBitmap? bitmap;
        try
        {
            bitmap = SKBitmap.Decode(imageData);
        }
        catch
        {
            // Can't decode, return original
            return (imageData, contentType);
        }

        if (bitmap == null)
        {
            // Can't decode, return original
            return (imageData, contentType);
        }

        using SKBitmap bitmapToDispose = bitmap;

        // Try to resize to meet the size limit
        return TryResizeToLimit(bitmap, imageData, contentType, maxSizeBytes);
    }

    /// <summary>
    /// Attempts to resize the bitmap to meet the size limit.
    /// Returns the smallest result that's still smaller than the original.
    /// </summary>
    private static (byte[] bytes, string contentType) TryResizeToLimit(
        SKBitmap bitmap,
        byte[] originalBytes,
        string contentType,
        int maxSizeBytes)
    {
        // Calculate initial scale factor based on target size
        double scaleFactor = Math.Sqrt((double)maxSizeBytes / originalBytes.Length);
        byte[] bestResult = originalBytes;
        string resultContentType = contentType;

        // Try progressively smaller sizes until we're under the limit
        for (int attempt = 0; attempt < 5; attempt++)
        {
            int newWidth = (int)(bitmap.Width * scaleFactor);
            int newHeight = (int)(bitmap.Height * scaleFactor);

            // Don't scale up or make too small
            if (newWidth >= bitmap.Width || newHeight >= bitmap.Height || newWidth < 10 || newHeight < 10)
            {
                break;
            }

            byte[]? resizedBytes = EncodeResizedImage(bitmap, newWidth, newHeight);
            if (resizedBytes == null)
            {
                break;
            }

            // Only use if smaller than current best
            if (resizedBytes.Length < bestResult.Length)
            {
                bestResult = resizedBytes;
                resultContentType = "image/jpeg";

                // If under the limit, we're done
                if (resizedBytes.Length <= maxSizeBytes)
                {
                    break;
                }
            }

            // Reduce scale for next attempt
            scaleFactor *= 0.7;
        }

        return (bestResult, resultContentType);
    }

    /// <summary>
    /// Resizes a bitmap to the specified dimensions and encodes as JPEG.
    /// </summary>
    private static byte[]? EncodeResizedImage(SKBitmap bitmap, int width, int height)
    {
        using SKBitmap? resized = bitmap.Resize(new SKImageInfo(width, height), SKSamplingOptions.Default);
        if (resized == null)
        {
            return null;
        }

        using SKImage image = SKImage.FromBitmap(resized);
        using SKData? encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return encoded?.ToArray();
    }
}
