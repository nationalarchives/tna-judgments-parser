
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
        private const string COURT_DOCUMENTS_DIR = "court_documents/e14fb247-5d9b-42b8-9238-52ae3bd8345b";
        private const string METADATA_FILENAME = "file-metadata.csv";
        private const string JUDGMENT_FILES_PATH = "JudgmentFiles";
        private const string HMCTS_FILES_PATH = "data/HMCTS_Judgment_Files";

        /// <summary>
        /// Gets the UUID for a given metadata line by looking it up in the metadata CSV.
        /// </summary>
        /// <param name="pathToDataFolder">Base path to the data folder</param>
        /// <param name="meta">Metadata line containing file information</param>
        /// <returns>UUID from the metadata file, or null if not found</returns>
        private static string GetUuid(string pathToDataFolder, Metadata.Line meta) {
            if (string.IsNullOrEmpty(meta.FilePath))
                throw new ArgumentException("FilePath cannot be empty", nameof(meta));

            // Handle paths in OS-agnostic way
            var normalizedPath = meta.FilePath.Replace('\\', '/');
            if (!normalizedPath.StartsWith(JUDGMENT_FILES_PATH))
                throw new ArgumentException($"FilePath must start with {JUDGMENT_FILES_PATH}", nameof(meta));

            // Extract the relative path after JudgmentFiles/
            var relativePath = normalizedPath.Substring(JUDGMENT_FILES_PATH.Length + 1);
                
            var metadataPath = Path.Combine(pathToDataFolder, TDR_METADATA_DIR, METADATA_FILENAME);
            if (!File.Exists(metadataPath))
                throw new FileNotFoundException($"Metadata file not found at {metadataPath}");

            var lines = File.ReadLines(metadataPath);
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                var parts = line.Split(',');
                if (parts.Length < 25) continue;  // Need at least up to the UUID column

                var filePath = parts[4].Replace('\\', '/');  // clientside_original_filepath is column 5
                // Look for files that have the same relative path after HMCTS_FILES_PATH
                if (!filePath.StartsWith(HMCTS_FILES_PATH)) continue;
                var metadataRelativePath = filePath.Substring(HMCTS_FILES_PATH.Length + 1);
                if (metadataRelativePath != relativePath) continue;

                return parts[25];  // UUID is the last column
            }

            throw new FileNotFoundException(
                $"No UUID found for {relativePath} in {metadataPath}. " +
                $"Original path: {meta.FilePath}"
            );
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
