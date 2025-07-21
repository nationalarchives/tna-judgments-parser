
using System;
using System.Collections.Generic;
using System.IO;

namespace Backlog.Src.Batch.One
{
    /// <summary>
    /// Handles file operations for the backlog processing module.
    /// </summary>
    class Files
    {
        // Directory structure constants
        private const string TDR_METADATA_DIR = "tdr_metadata";
        private const string COURT_DOCUMENTS_DIR = "court_documents";
        private const string METADATA_FILENAME = "file-metadata.csv";
        private const string JUDGMENT_FILES_PATH = "JudgmentFiles";
        private const string HMCTS_FILES_PATH = "data/HMCTS_Judgment_Files";

        /// <summary>
        /// Gets the relative path from a judgment file path.
        /// </summary>
        /// <param name="filePath">The full file path to normalize</param>
        /// <returns>The normalized relative path</returns>
        private static string GetNormalizedRelativePath(string filePath) {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("FilePath cannot be empty", nameof(filePath));

            var normalizedPath = filePath.Replace('\\', '/');
            if (!normalizedPath.StartsWith(JUDGMENT_FILES_PATH))
                throw new ArgumentException($"FilePath must start with {JUDGMENT_FILES_PATH}", nameof(filePath));

            return normalizedPath.Substring(JUDGMENT_FILES_PATH.Length + 1);
        }

        /// <summary>
        /// Gets the metadata file path and verifies it exists.
        /// </summary>
        /// <param name="pathToDataFolder">Base path to the data folder</param>
        /// <returns>The full path to the metadata file</returns>
        private static string GetMetadataFilePath(string pathToDataFolder) {
            var metadataPath = Path.Combine(pathToDataFolder, TDR_METADATA_DIR, METADATA_FILENAME);
            if (!File.Exists(metadataPath))
                throw new FileNotFoundException($"Metadata file not found at {metadataPath}");
            return metadataPath;
        }

        /// <summary>
        /// Finds the UUID in the metadata file matching the given relative path.
        /// </summary>
        /// <param name="metadataPath">Path to the metadata CSV file</param>
        /// <param name="relativePath">The relative path to match</param>
        /// <returns>The matching UUID from the metadata</returns>
        private static string FindUuidInMetadata(string metadataPath, string relativePath) {
            foreach (var line in File.ReadLines(metadataPath)) {
                if (string.IsNullOrEmpty(line)) continue;
                var parts = line.Split(',');
                if (parts.Length < 27) continue;  // Need at least up to the UUID column (27th column)

                var filePath = parts[4].Replace('\\', '/');  // clientside_original_filepath is column 5
                if (!filePath.StartsWith(HMCTS_FILES_PATH)) continue;
                
                var metadataRelativePath = filePath.Substring(HMCTS_FILES_PATH.Length + 1);
                if (metadataRelativePath == relativePath)
                    return parts[26];  // UUID is the 27th column
            }

            throw new FileNotFoundException(
                $"No UUID found for {relativePath} in {metadataPath}"
            );
        }

        /// <summary>
        /// Gets the UUID for a given metadata line by looking it up in the metadata CSV.
        /// </summary>
        /// <param name="pathToDataFolder">Base path to the data folder</param>
        /// <param name="meta">Metadata line containing file information</param>
        /// <returns>UUID from the metadata file</returns>
        private static string GetUuid(string pathToDataFolder, Metadata.Line meta) {
            var relativePath = GetNormalizedRelativePath(meta.FilePath);
            var metadataPath = GetMetadataFilePath(pathToDataFolder);
            return FindUuidInMetadata(metadataPath, relativePath);
        }

        /// <summary>
        /// Reads a file's contents using its metadata information.
        /// </summary>
        /// <param name="pathToDataFolder">Base path to the data folder</param>
        /// <param name="meta">Metadata line containing file information</param>
        /// <returns>File contents as a byte array</returns>
        internal static byte[] ReadFile(string pathToDataFolder, Metadata.Line meta) {
            string uuid = GetUuid(pathToDataFolder, meta);
            string documentPath = Path.Combine(pathToDataFolder, COURT_DOCUMENTS_DIR);
            string path = Path.Combine(documentPath, uuid);
            
            if (meta.Extension.ToLower() == ".doc")
                path += ".docx";
                
            return File.ReadAllBytes(path);
        }

        /// <summary>
        /// Copies files to new locations with their correct extensions.
        /// </summary>
        /// <param name="pathToDataFolder">Base path to the data folder</param>
        /// <param name="lines">List of metadata lines containing file information</param>
        internal static void CopyAllFilesWithExtension(string pathToDataFolder, List<Metadata.Line> lines) {
            foreach (var line in lines)
            {
                string uuid = GetUuid(pathToDataFolder, line);
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
