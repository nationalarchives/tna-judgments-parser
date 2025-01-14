
using System.Linq;

namespace Backlog.Src.Batch.One
{

    class Files
    {
        static readonly string Root = @"C:\Users\Administrator\TDR-2024-CG6F\data\JudgmentFiles\";

        internal static string GetPdf(string id) {
            var dir = Root + "j" + id + @"\";
            var files = System.IO.Directory.GetFiles(dir);
            if (files.Where(file => file.EndsWith(".docx")).Any())
                return null;
            var pdfs = files.Where(file => file.EndsWith(".pdf"));
            var count = pdfs.Count();
            if (count == 0)
                return null;
            if (count > 1)
                throw new System.Exception();
            return pdfs.First();
        }

    }

}
