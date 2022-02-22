
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

using ParseFunction = System.Func<byte[], UK.Gov.Legislation.Judgments.IOutsideMetadata, System.Collections.Generic.IEnumerable<byte[]>, UK.Gov.Legislation.Judgments.AkomaNtoso.ILazyBundle>;

namespace UK.Gov.NationalArchives.Judgments.Api {

public enum Hint { UKSC, EWCA, EWHC }

public class Parser {

    private static ILogger Logger = Logging.Factory.CreateLogger<Parser>();

    public static Response Parse(Request request) {

        if (request.Filename is not null)
            Logger.LogInformation($"parsing { request.Filename }");

        // if (request.Meta?.Uri is not null && !URI.IsValidURIOrComponent(request.Meta.Uri))
        //     throw new System.Exception();

        ParseFunction parse = GetParser(request.Hint);

        IOutsideMetadata meta1 = (request.Meta is null) ? null : new MetaWrapper() { Meta = request.Meta };
        IEnumerable<byte[]> attachments = (request.Attachments is null) ? Enumerable.Empty<byte[]>() : request.Attachments.Select(a => a.Content);

        AkN.ILazyBundle bundle = parse(request.Content, meta1, attachments);

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
    private static ParseFunction EWCAParserPDF = AkN.Parser.MakeParser4(UK.Gov.Legislation.Judgments.Parse.EWPDF.Parse3);

    private static ParseFunction ALL = Combine(new List<ParseFunction>(3) { EWCAParser, UKSCParser, EWCAParserPDF });
    private static ParseFunction EWCACombined = Combine(new List<ParseFunction>(2) { EWCAParser, EWCAParserPDF });

    private static ParseFunction GetParser(Hint? hint) {
        if (!hint.HasValue)
            return ALL;
        if (hint.Value == Hint.UKSC)
            return UKSCParser;
        return EWCACombined;
    }

    internal static ParseFunction Combine(List<ParseFunction> parsers) {
        if (!parsers.Any())
            return null;
        if (parsers.Count == 1)
            return parsers.First();
        return (byte[] docx, IOutsideMetadata meta, IEnumerable<byte[]> attachments) => {
            AkN.ILazyBundle bestBundle = null;
            int bestScore = -1;
            foreach (ParseFunction parse in parsers) {
                AkN.ILazyBundle bundle = parse(docx, meta, attachments);
                Meta meta2 = Parser.ConvertInternalMetadata(AkN.MetadataExtractor.Extract(bundle.Judgment));
                int score = Score(meta2);
                if (score == PerfectScore)
                    return bundle;
                if (score > bestScore) {
                    if (bestBundle is not null)
                        bestBundle.Close();
                    bestBundle = bundle;
                    bestScore = score;
                }
            }
            return bestBundle;
        };
    }

    private static int PerfectScore = 5;

    private static int Score(Meta meta) {
        int score = 0;
        if (meta.Uri is not null)
            score += 1;
        if (meta.Court is not null)
            score += 1;
        if (meta.Cite is not null)
            score += 1;
        if (meta.Date is not null)
            score += 1;
        if (meta.Name is not null)
            score += 1;
        return score;
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
