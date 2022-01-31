
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.NationalArchives.Judgments.Api {

public enum Hint { SC }

public class Parser {

    public static Response Parse(Request request) {

        Func<Stream, IOutsideMetadata, IEnumerable<Stream>, AkN.ILazyBundle> parse = MakeParser(request.Hint);

        Stream input = new MemoryStream(request.Content);
        IOutsideMetadata meta1 = (request.Meta is null) ? null : new MetaWrapper() { Meta = request.Meta };
        IEnumerable<Stream> attachments = (request.Attachments is null) ? Enumerable.Empty<Stream>() : request.Attachments.Select(a => new MemoryStream(a.Content));

        AkN.ILazyBundle bundle = parse(input, meta1, attachments);

        string xml = SerializeXml(bundle.Judgment);
        AkN.Meta internalMetadata = AkN.MetadataExtractor.Extract(bundle.Judgment);
        Meta meta2 = ConvertInternalMetadata(internalMetadata);
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
            Name = meta.WorkName
        };
    }

    private static Image ConvertImage(IImage image) {
        using var memStream = new MemoryStream();
        image.Content().CopyTo(memStream);
        return new Image() {
            Name = image.Name,
            Type = image.ContentType,
            Content = memStream.ToArray()
        };
    }

}

}
