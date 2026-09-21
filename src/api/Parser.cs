using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;

using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;
using AttachmentPair = System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>;
using OptimizedParseFunction = System.Func<DocumentFormat.OpenXml.Packaging.WordprocessingDocument,
    UK.Gov.NationalArchives.CaseLaw.Parse.WordDocument, UK.Gov.Legislation.Judgments.IOutsideMetadata, System.Collections.Generic.IEnumerable<System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument,
        UK.Gov.Legislation.Judgments.AttachmentType>>, UK.Gov.Legislation.Judgments.Parse.Judgment>;
using ParseFunction = System.Func<byte[], UK.Gov.Legislation.Judgments.IOutsideMetadata, System.Collections.Generic.IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>>,
    UK.Gov.Legislation.Judgments.AkomaNtoso.ILazyBundle>;
using PS = UK.Gov.NationalArchives.CaseLaw.PressSummaries;

namespace UK.Gov.NationalArchives.Judgments.Api;

public enum Hint { UKSC, EWCA, EWHC, UKUT, Judgment, PressSummary }

public class InvalidAkNException(ValidationEventArgs cause) : Exception(cause.Message, cause.Exception);

public interface IParser
{
    Response Parse(Request request);
}

public class Parser(ILogger<Parser> logger, AkN.IValidator validator) : IParser
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

        var bundle = parse(request.Content, meta1, attachments);

        var errors = validator.Validate(bundle.Judgment);
        if (errors.Any())
        {
            throw new InvalidAkNException(errors.First());
        }

        var xml = SerializeXml(bundle.Judgment);
        var aknMetadata = AkN.MetadataExtractor.Extract(bundle.Judgment);
        var meta2 = ConvertInternalMetadata(aknMetadata);
        Log(meta2);
        var images = bundle.Images.Select(ConvertImage).ToList();

        bundle.Dispose();

        return new Response
        {
            Xml = xml,
            Meta = meta2,
            Images = images
        };
    }

    private ParseFunction GetParser(Hint? hint)
    {
        return hint switch
        {
            null => JudgmentOrPressSummary,
            Hint.Judgment => ParseAnyJudgment,
            Hint.EWHC or Hint.EWCA => Wrap(OptimizedEWHCParser.Parse),
            Hint.UKSC => Wrap(OptimizedUKSCParser.Parse),
            Hint.UKUT => Wrap(OptimizedUKUTParser.Parse),
            Hint.PressSummary => ParsePressSummary,
            _ => throw new ArgumentOutOfRangeException(nameof(hint), hint, "unsupported hint")
        };
    }

    private static ParseFunction Wrap(OptimizedParseFunction f)
    {
        return (docx, meta, attachments) =>
        {
            var doc = AkN.Parser.Read(docx);
            var preParsed = new PreParser().Parse(doc);
            var attach2 = AkN.Parser.ConvertAttachments(attachments);
            IJudgment judgment = f(doc, preParsed, meta, attach2);

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
        IEnumerable<Tuple<byte[], Legislation.Judgments.AttachmentType>> attachments)
    {
        var doc = AkN.Parser.Read(docx);
        var preParsed = new PreParser().Parse(doc);
        IJudgment judgment = BestJudgment(preParsed, meta, attachments);
        return new AkN.Bundle(doc, judgment);
    }

    private Judgment BestJudgment(WordDocument preParsed, IOutsideMetadata meta,
        IEnumerable<Tuple<byte[], Legislation.Judgments.AttachmentType>> attachments)
    {
        var attach2 = AkN.Parser.ConvertAttachments(attachments);
        OptimizedParseFunction first = OptimizedEWHCParser.Parse;
        var others = new List<OptimizedParseFunction>(2) { OptimizedUKSCParser.Parse, OptimizedUKUTParser.Parse };
        var judgment1 = first(preParsed.Docx, preParsed, meta, attach2);
        var score1 = Score(judgment1);
        if (score1 == PerfectScore)
        {
            return judgment1;
        }

        foreach (var other in others)
        {
            var judgment2 = other(preParsed.Docx, preParsed, meta, attach2);
            var score2 = Score(judgment2);
            if (score2 == PerfectScore)
            {
                return judgment2;
            }

            if (score2 > score1)
            {
                judgment1 = judgment2;
                score1 = score2;
            }
        }

        return judgment1;
    }

    private AkN.ILazyBundle JudgmentOrPressSummary(byte[] docx, IOutsideMetadata meta,
        IEnumerable<Tuple<byte[], Legislation.Judgments.AttachmentType>> attachments)
    {
        var doc = AkN.Parser.Read(docx);
        var preParsed = new PreParser().Parse(doc);

        var judgment = BestJudgment(preParsed, meta, attachments);
        if (Score(judgment) == PerfectScore)
        {
            return new AkN.Bundle(doc, judgment);
        }

        var ps = PS.Parser.Parse(preParsed, meta);
        if (ps.InternalMetadata.DocType is not null)
        {
            return new AkN.PSBundle(doc, ps);
        }

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
        var doc = AkN.Parser.Read(docx);
        var ps = PS.Parser.Parse(doc, meta);
        return new AkN.PSBundle(doc, ps);
    }

    internal static string SerializeXml(XmlDocument judgment)
    {
        using var memStrm = new MemoryStream();
        AkN.Serializer.Serialize(judgment, memStrm);
        return Encoding.UTF8.GetString(memStrm.ToArray());
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
            Attachments =
                meta.ExternalAttachments.Select(a => new ExternalAttachment { Name = a.ShowAs, Link = a.Href })
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

    internal void Log(Meta meta)
    {
        if (string.IsNullOrEmpty(meta.DocumentType))
        {
            logger.LogWarning("The document type is null");
        }
        else
        {
            logger.LogInformation("The document type is {DocumentType}", meta.DocumentType);
        }

        if (string.IsNullOrEmpty(URI.ExtractShortURIComponent(meta.Uri)))
        {
            logger.LogWarning("The {DocumentType} uri is null", meta.DocumentType);
        }
        else
        {
            logger.LogInformation("The {DocumentType} uri is {Uri}", meta.DocumentType, meta.Uri);
        }

        if (meta.Court is null)
        {
            logger.LogWarning("The court is null");
        }
        else
        {
            logger.LogInformation("The court is {Court}", meta.Court);
        }

        if (meta.Cite is null)
        {
            logger.LogWarning("The case citation is null");
        }
        else
        {
            logger.LogInformation("The case citation is {Cite}", meta.Cite);
        }

        if (meta.Date is null)
        {
            logger.LogWarning("The {DocumentType} date is null", meta.DocumentType);
        }
        else
        {
            logger.LogInformation("The {DocumentType} date is {Date}", meta.DocumentType, meta.Date);
        }

        if (meta.Name is null)
        {
            logger.LogWarning("The {DocumentType} name is null", meta.DocumentType);
        }
        else
        {
            logger.LogInformation("The {DocumentType} name is {Name}", meta.DocumentType, meta.Name);
        }
    }
}
