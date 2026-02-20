
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Common;

namespace UK.Gov.Legislation {

interface IXmlDocument {

    string Id { get; }

    System.Xml.XmlDocument Document { get; }

    string Serialize();

    IEnumerable<IImage> Images { get; }

    /// <summary>
    /// Save images to disk in S3-mirrored folder structure alongside the output directory.
    /// </summary>
    /// <param name="outputDirectory">Directory where the AKN file is/will be saved</param>
    void SaveImages(string outputDirectory);

}

class XmlDocument_ : IXmlDocument {

    private static readonly ILogger logger = Logging.Factory.CreateLogger<XmlDocument_>();

    public string Id { get; }

    public System.Xml.XmlDocument Document { get; internal init; }

    public string Serialize() => UK.Gov.NationalArchives.Judgments.Api.Parser.SerializeXml(Document);

    public IEnumerable<IImage> Images { get; internal init; }

    /// <summary>
    /// Save images to disk in S3-mirrored folder structure alongside the output directory.
    /// Images are saved to {outputDirectory}/{type}/{year}/{number}/images/
    /// </summary>
    /// <param name="outputDirectory">Directory where the AKN file is/will be saved</param>
    public void SaveImages(string outputDirectory) {
        if (Images == null || !Images.Any()) {
            logger.LogDebug("No images to save");
            return;
        }

        if (string.IsNullOrEmpty(outputDirectory)) {
            throw new ArgumentException("Output directory cannot be null or empty", nameof(outputDirectory));
        }

        logger.LogInformation("Saving {Count} images to {Directory}", Images.Count(), outputDirectory);

        string resolvedBase = Path.GetFullPath(outputDirectory) + Path.DirectorySeparatorChar;

        int savedCount = 0;
        foreach (var image in Images) {
            if (image is not RenamedImage renamedImage || string.IsNullOrEmpty(renamedImage.RelativePath)) {
                logger.LogWarning("Image {Name} does not have a relative path and cannot be saved", image.Name);
                continue;
            }

            string fullPath = Path.GetFullPath(Path.Combine(resolvedBase, renamedImage.RelativePath));

            // Guard against path traversal: ensure the resolved path stays within outputDirectory
            if (!fullPath.StartsWith(resolvedBase, StringComparison.OrdinalIgnoreCase)) {
                logger.LogWarning("Skipping image {Name}: resolved path {Path} escapes output directory", image.Name, fullPath);
                continue;
            }

            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
                logger.LogDebug("Created directory: {Directory}", directory);
            }

            File.WriteAllBytes(fullPath, image.Read());
            logger.LogDebug("Saved image: {Path}", fullPath);
            savedCount++;
        }

        logger.LogInformation("Saved {Count} images to {Directory}", savedCount, outputDirectory);
    }

}

}
