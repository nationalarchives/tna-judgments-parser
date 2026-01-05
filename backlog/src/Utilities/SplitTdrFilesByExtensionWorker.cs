#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using CsvHelper;

using Microsoft.Extensions.Logging;

namespace Backlog.Utilities;

/// <summary>
///     Use this to split TDR processed files into files grouped by extension to prepare for file conversions.
///     It can be run against a single TDR folder or a folder containing multiple TDR folders.
///     Each run will generate a new output folder containing copied files with extensions as specified in the TDR
///     metadata.
/// </summary>
internal class SplitTdrFilesByExtensionWorker(ILogger<SplitTdrFilesByExtensionWorker> logger)
{
    public int Run(DirectoryInfo pathWithDirectoriesToSplit, DirectoryInfo destinationPath)
    {
        var destinationFolder = destinationPath.CreateSubdirectory(DateTime.Now.ToString("yyyyMMdd_hhmmss"));

        FindAndProcessTdrBuckets(pathWithDirectoriesToSplit.FullName, pathWithDirectoriesToSplit.FullName,
            destinationFolder.FullName);

        logger.LogInformation("------------------------------");
        return 0;
    }

    private void FindAndProcessTdrBuckets(string rootOriginalFolder, string originalFolder, string destinationFolder)
    {
        try
        {
            if (IsTdrBucket(originalFolder))
            {
                ProcessTdrBucket(rootOriginalFolder, originalFolder, destinationFolder);
            }
            else
            {
                logger.LogInformation("{Path} is not a TDR bucket. Processing subdirectories", originalFolder);
                var subdirectories = Directory.GetDirectories(originalFolder);
                foreach (var subdirectory in subdirectories)
                {
                    FindAndProcessTdrBuckets(rootOriginalFolder, subdirectory, destinationFolder);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error processing {Folder}", originalFolder);
        }
    }

    private void ProcessTdrBucket(string rootOriginalFolder, string originalFolder, string destinationFolder)
    {
        var relativePathFromRoot = Path.GetRelativePath(rootOriginalFolder, originalFolder);
        var destinationFolderPathWithTdrBucketName = Path.Join(destinationFolder, relativePathFromRoot);
        CopyTdrDocsToNewExtensionFoldersInDestination(originalFolder, destinationFolderPathWithTdrBucketName);
    }

    private static bool IsTdrBucket(string folder)
    {
        return Directory.Exists($"{folder}/court_documents");
    }

    private void CopyTdrDocsToNewExtensionFoldersInDestination(string tdrBucketFolder, string destinationFolder)
    {
        var courtDocumentsFolder = $"{tdrBucketFolder}/court_documents";

        logger.LogInformation("""
                              ------------------------------
                              Copying files from - {CourtDocumentsFolder}
                                to extension folders in {DestinationFolder}
                              """, courtDocumentsFolder, destinationFolder);

        var files = GetExpectedFilesFromTdrMetadata(tdrBucketFolder);
        var extensionDictionary = new Dictionary<string, ExtensionFolderManager>();

        foreach (var (uuid, preTdrFileName, tdrFileType) in files)
        {
            try
            {
                var isPreTdrFolder = string.Equals(tdrFileType, "Folder", StringComparison.InvariantCultureIgnoreCase);
                var isMetadataCsv = preTdrFileName.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase) &&
                                    preTdrFileName.Contains("metadata", StringComparison.InvariantCultureIgnoreCase);
                if (isPreTdrFolder || isMetadataCsv)
                {
                    continue;
                }

                var extension = Path.GetExtension(preTdrFileName).ToLower();

                if (!extensionDictionary.TryGetValue(extension, out var extensionFolderManager))
                {
                    extensionFolderManager = new ExtensionFolderManager(extension);
                    extensionFolderManager.CreateExtensionFolderIn(destinationFolder);
                    extensionDictionary.Add(extension, extensionFolderManager);
                }

                extensionFolderManager.CopyFileToExtensionFolder(courtDocumentsFolder, destinationFolder, uuid,
                    preTdrFileName);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error copying file {Uuid}: {OriginalFileName}", uuid, preTdrFileName);
            }
        }

        WriteReport(destinationFolder, extensionDictionary.Values);
    }

    private void WriteReport(string destinationFolder, IEnumerable<ExtensionFolderManager> extensionDictionary)
    {
        var report = new List<string>();
        var anyFailures = false;

        var orderedExtensionFolderManagers =
            extensionDictionary.OrderBy(extensionFolderManager => extensionFolderManager.ExtensionFolderName);

        //Log success/failure to console
        foreach (var extensionFolderManager in orderedExtensionFolderManagers)
        {
            report.Add($"--- {extensionFolderManager.ExtensionFolderName} ---");
            report.Add($"  Total number of expected files: {extensionFolderManager.FileCopyAttempts.Count}");
            report.Add(
                $"  Successfully copied {extensionFolderManager.FileCopyAttempts.FindAll(x => x.success).Count} of {extensionFolderManager.FileCopyAttempts.Count}");

            if (extensionFolderManager.FileCopyAttempts.Any(x => !x.success))
            {
                var unsuccessfulCopyAttempts = extensionFolderManager.FileCopyAttempts.FindAll(x => !x.success);
                report.Add(
                    $"  Failed to copy {unsuccessfulCopyAttempts.Count} of {extensionFolderManager.FileCopyAttempts.Count}");
                foreach (var (fileName, uuid, _) in unsuccessfulCopyAttempts)
                {
                    report.Add($"  - Name: {fileName}   UUID: {uuid}");
                }

                anyFailures = true;
            }
            else
            {
                report.Add("  No failed copy attempts");
            }
        }

        //Log to console
        logger.LogInformation(string.Join(Environment.NewLine, report));

        //Record failures in a file log
        if (anyFailures)
        {
            File.WriteAllLines(Path.Join(destinationFolder, "Failed.txt"), report);
        }
    }

    private static TdrFileMetadataLine[] GetExpectedFilesFromTdrMetadata(string bucketFolder)
    {
        var fileMetadataCsv = $"{bucketFolder}/tdr_metadata/file-metadata.csv";

        if (!File.Exists(fileMetadataCsv))
        {
            Console.WriteLine($"ERROR - no file-metadata.csv found for {bucketFolder}");
            return [];
        }

        using var reader = new StreamReader(fileMetadataCsv);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return csv.GetRecords<TdrFileMetadataLine>().ToArray();
    }
}

/// <summary>
///     Used by CsvHelper
///     Properties are named to match tdr_metadata/file-metadata.csv header
/// </summary>
public record TdrFileMetadataLine(string UUID, string file_name, string file_type);

internal class ExtensionFolderManager(string extension)
{
    public List<(string fileName, string uuid, bool success)> FileCopyAttempts { get; } = [];

    public string ExtensionFolderName =>
        extension == string.Empty
            ? "Other"
            : extension.Substring(1); //Remove the '.' from the beginning of the extension

    public void CreateExtensionFolderIn(string destinationFolder)
    {
        var extensionFolder = Path.Join(destinationFolder, ExtensionFolderName);
        if (Directory.Exists(extensionFolder))
        {
            throw new InvalidOperationException($"Destination folder {extensionFolder} already exists");
        }

        Directory.CreateDirectory(extensionFolder);
    }

    public void CopyFileToExtensionFolder(string oldFolder, string destinationFolder, string uuid,
        string preTdrFileName)
    {
        var oldFilePath = Path.Join(oldFolder, uuid);

        if (File.Exists(oldFilePath))
        {
            var fileNameWithExtension = extension == string.Empty ? uuid : $"{uuid}{extension}";
            var newFilePath = Path.Join(destinationFolder, ExtensionFolderName, fileNameWithExtension);

            File.Copy(oldFilePath, newFilePath);
            FileCopyAttempts.Add((preTdrFileName, uuid, true));
        }
        else
        {
            FileCopyAttempts.Add((preTdrFileName, uuid, false));
        }
    }
}
