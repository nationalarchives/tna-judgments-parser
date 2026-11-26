using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Xml.Schema;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;

using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;
using AttachmentPair = System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>;
using OptimizedParseFunction = System.Func<DocumentFormat.OpenXml.Packaging.WordprocessingDocument,
    UK.Gov.NationalArchives.CaseLaw.Parse.WordDocument, UK.Gov.Legislation.Judgments.IOutsideMetadata, System.
    Collections.Generic.IEnumerable<System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument,
        UK.Gov.Legislation.Judgments.AttachmentType>>, System.TimeProvider,
    UK.Gov.Legislation.Judgments.Parse.Judgment>;
using ParseFunction =
    System.Func<byte[], UK.Gov.Legislation.Judgments.IOutsideMetadata, System.Collections.Generic.IEnumerable<
            System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>>,
        UK.Gov.Legislation.Judgments.AkomaNtoso.ILazyBundle>;
using PS = UK.Gov.NationalArchives.CaseLaw.PressSummaries;

namespace UK.Gov.NationalArchives.Judgments.Api;

public enum Hint { UKSC, EWCA, EWHC, UKUT, Judgment, PressSummary }

public class InvalidAkNException(ValidationEventArgs cause) : Exception(cause.Message, cause.Exception);

public interface IParser
{
    Response Parse(Request request);
}

public class Parser(ILogger<Parser> logger, AkN.IValidator validator, TimeProvider timeProvider) : IParser
{
    /// <exception cref="InvalidAkNException"></exception>
    public Response Parse(Request request)
    {
        if (request.Filename is not null)
        {
            logger.LogInformation("parsing {RequestFilename}", request.Filename);
        }

        var parse = GetParser(request.Hint);

        IOutsideMetadata meta1 = request.Meta is null ? null : new MetaWrapper { Meta = request.Meta };
        var attachments = request.Attachments is null
            ? Enumerable.Empty<AttachmentPair>()
            : request.Attachments.Select(a => new AttachmentPair(a.Content, MapAttachmentType(a.Type)));

        logger.LogInformation("[STAGE] Parsing document ({Hint})", request.Hint?.ToString() ?? "no hint");
        var bundle = parse(request.Content, meta1, attachments);

        logger.LogInformation("[STAGE] Validating akoma ntoso");
        var errors = validator.Validate(bundle.Judgment);
        if (errors.Any())
        {
            throw new InvalidAkNException(errors.First());
        }

        logger.LogInformation("[STAGE] Serializing xml");
        var xml = AkN.Serializer.SerializeToString(bundle.Judgment);

        logger.LogInformation("[STAGE] Extracting metadata");
        var aknMetadata = AkN.MetadataExtractor.Extract(bundle.Judgment);
        var meta2 = ConvertInternalMetadata(aknMetadata);
        logger.LogInformation("Extracted metadata is: {ExtractedMetadata}", JsonSerializer.Serialize(meta2));

        logger.LogInformation("[STAGE] Converting images");
        var images = bundle.Images.Select(ConvertImage).ToList();

        bundle.Dispose();

        logger.LogInformation("[STAGE] Returning response");
        return new Response
        {
            Xml = xml,
            Meta = meta2,
            Images = images,
            StructuredMetadataFields = bundle.StructuredMetadataFields
        };
    }

    private ParseFunction GetParser(Hint? hint)
    {
        return hint switch
        {
            null => JudgmentOrPressSummary,
            Hint.Judgment => ParseAnyJudgment,
            Hint.EWHC or Hint.EWCA => Wrap("EWHC", OptimizedEWHCParser.Parse),
            Hint.UKSC => Wrap("UKSC", OptimizedUKSCParser.Parse),
            Hint.UKUT => Wrap("UKUT", OptimizedUKUTParser.Parse),
            Hint.PressSummary => ParsePressSummary,
            _ => throw new ArgumentOutOfRangeException(nameof(hint), hint, "unsupported hint")
        };
    }

    private ParseFunction Wrap(string parserName, OptimizedParseFunction f)
    {
        return (docx, meta, attachments) =>
        {
            logger.LogInformation("[STAGE] Reading docx");
            var doc = AkN.Parser.Read(docx);

            logger.LogInformation("[STAGE] Pre-parsing");
            var preParsed = new PreParser().Parse(doc);
            var attach2 = AkN.Parser.ConvertAttachments(attachments);

            logger.LogInformation("using {Parser} parser (hint)", parserName);
            IJudgment judgment = f(doc, preParsed, meta, attach2, timeProvider);

            //** HACK - suppress misidentified parties by calling `CaseName.Extract` **
            // For KBD and QBD cases, the king/queen and "on the application of" pieces of text are misidentified as
            // their own parties.
            // On a clean parse of the document, we create a case name which uses these misidentified parties to decide
            // whether or not to add "(R on the application of)" to the case name and then "suppresses" these
            // misidentified parties so they are not output in the xml.
            // On a reparse of the document we are supplied with a case name via the outside metadata so we don't
            // attempt to generate one. This means that the misidentified king/queen and "on the application of" parties
            // are never suppressed on reparse and so incorrectly end up in the final xml.
            // This hack fixes this issue by ensuring that the case name generation (and thus the misidentified party
            // suppression) is always called.
            _ = CaseName.Extract(judgment);

            return new AkN.Bundle(doc, judgment);
        };
    }

    private AkN.ILazyBundle ParseAnyJudgment(byte[] docx, IOutsideMetadata meta,
        IEnumerable<AttachmentPair> attachments)
    {
        logger.LogInformation("[STAGE] Reading docx");
        var doc = AkN.Parser.Read(docx);

        logger.LogInformation("[STAGE] Pre-parsing");
        var preParsed = new PreParser().Parse(doc);

        logger.LogInformation("[STAGE] Choosing best judgment parse");
        IJudgment judgment = BestJudgment(preParsed, meta, attachments);
        return new AkN.Bundle(doc, judgment);
    }

    private Judgment BestJudgment(WordDocument preParsed, IOutsideMetadata meta,
        IEnumerable<AttachmentPair> attachments)
    {
        var attach2 = AkN.Parser.ConvertAttachments(attachments).ToArray();

        logger.LogInformation("trying EWHC parser");
        var bestJudgment = OptimizedEWHCParser.Parse(preParsed.Docx, preParsed, meta, attach2, timeProvider);
        var bestParserScore = Score(bestJudgment);
        var bestParserName = "EWHC";

        logger.LogInformation("EWHC parser scored {Score} of {PerfectScore}", bestParserScore, PerfectScore);
        if (bestParserScore == PerfectScore)
        {
            logger.LogInformation("using EWHC parser (perfect score)");
            return bestJudgment;
        }

        foreach (var (parserName, parserFunction) in new (string, OptimizedParseFunction)[]
                 {
                     ("UKSC", OptimizedUKSCParser.Parse),
                     ("UKUT", OptimizedUKUTParser.Parse)
                 })
        {
            logger.LogInformation("trying {ParserName} parser", parserName);
            var judgment = parserFunction(preParsed.Docx, preParsed, meta, attach2, timeProvider);
            var score = Score(judgment);

            logger.LogInformation("{ParserName} parser scored {Score} of {PerfectScore}", parserName, score, PerfectScore);
            if (score == PerfectScore)
            {
                logger.LogInformation("using {ParserName} parser (perfect score)", parserName);
                return judgment;
            }

            if (score > bestParserScore)
            {
                bestJudgment = judgment;
                bestParserScore = score;
                bestParserName = parserName;
            }
        }

        logger.LogInformation("using {ParserName} parser (best score {Score} of {PerfectScore})", bestParserName,
            bestParserScore, PerfectScore);
        return bestJudgment;
    }

    private AkN.ILazyBundle JudgmentOrPressSummary(byte[] docx, IOutsideMetadata meta,
        IEnumerable<AttachmentPair> attachments)
    {
        var doc = AkN.Parser.Read(docx);
        var preParsed = new PreParser().Parse(doc);

        var judgment = BestJudgment(preParsed, meta, attachments);
        if (Score(judgment) == PerfectScore)
        {
            return new AkN.Bundle(doc, judgment);
        }

        logger.LogInformation("no perfect judgment parse, trying press summary parser");
        var ps = PS.Parser.Parse(preParsed, meta);
        if (ps.InternalMetadata.DocType is not null)
        {
            logger.LogInformation("using press summary parser (doc type {DocType})", ps.InternalMetadata.DocType);
            return new AkN.PSBundle(doc, ps);
        }

        logger.LogInformation("press summary parser found no doc type, keeping best judgment parse");
        return new AkN.Bundle(doc, judgment);
    }

    private static readonly int PerfectScore = 7;

    private static int Score(Judgment judgment)
    {
        var score = 0;
        if (judgment.Header is not null && judgment.Header.Any())
        {
            score += 2;
        }

        if (judgment.InternalMetadata.ShortUriComponent is not null)
        {
            score += 1;
        }

        if (judgment.InternalMetadata.Court is not null)
        {
            score += 1;
        }

        if (judgment.InternalMetadata.Cite is not null)
        {
            score += 1;
        }

        if (judgment.InternalMetadata.Date is not null)
        {
            score += 1;
        }

        if (judgment.InternalMetadata.Name is not null)
        {
            score += 1;
        }

        return score;
    }

    private AkN.ILazyBundle ParsePressSummary(byte[] docx, IOutsideMetadata meta,
        IEnumerable<Tuple<byte[], Legislation.Judgments.AttachmentType>> attachments)
    {
        logger.LogInformation("using press summary parser (hint)");
        var doc = AkN.Parser.Read(docx);
        var ps = PS.Parser.Parse(doc, meta);
        return new AkN.PSBundle(doc, ps);
    }

    internal static Meta ConvertInternalMetadata(AkN.Meta meta)
    {
        return new Meta
        {
            DocumentType = meta.DocElementName,
            Uri = URI.IsEmpty(meta.WorkUri) ? null : meta.WorkUri,
            Court = meta.UKCourt,
            Cite = meta.UKCite,
            Date = meta.WorkDate,
            Name = meta.WorkName,
            Attachments = meta.ExternalAttachments.Select(a => new ExternalAttachment
            {
                Name = a.ShowAs,
                Link = a.Href
            })
        };
    }

    internal static Image ConvertImage(IImage image)
    {
        return new Image
        {
            Name = image.Name,
            Type = image.ContentType,
            Content = image.Read()
        };
    }

    private static Legislation.Judgments.AttachmentType MapAttachmentType(AttachmentType attachmentType)
    {
        return attachmentType switch
        {
            AttachmentType.Order => Legislation.Judgments.AttachmentType.Order,
            AttachmentType.Appendix => Legislation.Judgments.AttachmentType.Appendix,
            _ => throw new ArgumentOutOfRangeException(nameof(attachmentType), attachmentType, null)
        };
    }
}
