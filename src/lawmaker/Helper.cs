
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UK.Gov.Legislation.Lawmaker.Api;

namespace UK.Gov.Legislation.Lawmaker
{

    public enum Hint { Bill }

    public class Helper
    {

        // Invoked via CLI when running locally
        public static Bundle LocalParse(string path)
        {
            byte[] docx = File.ReadAllBytes(path);
            return Parse(docx);
        }

        // Invoked via AWS Lambda function handler
        public static Response LambdaParse(Request request)
        {
            byte[] docx = request.Content;
            Bundle bundle = Parse(docx);
            return new Response()
            {
                Xml = bundle.Xml,
                Images = new List<Image>()
            };
        }


        public static Bundle Parse(byte[] docx)
        {
            Bill bill = BillParser.Parse(docx);
            XmlDocument doc = Builder.Build(bill);
            Simplifier.Simplify(doc, bill.Styles);
            string xml = NationalArchives.Judgments.Api.Parser.SerializeXml(doc);
            return new Bundle { Xml = xml };
        }

    }

}
