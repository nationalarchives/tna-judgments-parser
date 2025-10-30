
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            byte[] docx = File.ReadAllBytes(path);
            Bundle bundle =  Parse(docx, classifier, languageService);
            stopwatch.Stop();
            Console.WriteLine($"TOTAL TIME: {stopwatch.ElapsedMilliseconds}ms");
            return bundle;
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
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            XmlDocument doc = Builder.Build(bill);
            stopwatch.Stop();
            Console.WriteLine($"BUILDING TIME: {stopwatch.ElapsedMilliseconds}ms");

            stopwatch = new Stopwatch();
            stopwatch.Start();
            Simplifier.Simplify(doc, bill.Styles);
            stopwatch.Stop();
            Console.WriteLine($"SIMPLIFYING TIME: {stopwatch.ElapsedMilliseconds}ms");

            stopwatch = new Stopwatch();
            stopwatch.Start();
            string xml = NationalArchives.Judgments.Api.Parser.SerializeXml(doc);
            stopwatch.Stop();
            Console.WriteLine($"SERIALIZING TIME: {stopwatch.ElapsedMilliseconds}ms");

            return new Bundle { Xml = xml };
        }

    }

}
