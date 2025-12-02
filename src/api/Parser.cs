
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.CaseLaw.Parse;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;
using PS = UK.Gov.NationalArchives.CaseLaw.PressSummaries;

using AttachmentPair = System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>;
using ParseFunction = System.Func<byte[], UK.Gov.Legislation.Judgments.IOutsideMetadata, System.Collections.Generic.IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>>, UK.Gov.Legislation.Judgments.AkomaNtoso.ILazyBundle>;

using AttachmentPair1 = System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>;
using OptimizedParseFunction = System.Func<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.NationalArchives.CaseLaw.Parse.WordDocument, UK.Gov.Legislation.Judgments.IOutsideMetadata, System.Collections.Generic.IEnumerable<System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>>, UK.Gov.Legislation.Judgments.Parse.Judgment>;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.Judgments.Api;

public enum Hint { UKSC, EWCA, EWHC, UKUT, Judgment, PressSummary }

public class InvalidAkNException : Exception {

    public InvalidAkNException(ValidationEventArgs cause) : base(cause.Message, cause.Exception) { }
}

public class Parser(ILogger<Parser> logger, AkN.IValidator validator)
{
    /// <exception cref="InvalidAkNException"></exception>
    public Response Parse(Request request) {

        if (request.Filename is not null)
            logger.LogInformation($"parsing { request.Filename }");

        ParseFunction parse = GetParser(request.Hint);

        IOutsideMetadata meta1 = (request.Meta is null) ? null : new MetaWrapper() { Meta = request.Meta };
        IEnumerable<AttachmentPair> attachments = (request.Attachments is null) ? Enumerable.Empty<AttachmentPair>() : request.Attachments.Select(a => ConvertAttachment(a));

        AkN.ILazyBundle bundle = parse(request.Content, meta1, attachments);

        List<ValidationEventArgs> errors = validator.Validate(bundle.Judgment);
        if (errors.Any())
            throw new InvalidAkNException(errors.First());

        string xml = SerializeXml(bundle.Judgment);
        AkN.Meta aknMetadata = AkN.MetadataExtractor.Extract(bundle.Judgment);
        Meta meta2 = ConvertInternalMetadata(aknMetadata);
        Log(meta2);
        List<Image> images = bundle.Images.Select(i => ConvertImage(i)).ToList();

        bundle.Dispose();

        return new Response() {
            Xml = xml,
            Meta = meta2,
            Images = images
        };
    }

    private ParseFunction GetParser(Hint? hint) {
        if (!hint.HasValue)
            return JudgmentOrPressSummary;
        if (hint.Value == Hint.Judgment)
            return ParseAnyJudgment;
        if (hint.Value == Hint.EWHC || hint.Value == Hint.EWCA)
            return Wrap(OptimizedEWHCParser.Parse);
        if (hint.Value == Hint.UKSC)
            return Wrap(OptimizedUKSCParser.Parse);
        if (hint.Value == Hint.UKUT)
            return Wrap(OptimizedUKUTParser.Parse);
        if (hint.Value == Hint.PressSummary)
            return ParsePressSummary;
        throw new Exception("unsupported hint: " + Enum.GetName(typeof(Hint), hint));
    }

    private static ParseFunction Wrap(OptimizedParseFunction f) {
        return (docx, meta, attachments) => {
            WordprocessingDocument doc = AkN.Parser.Read(docx);
            WordDocument preParsed = new PreParser().Parse(doc);
            IEnumerable<AttachmentPair1> attach2 = AkN.Parser.ConvertAttachments(attachments);
            IJudgment judgment = f(doc, preParsed, meta, attach2);
            return new AkN.Bundle(doc, judgment);
        };
    }

    private AkN.ILazyBundle ParseAnyJudgment(byte[] docx, IOutsideMetadata meta, IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>> attachments) {
        WordprocessingDocument doc = AkN.Parser.Read(docx);
        WordDocument preParsed = new PreParser().Parse(doc);
        IJudgment judgment = BestJudgment(preParsed, meta, attachments);
        return new AkN.Bundle(doc, judgment);
    }

    private Judgment BestJudgment(WordDocument preParsed, IOutsideMetadata meta, IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>> attachments) {
        IEnumerable<AttachmentPair1> attach2 = AkN.Parser.ConvertAttachments(attachments);
        OptimizedParseFunction first = OptimizedEWHCParser.Parse;
        List<OptimizedParseFunction> others = new List<OptimizedParseFunction>(2) {
            OptimizedUKSCParser.Parse,
            OptimizedUKUTParser.Parse
        };
        Judgment judgment1 = first(preParsed.Docx, preParsed, meta, attach2);
        int score1 = Score(judgment1);
        if (score1 == PerfectScore)
            return judgment1;
        foreach (var other in others) {
            Judgment judgment2 = other(preParsed.Docx, preParsed, meta, attach2);
            int score2 = Score(judgment2);
            if (score2 == PerfectScore)
                return judgment2;
            if (score2 > score1) {
                judgment1 = judgment2;
                score1 = score2;
            }
        }
        return judgment1;
    }

    private AkN.ILazyBundle JudgmentOrPressSummary(byte[] docx, IOutsideMetadata meta, IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>> attachments) {
        WordprocessingDocument doc = AkN.Parser.Read(docx);
        WordDocument preParsed = new PreParser().Parse(doc);

        Judgment judgment = BestJudgment(preParsed, meta, attachments);
        if (Score(judgment) == PerfectScore)
            return new AkN.Bundle(doc, judgment);

        PS.PressSummary ps = PS.Parser.Parse(preParsed, meta);
        if (ps.InternalMetadata.DocType is not null)
            return new AkN.PSBundle(doc, ps);

        return new AkN.Bundle(doc, judgment);
    }

    private static int PerfectScore = 7;

    private static int Score(Judgment judgment) {
        int score = 0;
        if (judgment.Header is not null && judgment.Header.Any())
            score += 2;
        if (judgment.InternalMetadata.ShortUriComponent is not null)
            score += 1;
        if (judgment.InternalMetadata.Court is not null)
            score += 1;
        if (judgment.InternalMetadata.Cite is not null)
            score += 1;
        if (judgment.InternalMetadata.Date is not null)
            score += 1;
        if (judgment.InternalMetadata.Name is not null)
            score += 1;
        return score;
    }

    private AkN.ILazyBundle ParsePressSummary(byte[] docx, IOutsideMetadata meta, IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>> attachments) {
        WordprocessingDocument doc = AkN.Parser.Read(docx);
        PS.PressSummary ps = PS.Parser.Parse(doc, meta);
        return new AkN.PSBundle(doc, ps);
    }

    /* */

    internal static string SerializeXml(XmlDocument judgment) {
        using MemoryStream memStrm = new MemoryStream();
        AkN.Serializer.Serialize(judgment, memStrm);
        return System.Text.Encoding.UTF8.GetString(memStrm.ToArray());
    }

    internal static Meta ConvertInternalMetadata(UK.Gov.Legislation.Judgments.AkomaNtoso.Meta meta) {
        return new Meta() {
            DocumentType = meta.DocElementName,
            Uri = URI.IsEmpty(meta.WorkUri) ? null : meta.WorkUri,
            Court = meta.UKCourt,
            Cite = meta.UKCite,
            Date = meta.WorkDate,
            Name = meta.WorkName,
            Attachments = meta.ExternalAttachments.Select(a => new ExternalAttachment() { Name = a.ShowAs, Link = a.Href })
        };
    }

    internal static Image ConvertImage(IImage image) {
        return new Image() {
            Name = image.Name,
            Type = image.ContentType,
            Content = image.Read()
        };
    }

    internal static AttachmentPair ConvertAttachment(Attachment a) {
        var content = a.Content;
        var type1 = a.Type;
        UK.Gov.Legislation.Judgments.AttachmentType type2;
        if (type1 == Api.AttachmentType.Order)
            type2 = UK.Gov.Legislation.Judgments.AttachmentType.Order;
        else if (type1 == Api.AttachmentType.Appendix)
            type2 = UK.Gov.Legislation.Judgments.AttachmentType.Appendix;
        else
            throw new System.Exception();
        return new System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>(content, type2);
    }

    internal void Log(Api.Meta meta) {
        if (string.IsNullOrEmpty(meta.DocumentType))
            logger.LogWarning("The document type is null");
        else
            logger.LogInformation("The document type is {}", meta.DocumentType);
        if (string.IsNullOrEmpty(URI.ExtractShortURIComponent(meta.Uri)))
            logger.LogWarning("The {} uri is null", meta.DocumentType);
        else
            logger.LogInformation("The {} uri is {}", meta.DocumentType, meta.Uri);
        if (meta.Court is null)
            logger.LogWarning("The court is null");
        else
            logger.LogInformation("The court is {}", meta.Court);
        if (meta.Cite is null)
            logger.LogWarning("The case citation is null");
        else
            logger.LogInformation("The case citation is {}", meta.Cite);
        if (meta.Date is null)
            logger.LogWarning("The {} date is null", meta.DocumentType);
        else
            logger.LogInformation("The {} date is {}", meta.DocumentType, meta.Date);
        if (meta.Name is null)
            logger.LogWarning("The {} name is null", meta.DocumentType);
        else
            logger.LogInformation("The {} name is {}", meta.DocumentType, meta.Name);
    }

}
