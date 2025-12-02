
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UK.Gov.Legislation.Lawmaker.Api;

namespace UK.Gov.Legislation.Lawmaker
{
    public class Helper
    {

        // Invoked via CLI when running locally
        public static Bundle LocalParse(string path, LegislationClassifier classifier, LanguageService languageService)
        {
            byte[] docx = File.ReadAllBytes(path);
            return Parse(docx, classifier, languageService);
        }

        // Invoked via AWS Lambda function handler
        public static Response LambdaParse(Request request, LegislationClassifier classifier, LanguageService languageService)
        {
            byte[] docx = request.Content;
            Bundle bundle = Parse(docx, classifier, languageService);
            return new Response()
            {
                Xml = bundle.Xml,
                Images = new List<Image>()
            };
        }


        public static Bundle Parse(byte[] docx, LegislationClassifier classifier, LanguageService languageService)
        {

            Document bill = LegislationParser.Parse(docx, classifier, languageService);
            XmlDocument doc = Builder.Build(bill, languageService);
            Simplifier.Simplify(doc, bill.Styles);
            string xml = NationalArchives.Judgments.Api.Parser.SerializeXml(doc);
            return new Bundle { Xml = xml };
        }

    }

}
