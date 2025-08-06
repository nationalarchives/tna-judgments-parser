
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UK.Gov.Legislation.Lawmaker.Api;

namespace UK.Gov.Legislation.Lawmaker
{
    public class Helper
    {

        // Invoked via CLI when running locally
        public static Bundle LocalParse(string path, LegislationClassifier classifier)
        {
            byte[] docx = File.ReadAllBytes(path);
            return Parse(docx, classifier);
        }

        // Invoked via AWS Lambda function handler
        public static Response LambdaParse(Request request, LegislationClassifier classifier)
        {
            byte[] docx = request.Content;
            Bundle bundle = Parse(docx, classifier);
            return new Response()
            {
                Xml = bundle.Xml,
                Images = new List<Image>()
            };
        }


        public static Bundle Parse(byte[] docx, LegislationClassifier classifier)
        {

            Bill bill = LegislationParser.Parse(docx, classifier);
            XmlDocument doc = Builder.Build(bill);
            Simplifier.Simplify(doc, bill.Styles);
            string xml = NationalArchives.Judgments.Api.Parser.SerializeXml(doc);
            return new Bundle { Xml = xml };
        }

    }

}
