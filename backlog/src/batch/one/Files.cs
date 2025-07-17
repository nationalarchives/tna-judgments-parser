
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
        /// Gets the UUID for a given metadata line by looking it up in the metadata CSV.
        /// </summary>
        /// <param name="pathToDataFolder">Base path to the data folder</param>
        /// <param name="meta">Metadata line containing file information</param>
        /// <returns>UUID from the metadata file, or null if not found</returns>
        private static string GetUuid(string pathToDataFolder, Metadata.Line meta) {
            string clientside_original_filepath = meta.FilePath
                .Replace(Path.DirectorySeparatorChar == '/' ? JUDGMENT_FILES_PATH : JUDGMENT_FILES_PATH.Replace('/', '\\'),
                        HMCTS_FILES_PATH)
                .Replace('\\', '/');
                
            var metadataPath = Path.Combine(pathToDataFolder, TDR_METADATA_DIR, METADATA_FILENAME);
            IEnumerable<string> lines = File.ReadLines(metadataPath);
            
            foreach (var line in lines)
            {
                if (!line.Contains(clientside_original_filepath))
                    continue;
                return line.Substring(line.LastIndexOf(',') + 1);
            }
            return null;
        }

        /// <summary>
        /// Reads a file's contents using its metadata information.
        /// </summary>
        /// <param name="pathToDataFolder">Base path to the data folder</param>
        /// <param name="meta">Metadata line containing file information</param>
        /// <returns>File contents as a byte array</returns>
        internal static byte[] ReadFile(string pathToDataFolder, Metadata.Line meta) {
            string uuid = GetUuid(pathToDataFolder, meta);
            string path = Path.Combine(pathToDataFolder, COURT_DOCUMENTS_DIR, uuid);
            
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
                
                byte[] data = File.ReadAllBytes(sourcePath);
                File.WriteAllBytes(targetPath, data);
            }
        }

    }

}
