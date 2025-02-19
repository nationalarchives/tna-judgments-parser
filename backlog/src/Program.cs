
using System.IO;
using System.Security;
using Backlog.Src.Batch.One;
using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog.Src
{

    class Program
    {

        static int Main(string[] args)
        {
//             uint id = 143;
//             uint bulkNum = BulkNumbers.Next(id);
//             byte[] bundle = Metadata.FindLineAndMakeBundle(id, bulkNum);
//             string key = "bulk" + bulkNum + ".tar.gz";
// //            Bucket.UploadBundle(key, bundle);
//             BulkNumbers.Save(id, bulkNum);
//             return 0;

            uint id = 3;
            ExtendedMetadata meta = Metadata.GetMetadata(id, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            var file = Files.GetDocx(id.ToString());
            var docx = File.ReadAllBytes(file);
            Api.Meta meta2 = new()
            {
                DocumentType = "decision",
                Uri = null,
                Court = meta.Court?.Code,
                Cite = null,
                Date = meta.Date?.Date,
                Name = meta.Name,
                Extensions = new() {
                    SourceFormat = meta.SourceFormat,
                    CaseNumbers = meta.CaseNumbers,
                    Parties = meta.Parties,
                    Categories = meta.Categories
                },
                Attachments = []
            };
            Api.Request request = new() {
                Meta = meta2,
                Content = docx
            };
            Api.Response response = Api.Parser.Parse(request);
            System.Console.WriteLine(response.Xml);
            Files.SaveXml(id.ToString(), response.Xml);
            return 0;
        }

    }

}
