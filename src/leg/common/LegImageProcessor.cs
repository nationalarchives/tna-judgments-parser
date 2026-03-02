
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using Imaging = UK.Gov.NationalArchives.Imaging;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Processes images for legislation documents, converting formats and applying S3 naming conventions.
/// </summary>
class LegImageProcessor {

    private static readonly ILogger logger = Logging.Factory.CreateLogger<LegImageProcessor>();

    /// <summary>
    /// Process images for a legislation document: convert formats, rename to S3 convention, update references.
    /// </summary>
    /// <param name="document">The parsed document containing images</param>
    /// <param name="language">Language code (default: "en")</param>
    /// <returns>Processed images with S3-compliant names and GIF format</returns>
    public static IEnumerable<IImage> ProcessImages(IDocument document, string language = "en") {
        if (document.Images == null || !document.Images.Any()) {
            logger.LogDebug("No images to process");
            return Enumerable.Empty<IImage>();
        }

        string shortUri = document.Meta.ShortUriComponent;
        if (string.IsNullOrEmpty(shortUri)) {
            logger.LogWarning("Document has no ShortUriComponent, cannot generate S3 image names");
            return document.Images;
        }

        logger.LogInformation("Processing {Count} images for {Uri}", document.Images.Count(), shortUri);

        // Collect image references from all parts of the document (Header, Body, Annexes)
        IEnumerable<IImageRef> refs;
        if (document is IDividedDocument dividedDoc) {
            var bodyRefs = UK.Gov.Legislation.Judgments.Util.Descendants<IImageRef>(dividedDoc.Body);
            var headerRefs = dividedDoc.Header != null
                ? UK.Gov.Legislation.Judgments.Util.Descendants<IImageRef>(dividedDoc.Header)
                : Enumerable.Empty<IImageRef>();
            var annexRefs = dividedDoc.Annexes != null
                ? dividedDoc.Annexes.SelectMany(a => UK.Gov.Legislation.Judgments.Util.Descendants<IImageRef>(a.Contents))
                : Enumerable.Empty<IImageRef>();
            refs = bodyRefs.Concat(headerRefs).Concat(annexRefs);
        } else if (document is IUndividedDocument undividedDoc) {
            refs = UK.Gov.Legislation.Judgments.Util.Descendants<IImageRef>(undividedDoc.Body);
        } else {
            refs = Enumerable.Empty<IImageRef>();
        }

        // Step 1: Convert EMF/WMF to intermediate format (reuse existing ImageConverter)
        List<IImage> images = document.Images.ToList();
        ImageConverter.ConvertImages(() => images, refs, newImages => images = newImages);

        // Step 2: Convert all images to GIF, rename to S3 convention, update references
        List<IImage> renamedImages = new List<IImage>();
        int sequence = 1;

        foreach (IImage image in images) {
            // Convert to GIF
            byte[] gifData;
            try {
                gifData = Imaging.Convert.ConvertToGif(image.Read());
                logger.LogDebug("Converted {Name} to GIF", image.Name);
            } catch (Exception e) {
                logger.LogWarning("Cannot convert {Name} to GIF: {Message}. Skipping image.", image.Name, e.Message);
                sequence++;
                continue;
            }

            // Generate new S3-style filename with .gif extension
            string newFilename = ImageNaming.GenerateFilename(shortUri, language, sequence, "gif");
            string newSrc = "images/" + newFilename;

            logger.LogDebug("Renaming image: {OldName} -> {NewName} (src: {NewSrc})",
                image.Name, newFilename, newSrc);

            // Update all references to this image
            foreach (var imageRef in refs.Where(r => r.Src == image.Name)) {
                imageRef.Src = newSrc;
                logger.LogTrace("Updated image reference from {OldSrc} to {NewSrc}",
                    image.Name, newSrc);
            }

            renamedImages.Add(new RenamedImage {
                Name = newFilename,
                ContentType = "image/gif",
                Data = gifData,
                RelativePath = ImageNaming.GenerateRelativePath(shortUri, language, sequence, "gif")
            });
            sequence++;
        }

        logger.LogInformation("Processed {Count} images with S3 naming convention", renamedImages.Count);
        return renamedImages;
    }

}

/// <summary>
/// An image that has been renamed to S3 convention.
/// </summary>
class RenamedImage : IImage {

    public string Name { get; internal init; }

    public string ContentType { get; internal init; }

    internal byte[] Data { get; init; }

    /// <summary>
    /// Full relative path for disk export (e.g. "ukia/2025/1/images/ukia_20251_en_001.png")
    /// </summary>
    public string RelativePath { get; internal init; }

    public Stream Content() => new MemoryStream(Data);

    public byte[] Read() => Data;

}

}
