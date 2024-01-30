
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
using OptimizedParseFunction = System.Func<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.NationalArchives.CaseLaw.Parse.WordDocument, UK.Gov.Legislation.Judgments.IOutsideMetadata, System.Collections.Generic.IEnumerable<System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>>, UK.Gov.Legislation.Judgments.IJudgment>;

namespace UK.Gov.NationalArchives.Judgments.Api {

public enum Hint { UKSC, EWCA, EWHC, UKUT, Judgment, PressSummary }

public class InvalidAkNException : System.Exception {

    public InvalidAkNException(ValidationEventArgs cause) : base(cause.Message, cause.Exception) { }
}

public class Parser {

    private static ILogger Logger = Logging.Factory.CreateLogger<Parser>();
    private static AkN.Validator validator = new AkN.Validator();

    /// <exception cref="InvalidAkNException"></exception>
    public static Response Parse(Request request) {

        if (request.Filename is not null)
            Logger.LogInformation($"parsing { request.Filename }");

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

    private static ParseFunction GetParser(Hint? hint) {
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
        return (byte[] docx, IOutsideMetadata meta, IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>> attachments) => {
            WordprocessingDocument doc = AkN.Parser.Read(docx);
            WordDocument preParsed = new PreParser().Parse(doc);
            IEnumerable<AttachmentPair1> attach2 = AkN.Parser.ConvertAttachments(attachments);
            IJudgment judgment = f(doc, preParsed, meta, attach2);
            return new AkN.Bundle(doc, judgment);
        };
    }

    private static AkN.ILazyBundle ParseAnyJudgment(byte[] docx, IOutsideMetadata meta, IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>> attachments) {
        WordprocessingDocument doc = AkN.Parser.Read(docx);
        WordDocument preParsed = new PreParser().Parse(doc);
        IJudgment judgment = BestJudgment(preParsed, meta, attachments);
        return new AkN.Bundle(doc, judgment);
    }

    private static IJudgment BestJudgment(WordDocument preParsed, IOutsideMetadata meta, IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>> attachments) {
        IEnumerable<AttachmentPair1> attach2 = AkN.Parser.ConvertAttachments(attachments);
        OptimizedParseFunction first = OptimizedEWHCParser.Parse;
        List<OptimizedParseFunction> others = new List<OptimizedParseFunction>(2) {
            OptimizedUKSCParser.Parse,
            OptimizedUKUTParser.Parse
        };
        IJudgment judgment1 = first(preParsed.Docx, preParsed, meta, attach2);
        int score1 = Score(judgment1);
        if (score1 == PerfectScore)
            return judgment1;
        foreach (var other in others) {
            IJudgment judgment2 = other(preParsed.Docx, preParsed, meta, attach2);
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

    private static AkN.ILazyBundle JudgmentOrPressSummary(byte[] docx, IOutsideMetadata meta, IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>> attachments) {
        WordprocessingDocument doc = AkN.Parser.Read(docx);
        WordDocument preParsed = new PreParser().Parse(doc);

        IJudgment judgment = BestJudgment(preParsed, meta, attachments);
        if (Score(judgment) == PerfectScore)
            return new AkN.Bundle(doc, judgment);

        PS.PressSummary ps = PS.Parser.Parse(preParsed);
        if (ps.Metadata.DocType is not null)
            return new AkN.PSBundle(doc, ps);

        return new AkN.Bundle(doc, judgment);
    }

    private static int PerfectScore = 7;

    private static int Score(IJudgment judgment) {
        int score = 0;
        if (judgment.Header is not null && judgment.Header.Any())
            score += 2;
        if (judgment.Metadata.ShortUriComponent is not null)
            score += 1;
        if (judgment.Metadata.Court() is not null)
            score += 1;
        if (judgment.Metadata.Cite is not null)
            score += 1;
        if (judgment.Metadata.Date is not null)
            score += 1;
        if (judgment.Metadata.Name is not null)
            score += 1;
        return score;
    }

    private static AkN.ILazyBundle ParsePressSummary(byte[] docx, IOutsideMetadata meta, IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>> attachments) {
        WordprocessingDocument doc = AkN.Parser.Read(docx);
        PS.PressSummary ps = PS.Parser.Parse(doc);
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
        using var memStream = new MemoryStream();
        image.Content().CopyTo(memStream);
        return new Image() {
            Name = image.Name,
            Type = image.ContentType,
            Content = memStream.ToArray()
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

    internal static void Log(Api.Meta meta) {
        if (string.IsNullOrEmpty(meta.DocumentType))
            Logger.LogWarning("The document type is null");
        else
            Logger.LogInformation("The document type is {}", meta.DocumentType);
        if (string.IsNullOrEmpty(URI.ExtractShortURIComponent(meta.Uri)))
            Logger.LogWarning("The {} uri is null", meta.DocumentType);
        else
            Logger.LogInformation("The {} uri is {}", meta.DocumentType, meta.Uri);
        if (meta.Court is null)
            Logger.LogWarning("The court is null");
        else
            Logger.LogInformation("The court is {}", meta.Court);
        if (meta.Cite is null)
            Logger.LogWarning("The case citation is null");
        else
            Logger.LogInformation("The case citation is {}", meta.Cite);
        if (meta.Date is null)
            Logger.LogWarning("The {} date is null", meta.DocumentType);
        else
            Logger.LogInformation("The {} date is {}", meta.DocumentType, meta.Date);
        if (meta.Name is null)
            Logger.LogWarning("The {} name is null", meta.DocumentType);
        else
            Logger.LogInformation("The {} name is {}", meta.DocumentType, meta.Name);
    }

}

}
