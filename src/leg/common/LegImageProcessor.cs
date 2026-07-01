
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;

using UK.Gov.Legislation.Common.Rendering;
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
            // Without a URI we can't name or upload images; drop them (the builder then
            // drops their now-unresolved refs) rather than return raw WImages, which
            // are unsaveable and throw once the source package is disposed.
            logger.LogWarning("Document has no ShortUriComponent; dropping images (cannot generate S3 names)");
            return Enumerable.Empty<IImage>();
        }

        string fileIdentifier = document.Meta.ImageFileIdentifier ?? shortUri;
        string srcBase = document.Meta.ExpressionUri + "/images/";

        logger.LogInformation("Processing {Count} images for {Uri} (file identifier: {Id})",
            document.Images.Count(), shortUri, fileIdentifier);

        // Collect image references from every region (Header, Body, Annexes) for both
        // document shapes; a missed region leaves its images unprocessed and refs dangling.
        var headerRefs = document.Header != null
            ? UK.Gov.Legislation.Judgments.Util.Descendants<IImageRef>(document.Header)
            : Enumerable.Empty<IImageRef>();
        IEnumerable<IImageRef> bodyRefs = document switch {
            IDividedDocument dividedDoc => UK.Gov.Legislation.Judgments.Util.Descendants<IImageRef>(dividedDoc.Body),
            IUndividedDocument undividedDoc => UK.Gov.Legislation.Judgments.Util.Descendants<IImageRef>(undividedDoc.Body),
            _ => Enumerable.Empty<IImageRef>()
        };
        var annexRefs = document.Annexes != null
            ? document.Annexes.SelectMany(a => UK.Gov.Legislation.Judgments.Util.Descendants<IImageRef>(a.Contents))
            : Enumerable.Empty<IImageRef>();
        IEnumerable<IImageRef> refs = headerRefs.Concat(bodyRefs).Concat(annexRefs);

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
                gifData = ConvertToGif(image.Read());
                logger.LogDebug("Converted {Name} to GIF", image.Name);
            } catch (Exception e) {
                gifData = RenderToGif(image);  // fall back to the renderer before giving up
                if (gifData is null) {
                    logger.LogWarning("Cannot convert {Name} to GIF: {Message}. Dropping image.", image.Name, e.Message);
                    sequence++;
                    continue;
                }
                logger.LogInformation("Recovered {Name} via the renderer", image.Name);
            }

            // Generate new S3-style filename with .gif extension
            string newFilename = ImageNaming.GenerateFilename(fileIdentifier, language, sequence, "gif");
            string newSrc = srcBase + newFilename;

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
                RelativePath = ImageNaming.GenerateRelativePath(shortUri, fileIdentifier, language, sequence, "gif")
            });
            sequence++;
        }

        logger.LogInformation("Processed {Count} images with S3 naming convention", renamedImages.Count);
        return renamedImages;
    }

    private static byte[] ConvertToGif(byte[] source) {
        using var image = Image.Load(source);
        using var stream = new MemoryStream();
        image.SaveAsGif(stream);
        return stream.ToArray();
    }

    // Rasterise via the active renderer (LibreOffice) when ImageSharp can't read the
    // image, then take the normal GIF path. Null if no renderer is available or it fails.
    private static byte[] RenderToGif(IImage image) {
        var renderer = RenderSession.Current?.Renderer;
        if (renderer is null)
            return null;
        byte[] png = renderer.RenderImage(image.Read(), Path.GetExtension(image.Name), CancellationToken.None);
        if (png is null || png.Length == 0)
            return null;
        try {
            return ConvertToGif(png);
        } catch (Exception e) {
            logger.LogWarning("Renderer output for {Name} still not convertible: {Message}", image.Name, e.Message);
            return null;
        }
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
