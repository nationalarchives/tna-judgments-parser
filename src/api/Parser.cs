
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

using AttachmentPair = System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>;
using ParseFunction = System.Func<byte[], UK.Gov.Legislation.Judgments.IOutsideMetadata, System.Collections.Generic.IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>>, UK.Gov.Legislation.Judgments.AkomaNtoso.ILazyBundle>;

using AttachmentPair1 = System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>;
using ParseFunction1 = System.Func<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.IOutsideMetadata, System.Collections.Generic.IEnumerable<System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>>, UK.Gov.Legislation.Judgments.IJudgment>;

namespace UK.Gov.NationalArchives.Judgments.Api {

public enum Hint { UKSC, EWCA, EWHC, UKUT }

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

        bundle.Close();

        return new Response() {
            Xml = xml,
            Meta = meta2,
            Images = images
        };
    }

    private static ParseFunction EWCAParser = AkN.Parser.MakeParser4(UK.Gov.Legislation.Judgments.Parse.CourtOfAppealParser.Parse3);
    private static ParseFunction UKSCParser = AkN.Parser.MakeParser4(UK.Gov.Legislation.Judgments.Parse.SupremeCourtParser.Parse3);
    private static ParseFunction UKUTParser = AkN.Parser.MakeParser4(UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT.Parser.Parse);

    private static ParseFunction GetParser(Hint? hint) {
        if (!hint.HasValue)
            return Combined;
        if (hint.Value == Hint.UKSC)
            return UKSCParser;
        if (hint.Value == Hint.UKUT)
            return UKUTParser;
        return EWCAParser;
    }

    private static AkN.ILazyBundle Combined(byte[] docx, IOutsideMetadata meta, IEnumerable<System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>> attachments) {
        WordprocessingDocument doc = AkN.Parser.Read(docx);
        IEnumerable<AttachmentPair1> attach2 = AkN.Parser.ConvertAttachments(attachments);
        ParseFunction1 first = UK.Gov.Legislation.Judgments.Parse.CourtOfAppealParser.Parse3;
        List<ParseFunction1> others = new List<ParseFunction1>(2) {
            UK.Gov.Legislation.Judgments.Parse.SupremeCourtParser.Parse3,
            UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT.Parser.Parse
        };
        IJudgment judgment1 = first(doc, meta, attach2);
        if (judgment1.Metadata.Court() is not null || judgment1.Metadata.Cite is not null)
            return new AkN.Bundle(doc, judgment1);
        foreach (var other in others) {
            IJudgment judgment2 = other(doc, meta, attach2);
            if (judgment2.Metadata.Court() is not null || judgment2.Metadata.Cite is not null)
                return new AkN.Bundle(doc, judgment2);
        }
        return new AkN.Bundle(doc, judgment1);
    }

    internal static string SerializeXml(XmlDocument judgment) {
        using MemoryStream memStrm = new MemoryStream();
        AkN.Serializer.Serialize(judgment, memStrm);
        return System.Text.Encoding.UTF8.GetString(memStrm.ToArray());
    }

    internal static Meta ConvertInternalMetadata(UK.Gov.Legislation.Judgments.AkomaNtoso.Meta meta) {
        return new Meta() {
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
        if (string.IsNullOrEmpty(URI.ExtractShortURIComponent(meta.Uri)))
            Logger.LogWarning(@"The judgment's uri is null");
        else
            Logger.LogInformation($"The judgment's uri is { meta.Uri }");
        if (meta.Court is null)
            Logger.LogWarning(@"The court is null");
        else
            Logger.LogInformation($"The court is { meta.Court }");
        if (meta.Cite is null)
            Logger.LogWarning(@"The case citation is null");
        else
            Logger.LogInformation($"The case citation is { meta.Cite }");
        if (meta.Date is null)
            Logger.LogWarning(@"The judgment date is null");
        else
            Logger.LogInformation($"The judgment date is { meta.Date }");
        if (meta.Name is null)
            Logger.LogWarning(@"The case name is null");
        else
            Logger.LogInformation($"The case name is { meta.Name }");
    }

}

}
