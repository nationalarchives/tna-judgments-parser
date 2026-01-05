
using System;
using System.Collections.Generic;
using System.IO;

namespace Backlog.Src
{
    /// <summary>
    /// Provides file operations for processing backlog documents, including UUID lookup,
    /// file reading, and copying operations between tribunal and transfer metadata systems.
    /// </summary>
    class BacklogFiles
    {
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
        /// <param name="filePath">The full file path from tribunal metadata to normalize</param>
        /// <param name="judgmentsFilePath">The base judgments file path prefix to remove from the full path</param>
        /// <returns>The relative file path for matching against transfer metadata</returns>
        /// <exception cref="ArgumentException">Thrown when filePath does not start with judgmentsFilePath</exception>
        private static string GetFilePathFromTribunalMetadata(string filePath, string judgmentsFilePath)
        {
            if (!filePath.StartsWith(judgmentsFilePath))
                throw new ArgumentException($"FilePath {filePath} must start with {judgmentsFilePath}", nameof(filePath));
            var relativePath = filePath.Substring(judgmentsFilePath.Length);
            return relativePath;
        }

        /// <summary>
        /// Searches the transfer metadata CSV file to find the UUID matching the given tribunal data file path.
        /// Performs path normalization and comparison to locate the corresponding UUID in the transfer metadata.
        /// </summary>
        /// <param name="pathToDataFolder">Path to the data folder containing the metadata subdirectories</param>
        /// <param name="tribunalDataFilePath">The tribunal data file path to match against metadata entries</param>
        /// <param name="hmctsFilePath">The HMCTS file path prefix used for path normalization</param>
        /// <returns>The matching UUID from the transfer metadata file</returns>
        /// <exception cref="FileNotFoundException">Thrown when the metadata file is not found or no matching UUID is found</exception>
        private static string FindUuidInTransferMetadata(string pathToDataFolder, string tribunalDataFilePath, string hmctsFilePath) {
            var transferMetadataPath = Path.Combine(pathToDataFolder, TDR_METADATA_DIR, METADATA_FILENAME);
            System.Console.WriteLine($"Finding UUID for {tribunalDataFilePath} in transfer metadata file {transferMetadataPath}");
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
                if (!filePath.StartsWith(hmctsFilePath)) continue;
                var metadataRelativePath = filePath.Substring(hmctsFilePath.Length);

                if (metadataRelativePath.Replace('\\', '/').Equals(tribunalDataFilePath.Replace('\\', '/')))
                    return parts[UUID_COLUMN];
            }

            throw new FileNotFoundException(
                $"No UUID found for {tribunalDataFilePath} in {transferMetadataPath}"
            );
        }

        /// <summary>
        /// Retrieves the UUID for a given metadata line by performing path normalization and lookup 
        /// in the transfer metadata CSV file. Combines tribunal metadata path extraction with UUID searching.
        /// </summary>
        /// <param name="pathToDataFolder">Base path to the data folder containing metadata and documents</param>
        /// <param name="meta">Metadata line containing file information from tribunal metadata</param>
        /// <param name="judgmentsFilePath">The base judgments file path for path normalization</param>
        /// <param name="hmctsFilePath">The HMCTS file path prefix for metadata matching</param>
        /// <returns>UUID from the transfer metadata file corresponding to the metadata line</returns>
        /// <exception cref="FileNotFoundException">Thrown when no matching UUID is found in transfer metadata</exception>
        /// <exception cref="ArgumentException">Thrown when file path normalization fails</exception>
        private static string GetUuid(string pathToDataFolder, Metadata.Line meta, string judgmentsFilePath, string hmctsFilePath) {
            var tribunalDataFilePath = GetFilePathFromTribunalMetadata(meta.FilePath, judgmentsFilePath);
            System.Console.WriteLine($"Tribunal Metadata filepath: {tribunalDataFilePath}");
            return FindUuidInTransferMetadata(pathToDataFolder, tribunalDataFilePath, hmctsFilePath);
        }

        /// <summary>
        /// Reads the contents of a file using its metadata information by resolving the UUID
        /// and locating the corresponding document in the court documents directory.
        /// Handles special case for .doc files which are stored as .docx files.
        /// </summary>
        /// <param name="pathToDataFolder">Base path to the data folder containing court documents</param>
        /// <param name="meta">Metadata line containing file information and extension details</param>
        /// <param name="judgmentsFilePath">The base judgments file path for path normalization</param>
        /// <param name="hmctsFilePath">The HMCTS file path prefix for UUID lookup</param>
        /// <returns>File contents as a byte array</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file or its UUID cannot be found</exception>
        /// <exception cref="ArgumentException">Thrown when path normalization fails</exception>
        internal static byte[] ReadFile(string pathToDataFolder, Metadata.Line meta, string judgmentsFilePath, string hmctsFilePath) {
            string uuid = GetUuid(pathToDataFolder, meta, judgmentsFilePath, hmctsFilePath);
            string documentPath = Path.Combine(pathToDataFolder, COURT_DOCUMENTS_DIR);
            string path = Path.Combine(documentPath, uuid);
         
            if (meta.Extension.ToLower() == ".doc")
                path += ".docx";

            return File.ReadAllBytes(path);
        }

        /// <summary>
        /// Copies all files from the court documents directory to new locations with their correct file extensions.
        /// Handles the special case where .doc files are stored as .docx files but should be copied with 
        /// their original .doc extension. Processes multiple metadata lines in batch.
        /// </summary>
        /// <param name="pathToDataFolder">Base path to the data folder containing court documents</param>
        /// <param name="lines">List of metadata lines containing file information and extensions</param>
        /// <param name="judgmentsFilePath">The base judgments file path for path normalization</param>
        /// <param name="hmctsFilePath">The HMCTS file path prefix for UUID resolution</param>
        /// <exception cref="FileNotFoundException">Thrown when source files or UUIDs cannot be found</exception>
        /// <exception cref="ArgumentException">Thrown when path normalization fails</exception>
        internal static void CopyAllFilesWithExtension(string pathToDataFolder, List<Metadata.Line> lines, string judgmentsFilePath, string hmctsFilePath) {
            foreach (var line in lines)
            {
                string uuid = GetUuid(pathToDataFolder, line, judgmentsFilePath, hmctsFilePath);
                string sourcePath = Path.Combine(pathToDataFolder, COURT_DOCUMENTS_DIR, uuid);
                string targetPath = sourcePath + line.Extension.ToLower();
                
                string sourceFullPath = sourcePath;
                if (line.Extension.ToLower() == ".doc")
                    sourceFullPath += ".docx";
                
                byte[] data = File.ReadAllBytes(sourceFullPath);
                File.WriteAllBytes(targetPath, data);
            }
        }

    }

}
