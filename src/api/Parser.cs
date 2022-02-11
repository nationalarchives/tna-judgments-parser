
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.NationalArchives.Judgments.Api {

public enum Hint { SC }

public class Parser {

    private static ILogger Logger = Logging.Factory.CreateLogger<Parser>();

    public static Response Parse(Request request) {

        if (request.Filename is not null)
            Logger.LogInformation($"parsing { request.Filename }");

        Func<Stream, IOutsideMetadata, IEnumerable<Stream>, AkN.ILazyBundle> parse = MakeParser(request.Hint);

        Stream input = new MemoryStream(request.Content);
        IOutsideMetadata meta1 = (request.Meta is null) ? null : new MetaWrapper() { Meta = request.Meta };
        IEnumerable<Stream> attachments = (request.Attachments is null) ? Enumerable.Empty<Stream>() : request.Attachments.Select(a => new MemoryStream(a.Content));

        AkN.ILazyBundle bundle = parse(input, meta1, attachments);

        string xml = SerializeXml(bundle.Judgment);
        AkN.Meta internalMetadata = AkN.MetadataExtractor.Extract(bundle.Judgment);
        Meta meta2 = ConvertInternalMetadata(internalMetadata);
        Log(meta2);
        List<Image> images = bundle.Images.Select(i => ConvertImage(i)).ToList();

        bundle.Close();
        input.Close();

        return new Response() {
            Xml = xml,
            Meta = meta2,
            Images = images
        };
    }

    private static Func<Stream, IOutsideMetadata, IEnumerable<Stream>, AkN.ILazyBundle> MakeParser(Hint? hint) {
        Func<WordprocessingDocument, IOutsideMetadata, IEnumerable<WordprocessingDocument>, IJudgment> parse;
        if (!hint.HasValue)
            parse = UK.Gov.Legislation.Judgments.Parse.CourtOfAppealParser.Parse3;
        else if (hint.Value == Hint.SC)
            parse = UK.Gov.Legislation.Judgments.Parse.SupremeCourtParser.Parse3;
        else
            parse = UK.Gov.Legislation.Judgments.Parse.CourtOfAppealParser.Parse3;
        return UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.MakeParser3(parse);
    }

    internal static string SerializeXml(XmlDocument judgment) {
        using MemoryStream memStrm = new MemoryStream();
        AkN.Serializer.Serialize(judgment, memStrm);
        return System.Text.Encoding.UTF8.GetString(memStrm.ToArray());
    }

    internal static Meta ConvertInternalMetadata(UK.Gov.Legislation.Judgments.AkomaNtoso.Meta meta) {
        return new Meta() {
            Uri = meta.WorkUri,
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

    internal static void Log(Api.Meta meta) {
        if (meta.Uri is null)
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
