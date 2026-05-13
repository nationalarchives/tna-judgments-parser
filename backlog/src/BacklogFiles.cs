#nullable enable

using System;
using System.IO;
using System.Linq;

using Backlog.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backlog.Src
{
    /// <summary>
    /// Provides file operations for processing backlog documents, including UUID lookup,
    /// file reading, and copying operations between tribunal and transfer metadata systems.
    /// </summary>
    internal class BacklogFiles
    {
        private readonly string transferMetadataPath;
        private readonly ILogger<BacklogFiles> logger;
        private readonly IOptions<BacklogParserOptions> backlogParserOptions;
        private readonly DirectoryInfo courtDocumentsDirectory;

        public BacklogFiles(ILogger<BacklogFiles> logger, IOptions<BacklogParserOptions> backlogParserOptions)
        {
            this.logger = logger;
            this.backlogParserOptions = backlogParserOptions;

            var pathToDataFolder = backlogParserOptions.Value.DataFolderPath;
            transferMetadataPath = Path.Combine(pathToDataFolder, TDR_METADATA_DIR, METADATA_FILENAME);

            courtDocumentsDirectory = new DirectoryInfo(Path.Combine(pathToDataFolder, COURT_DOCUMENTS_DIR));
            if (!courtDocumentsDirectory.Exists)
            {
                throw new DirectoryNotFoundException($"Couldn't find {courtDocumentsDirectory}");
            }
        }

        // Directory structure constants
        private const string TDR_METADATA_DIR = "tdr_metadata";
        private const string COURT_DOCUMENTS_DIR = "court_documents";
        private const string METADATA_FILENAME = "file-metadata.csv";

        private const int MIN_METADATA_COLUMNS = 27; // Minimum columns expected in transfer metadata

        private const int FILE_TYPE_COLUMN = 2; // file_type is column 3 (0-indexed)
        private const int FILE_PATH_COLUMN = 4; // clientside_original_filepath is column 5 (0-indexed)
        private const int UUID_COLUMN = 26; // UUID is the 27th column (0-indexed)

        /// <summary>
        /// Extracts the relative file path from tribunal metadata by removing the base judgments file path prefix.
        /// This normalizes full file paths to relative paths for consistent matching against transfer metadata.
        /// </summary>
        /// <param name="metadataProvidedFilePath">The full file path from tribunal metadata to normalize</param>
        /// <returns>The relative file path for matching against transfer metadata</returns>
        /// <exception cref="ArgumentException">Thrown when filePath does not start with metadataProvidedFilePathPrefix</exception>
        private string GetFilePathFromTribunalMetadata(string metadataProvidedFilePath)
        {
            var metadataProvidedFilePathPrefix = backlogParserOptions.Value.MetadataProvidedFilePathPrefix;
            if (!metadataProvidedFilePath.StartsWith(metadataProvidedFilePathPrefix))
            {
                throw new ArgumentException(
                    $"FilePath {metadataProvidedFilePath} must start with {metadataProvidedFilePathPrefix}",
                    nameof(metadataProvidedFilePath));
            }

            var relativePath = metadataProvidedFilePath.Substring(metadataProvidedFilePathPrefix.Length);
            return relativePath;
        }

        /// <summary>
        /// Retrieves the UUID for a given metadata line by performing path normalization and lookup 
        /// in the transfer metadata CSV file. Combines tribunal metadata path extraction with UUID searching.
        /// </summary>
        /// <param name="metadataProvidedFilePath"></param>
        /// <returns>UUID from the transfer metadata file corresponding to the metadata line</returns>
        /// <exception cref="FileNotFoundException">Thrown when no matching UUID is found in transfer metadata</exception>
        /// <exception cref="ArgumentException">Thrown when file path normalization fails</exception>
        internal string FindUuidInTransferMetadata(string metadataProvidedFilePath)
        {
            var tribunalDataFilePath = GetFilePathFromTribunalMetadata(metadataProvidedFilePath);

            logger.LogInformation(
                "Finding UUID for {TribunalDataFilePath} in transfer metadata file {TransferMetadataPath}",
                tribunalDataFilePath, transferMetadataPath);

            if (!File.Exists(transferMetadataPath))
                throw new FileNotFoundException($"Metadata file not found at {transferMetadataPath}");

            foreach (var line in File.ReadLines(transferMetadataPath))
            {
                if (string.IsNullOrEmpty(line)) continue;
                var parts = line.Split(',');
                if (parts.Length < MIN_METADATA_COLUMNS) continue;

                var fileType = parts[FILE_TYPE_COLUMN];
                if (!fileType.Equals("File")) continue;

                var filePath = parts[FILE_PATH_COLUMN];
                if (!filePath.StartsWith(backlogParserOptions.Value.TransferMetadataFilePathPrefix)) continue;

                var metadataRelativePath = filePath.Substring(backlogParserOptions.Value.TransferMetadataFilePathPrefix.Length);

                if (metadataRelativePath.Replace('\\', '/').Equals(tribunalDataFilePath.Replace('\\', '/')))
                    return parts[UUID_COLUMN];
            }

            throw new FileNotFoundException(
                $"No UUID found for {tribunalDataFilePath} in {transferMetadataPath}"
            );
        }

        /// <summary>
        /// Reads the contents of a file using its metadata information by resolving the UUID
        /// and locating the corresponding document in the court documents directory.
        /// Handles special case for .doc files which are stored as .docx files.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns>File contents as a byte array</returns>
        internal byte[] ReadFile(string uuid)
        {
            var filesWithUuid = courtDocumentsDirectory.GetFiles($"{uuid}*", SearchOption.AllDirectories);
            if (filesWithUuid.Length == 0)
            {
                throw new FileNotFoundException($"Couldn't find file with UUID: {uuid}. It must have been received through TDR in order to have been assigned a UUID so check the original TDR bucket and check any file conversion folders");
            }

            if (filesWithUuid.Length > 1)
            {
                throw new MoreThanOneFileFoundException(
                    $"There should only be one file in {COURT_DOCUMENTS_DIR} matching UUID {uuid} but found {filesWithUuid.Length}: [{string.Join(", ", filesWithUuid.OrderBy(f => f.Name).Select(f => $"\"{f.Name}\""))}]");
            }

            return File.ReadAllBytes(filesWithUuid.Single().FullName);
        }
    }
}
