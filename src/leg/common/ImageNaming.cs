
using System;
using System.IO;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Generates S3-compatible image filenames and paths for legislation documents.
/// </summary>
public static class ImageNaming {

    /// <summary>
    /// Generate an S3-style image filename.
    /// </summary>
    /// <param name="shortUri">Short URI component (e.g. "ukia/2025/1")</param>
    /// <param name="language">Language code (e.g. "en" for English, "cy" for Welsh)</param>
    /// <param name="sequence">Image sequence number (1-based)</param>
    /// <param name="extension">File extension without dot (e.g. "png", "jpg")</param>
    /// <returns>Filename like "ukia_20251_en_001.png"</returns>
    public static string GenerateFilename(string shortUri, string language, int sequence, string extension) {
        if (string.IsNullOrEmpty(shortUri))
            throw new ArgumentException("Short URI cannot be null or empty", nameof(shortUri));
        
        // Parse type, year, and number from shortUri (e.g. "ukia/2025/1")
        string[] parts = shortUri.Split('/');
        if (parts.Length < 3)
            throw new ArgumentException($"Invalid short URI format: {shortUri}. Expected format: type/year/number", nameof(shortUri));
        
        string type = parts[0];
        string year = parts[1];
        string number = parts[2];
        
        // Format: {type}_{year}{number}_{lang}_{seq:D3}.{ext}
        // e.g. ukia_20251_en_001.png
        return $"{type}_{year}{number}_{language}_{sequence:D3}.{extension}";
    }

    /// <summary>
    /// Generate the relative folder path for images.
    /// </summary>
    /// <param name="shortUri">Short URI component (e.g. "ukia/2025/1")</param>
    /// <returns>Folder path like "ukia/2025/1/images"</returns>
    public static string GenerateFolderPath(string shortUri) {
        if (string.IsNullOrEmpty(shortUri))
            throw new ArgumentException("Short URI cannot be null or empty", nameof(shortUri));
        
        // {type}/{year}/{number}/images/
        // e.g. ukia/2025/1/images
        return Path.Combine(shortUri, "images");
    }

    /// <summary>
    /// Generate the full relative path (folder + filename).
    /// </summary>
    /// <param name="shortUri">Short URI component (e.g. "ukia/2025/1")</param>
    /// <param name="language">Language code (e.g. "en")</param>
    /// <param name="sequence">Image sequence number (1-based)</param>
    /// <param name="extension">File extension without dot (e.g. "png")</param>
    /// <returns>Full relative path like "ukia/2025/1/images/ukia_20251_en_001.png"</returns>
    public static string GenerateRelativePath(string shortUri, string language, int sequence, string extension) {
        string folder = GenerateFolderPath(shortUri);
        string filename = GenerateFilename(shortUri, language, sequence, extension);
        return Path.Combine(folder, filename);
    }

    /// <summary>
    /// Extract file extension from content type or filename.
    /// </summary>
    /// <param name="contentType">MIME type (e.g. "image/png")</param>
    /// <param name="fallbackFilename">Fallback filename to extract extension from</param>
    /// <returns>Extension without dot (e.g. "png", "jpg", "gif")</returns>
    public static string GetExtension(string contentType, string fallbackFilename = null) {
        // Try to get from content type first
        if (!string.IsNullOrEmpty(contentType)) {
            string ext = contentType.ToLowerInvariant() switch {
                "image/png" => "png",
                "image/jpeg" => "jpg",
                "image/jpg" => "jpg",
                "image/gif" => "gif",
                "image/bmp" => "bmp",
                "image/tiff" => "tiff",
                "image/webp" => "webp",
                _ => null
            };
            if (ext != null)
                return ext;
        }

        // Fall back to filename extension
        if (!string.IsNullOrEmpty(fallbackFilename)) {
            string ext = Path.GetExtension(fallbackFilename)?.TrimStart('.');
            if (!string.IsNullOrEmpty(ext))
                return ext.ToLowerInvariant();
        }

        // Default to png
        return "png";
    }

}

}
