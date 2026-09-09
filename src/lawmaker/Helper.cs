
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Lawmaker.Api;

using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;
using WordprocessingDocument = DocumentFormat.OpenXml.Packaging.WordprocessingDocument;

namespace UK.Gov.Legislation.Lawmaker;

public class Helper
{

    // Invoked via CLI when running locally
    public static Response LocalParse(string path, LegislationClassifier classifier, LanguageService languageService)
    {
        var docx = File.ReadAllBytes(path);
        return Parse(docx, classifier, languageService);
    }

    // Invoked via AWS Lambda function handler
    public static Response LambdaParse(Request request, LegislationClassifier classifier, LanguageService languageService)
    {
        var docx = request.Content;
        return Parse(docx, classifier, languageService);
    }

    // TODO: Both LocalParse and LambdaParse call this method.
    // Need to ensure that Images is populated, rather than an empty list.
    public static Response Parse(byte[] docx, LegislationClassifier classifier, LanguageService languageService)
    {
        WordprocessingDocument wordDoc = AkN.Parser.Read(docx);
        Document bill;
        try
        {
            bill = LegislationParser.Parse(wordDoc, classifier, languageService);
        }
        catch (BlockParsingException ex)
        {
            return new Response
            {
                Error = new ParseError
                {
                    BlockNumber = ex.BlockNumber,
                    BlockText = ex.BlockText,
                    Message = ex.InnerException?.Message ?? ex.Message
                }
            };
        }
        XmlDocument doc = Builder.Build(bill, languageService);
        Simplifier.Simplify(doc, bill.Styles);
        var xml = NationalArchives.Judgments.Api.Parser.SerializeXml(doc);
        IEnumerable<IImage> images = WImage.Get(wordDoc).ToArray();
        return new Response
        {
            Xml = xml,
            Images = images.Select(ConvertImage).ToList()
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
