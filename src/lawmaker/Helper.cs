
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Lawmaker.Api;

namespace UK.Gov.Legislation.Lawmaker
{
    public class Helper
    {

        // Invoked via CLI when running locally
        public static Response LocalParse(string path, LegislationClassifier classifier, LanguageService languageService)
        {
            byte[] docx = File.ReadAllBytes(path);
            return Parse(docx, classifier, languageService);
        }

        // Invoked via AWS Lambda function handler
        public static Response LambdaParse(Request request, LegislationClassifier classifier, LanguageService languageService)
        {
            byte[] docx = request.Content;
            return Parse(docx, classifier, languageService);
        }

        // TODO: Both LocalParse and LambdaParse call this method.
        // Need to ensure that Images is populated, rather than an empty list.
        public static Response Parse(byte[] docx, LegislationClassifier classifier, LanguageService languageService)
        {
            Document bill = LegislationParser.Parse(docx, classifier, languageService);
            XmlDocument doc = Builder.Build(bill, languageService);
            Simplifier.Simplify(doc, bill.Styles);
            string xml = NationalArchives.Judgments.Api.Parser.SerializeXml(doc);
            IEnumerable<IImage> images = [];
            return new Response { 
                Xml = xml,
                Images = images.Select(i => ConvertImage(i)).ToList()
            };
        }

        public static Image ConvertImage(IImage image)
        {
            return new Image()
            {
                Name = image.Name,
                Type = image.ContentType,
                Content = image.Read()
            };
        }

    }

}
