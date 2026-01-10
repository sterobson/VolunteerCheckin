using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using SkiaSharp;
using VolunteerCheckin.Functions.Helpers;

namespace VolunteerCheckin.Functions.Tests;

[TestClass]
public class ImageResizerTests
{
    #region CanResize Tests

    [TestMethod]
    public void CanResize_Jpeg_ReturnsTrue()
    {
        ImageResizer.CanResize("image/jpeg").ShouldBeTrue();
        ImageResizer.CanResize("image/jpg").ShouldBeTrue();
        ImageResizer.CanResize("IMAGE/JPEG").ShouldBeTrue();
    }

    [TestMethod]
    public void CanResize_Png_ReturnsTrue()
    {
        ImageResizer.CanResize("image/png").ShouldBeTrue();
        ImageResizer.CanResize("IMAGE/PNG").ShouldBeTrue();
    }

    [TestMethod]
    public void CanResize_WebP_ReturnsTrue()
    {
        ImageResizer.CanResize("image/webp").ShouldBeTrue();
    }

    [TestMethod]
    public void CanResize_Svg_ReturnsFalse()
    {
        ImageResizer.CanResize("image/svg+xml").ShouldBeFalse();
    }

    [TestMethod]
    public void CanResize_Gif_ReturnsFalse()
    {
        ImageResizer.CanResize("image/gif").ShouldBeFalse();
    }

    #endregion

    #region ResizeIfNeeded Tests

    [TestMethod]
    public void ResizeIfNeeded_SmallImage_ReturnsOriginal()
    {
        // Arrange - create a small test image (under 500KB)
        byte[] smallImage = CreateTestImage(100, 100);
        smallImage.Length.ShouldBeLessThan(500 * 1024);

        // Act
        (byte[] result, string contentType) = ImageResizer.ResizeIfNeeded(smallImage, "image/png");

        // Assert - should return original unchanged
        result.ShouldBe(smallImage);
        contentType.ShouldBe("image/png");
    }

    [TestMethod]
    public void ResizeIfNeeded_LargeImage_ResizesToUnderLimit()
    {
        // Arrange - create a large test image (over 500KB) with noise to prevent compression
        byte[] largeImage = CreateNoisyTestImage(2000, 2000);
        largeImage.Length.ShouldBeGreaterThan(500 * 1024);

        // Act
        (byte[] result, string contentType) = ImageResizer.ResizeIfNeeded(largeImage, "image/png");

        // Assert - should be resized to under 500KB
        result.Length.ShouldBeLessThanOrEqualTo(500 * 1024);
        result.Length.ShouldBeLessThan(largeImage.Length);
        contentType.ShouldBe("image/jpeg"); // Converted to JPEG for compression
    }

    [TestMethod]
    public void ResizeIfNeeded_LargeImage_NeverIncreasesSize()
    {
        // Arrange
        byte[] largeImage = CreateNoisyTestImage(1500, 1500);
        int originalSize = largeImage.Length;

        // Act
        (byte[] result, string contentType) = ImageResizer.ResizeIfNeeded(largeImage, "image/png");

        // Assert - result should never be larger than original
        result.Length.ShouldBeLessThanOrEqualTo(originalSize);
    }

    [TestMethod]
    public void ResizeIfNeeded_CustomMaxSize_RespectsLimit()
    {
        // Arrange - create noisy image and set a smaller limit
        byte[] image = CreateNoisyTestImage(500, 500);
        int customMaxSize = 50 * 1024; // 50KB

        // Act
        (byte[] result, string contentType) = ImageResizer.ResizeIfNeeded(image, "image/png", customMaxSize);

        // Assert
        result.Length.ShouldBeLessThanOrEqualTo(customMaxSize);
    }

    [TestMethod]
    public void ResizeIfNeeded_InvalidImageData_ReturnsOriginal()
    {
        // Arrange - invalid image data
        byte[] invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act
        (byte[] result, string contentType) = ImageResizer.ResizeIfNeeded(invalidData, "image/png", 2); // Very small limit

        // Assert - should return original since it can't be decoded
        result.ShouldBe(invalidData);
        contentType.ShouldBe("image/png");
    }

    [TestMethod]
    public void ResizeIfNeeded_PreservesAspectRatio()
    {
        // Arrange - create a rectangular noisy image
        int originalWidth = 1600;
        int originalHeight = 800;
        byte[] image = CreateNoisyTestImage(originalWidth, originalHeight);
        double originalAspectRatio = (double)originalWidth / originalHeight;

        // Act
        (byte[] result, string _) = ImageResizer.ResizeIfNeeded(image, "image/png", 50 * 1024);

        // Assert - decode result and check aspect ratio is preserved
        using SKBitmap? resultBitmap = SKBitmap.Decode(result);
        resultBitmap.ShouldNotBeNull();

        double resultAspectRatio = (double)resultBitmap.Width / resultBitmap.Height;
        resultAspectRatio.ShouldBe(originalAspectRatio, 0.1); // Allow small tolerance
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test PNG image with the specified dimensions.
    /// Uses a gradient pattern which compresses well.
    /// </summary>
    private static byte[] CreateTestImage(int width, int height)
    {
        using SKBitmap bitmap = new(width, height);

        // Fill with a gradient pattern to create realistic compression scenarios
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte r = (byte)((x * 255) / width);
                byte g = (byte)((y * 255) / height);
                byte b = (byte)(((x + y) * 127) / (width + height));
                bitmap.SetPixel(x, y, new SKColor(r, g, b));
            }
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData? data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray() ?? [];
    }

    /// <summary>
    /// Creates a test PNG image with random noise that doesn't compress well.
    /// This ensures the image is large enough to test resizing.
    /// </summary>
    private static byte[] CreateNoisyTestImage(int width, int height)
    {
        using SKBitmap bitmap = new(width, height);
        Random random = new(42); // Fixed seed for reproducibility

        // Fill with random noise to prevent efficient compression
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte r = (byte)random.Next(256);
                byte g = (byte)random.Next(256);
                byte b = (byte)random.Next(256);
                bitmap.SetPixel(x, y, new SKColor(r, g, b));
            }
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData? data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray() ?? [];
    }

    #endregion
}
