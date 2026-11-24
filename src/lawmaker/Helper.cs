
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Lawmaker.Api;

using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;
using WordprocessingDocument = DocumentFormat.OpenXml.Packaging.WordprocessingDocument;

namespace UK.Gov.Legislation.Lawmaker;

public class Helper
{

    private static readonly ILogger Logger = Logging.Factory.CreateLogger<Helper>();

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
        try
        {
            WordprocessingDocument wordDoc = ParseStageException.Wrap(ParseStageException.ReadingDocument, () => AkN.Parser.Read(docx));
            Document bill = LegislationParser.Parse(wordDoc, classifier, languageService);
            XmlDocument doc = ParseStageException.Wrap(ParseStageException.BuildingXml, () => Builder.Build(bill, languageService));
            ParseStageException.Wrap(ParseStageException.SimplifyingXml, () => Simplifier.Simplify(doc, bill.Styles));
            var xml = ParseStageException.Wrap(ParseStageException.SerializingXml, () => AkN.Serializer.SerializeToString(doc));
            IEnumerable<IImage> images = ParseStageException.Wrap(ParseStageException.ExtractingImages, () => WImage.Get(wordDoc).ToArray());
            return new Response
            {
                Xml = xml,
                Images = images.Select(ConvertImage).ToList()
            };
        }
        catch (BlockParsingException ex)
        {
            Logger.LogError(ex.InnerException ?? ex, "parsing failed at block {}", ex.BlockNumber);
            return ErrorResponse(ParseStageException.ParsingStructure, ex.BlockNumber, ex.BlockText, ex.InnerException ?? ex);
        }
        catch (ParseStageException ex)
        {
            Logger.LogError(ex.InnerException ?? ex, "parsing failed while {}", ex.Stage);
            return ErrorResponse(ex.Stage, ex.BlockNumber, ex.BlockText, ex.InnerException ?? ex);
        }
    }

    private static Response ErrorResponse(string stage, int? blockNumber, string blockText, Exception cause)
    {
        return new Response
        {
            Error = new ParseError
            {
                Stage = stage,
                BlockNumber = blockNumber,
                BlockText = blockText,
                Message = $"{cause.GetType().Name}: {cause.Message}"
            }
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
