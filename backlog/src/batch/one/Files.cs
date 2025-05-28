
using System.Collections.Generic;

namespace Backlog.Src.Batch.One
{

    class Files
    {

        private static string GetUuid(string pathToDataFolder, Metadata.Line meta) {
            string clientside_original_filepath = meta.FilePath.Replace(@"JudgmentFiles\", @"data/HMCTS_Judgment_Files/").Replace('\\', '/');
            IEnumerable<string> lines = System.IO.File.ReadLines(pathToDataFolder + @"tdr_metadata\file-metadata.csv");
            foreach (var line in lines)
            {
                if (!line.Contains(clientside_original_filepath))
                    continue;
                return line.Substring(line.LastIndexOf(',') + 1);
            }
            return null;
        }

        internal static byte[] ReadFile(string pathToDataFolder, Metadata.Line meta) {
            string uuid = GetUuid(pathToDataFolder, meta);
            string path = pathToDataFolder + @"court_documents\" + uuid;
            if (meta.Extension == ".doc")
                path += ".docx";
            return System.IO.File.ReadAllBytes(path);
        }

        /* */

        internal static void CopyAllFilesWithExtension(string pathToDataFolder, List<Metadata.Line> lines) {
            foreach (var line in lines)
            {
                string uuid = GetUuid(pathToDataFolder, line);
                string path = pathToDataFolder + @"court_documents\" + uuid;
                byte[] data = System.IO.File.ReadAllBytes(path);
                path += line.Extension.ToLower();
                System.IO.File.WriteAllBytes(path, data);
            }

        }

    }

}
