
using System;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Generates S3-compatible image filenames and paths for legislation associated documents.
///
/// Two separate concepts are used:
///   shortUri       - the document's FRBR expression path, used for the S3 folder.
///                    e.g. "uksi/2025/81/impacts/2025/17"
///   fileIdentifier - the identifier encoded in the filename, which uniquely names the
///                    associated document itself. For IAs this is "ukia/{year}/{number}";
///                    for EMs and other types it is the same as shortUri.
///                    e.g. "ukia/2025/17"
///
/// This keeps images from different associated documents under the same legislation
/// both path-separated (different S3 folders) and name-separated (different filenames).
/// </summary>
public static class ImageNaming {

    /// <summary>
    /// Generate an S3-style image filename from a document file identifier.
    /// All path segments of the identifier are joined with underscores.
    /// </summary>
    /// <param name="fileIdentifier">
    /// Document identifier path, e.g. "ukia/2025/17" or "uksi/2013/2911/memorandum/1".
    /// Year and number segments are separated (never concatenated).
    /// </param>
    /// <param name="language">Language code, e.g. "en" or "cy"</param>
    /// <param name="sequence">Image sequence number (1-based)</param>
    /// <param name="extension">File extension without dot, e.g. "gif"</param>
    /// <returns>
    /// Filename like "ukia_2025_17_en_001.gif" or "uksi_2013_2911_memorandum_1_en_001.gif"
    /// </returns>
    public static string GenerateFilename(string fileIdentifier, string language, int sequence, string extension) {
        if (string.IsNullOrEmpty(fileIdentifier))
            throw new ArgumentException("File identifier cannot be null or empty", nameof(fileIdentifier));

        string baseName = fileIdentifier.Replace('/', '_');
        return $"{baseName}_{language}_{sequence:D3}.{extension}";
    }

    /// <summary>
    /// Generate the S3 folder path for images belonging to a document.
    /// Uses the document's full FRBR expression path so images are scoped under
    /// the associated document, not at the parent legislation root.
    /// </summary>
    /// <param name="shortUri">Full document expression path, e.g. "uksi/2025/81/impacts/2025/17"</param>
    /// <returns>Folder path like "uksi/2025/81/impacts/2025/17/images"</returns>
    public static string GenerateFolderPath(string shortUri) {
        if (string.IsNullOrEmpty(shortUri))
            throw new ArgumentException("Short URI cannot be null or empty", nameof(shortUri));

        return shortUri + "/images";
    }

    /// <summary>
    /// Generate the full S3 key (folder path + filename).
    /// </summary>
    /// <param name="shortUri">Full document expression path used for the folder</param>
    /// <param name="fileIdentifier">Document identifier used for the filename</param>
    /// <param name="language">Language code, e.g. "en"</param>
    /// <param name="sequence">Image sequence number (1-based)</param>
    /// <param name="extension">File extension without dot, e.g. "gif"</param>
    /// <returns>
    /// Full S3 key like "uksi/2025/81/impacts/2025/17/images/ukia_2025_17_en_001.gif"
    /// </returns>
    public static string GenerateRelativePath(string shortUri, string fileIdentifier, string language, int sequence, string extension) {
        string folder = GenerateFolderPath(shortUri);
        string filename = GenerateFilename(fileIdentifier, language, sequence, extension);
        return folder + "/" + filename;
    }

    /// <summary>
    /// Extract file extension from content type or filename.
    /// </summary>
    /// <param name="contentType">MIME type, e.g. "image/png"</param>
    /// <param name="fallbackFilename">Fallback filename to extract extension from</param>
    /// <returns>Extension without dot, e.g. "png", "jpg", "gif"</returns>
    public static string GetExtension(string contentType, string fallbackFilename = null) {
        if (!string.IsNullOrEmpty(contentType)) {
            string ext = contentType.ToLowerInvariant() switch {
                "image/png"  => "png",
                "image/jpeg" => "jpg",
                "image/jpg"  => "jpg",
                "image/gif"  => "gif",
                "image/bmp"  => "bmp",
                "image/tiff" => "tiff",
                "image/webp" => "webp",
                _ => null
            };
            if (ext != null)
                return ext;
        }

        if (!string.IsNullOrEmpty(fallbackFilename)) {
            int dot = fallbackFilename.LastIndexOf('.');
            if (dot >= 0 && dot < fallbackFilename.Length - 1)
                return fallbackFilename[(dot + 1)..].ToLowerInvariant();
        }

        return "png";
    }

}

}
