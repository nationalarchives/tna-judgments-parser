
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Judgments.Parse.UKSC;
using System.Text.RegularExpressions;
using UK.Gov.NationalArchives.CaseLaw.Parsers;

namespace UK.Gov.NationalArchives.CaseLaw.Parse.UKSC {

class PressSummary : IAknDocument {

    public DocType Type { get => DocType.Summary; }

    public IAknMetadata Metadata { get; init; }

    public IEnumerable<IBlock> Body { get; init; }

    public IEnumerable<IImage> Images { get; init; }

}

class PressSummaryParser {

    internal IAknDocument Parse(WordprocessingDocument doc) {
        var contents = doc.MainDocumentPart.Document.Body.ChildElements.Select(e => ParseParagraph(doc.MainDocumentPart, e)).Where(p => p is not null);
        contents = new RemoveTrailingWhitespace().Enrich(contents);
        contents = new Merger().Enrich(contents);
        contents = new PSCite().Enrich(contents);
        contents = new PSDocType().Enrich(contents);
        contents = new PSDate().Enrich(contents);
        var metadata = new PSMetadata(doc.MainDocumentPart, contents);
        var images = WImage.Get(doc);
        return new PressSummary { Metadata = metadata, Body = contents, Images = images };
    }

    private static IBlock ParseParagraph(MainDocumentPart main, OpenXmlElement e) {
        if (AbstractParser.IsSkippable(e))
            return null;
        if (e is Paragraph p) {
            DOCX.NumberInfo? info = DOCX.Numbering2.GetFormattedNumber(main, p);
            if (info is not null)
                return new WOldNumberedParagraph(info.Value, main, p);
            WLine line = new WLine(main, p);
            INumber num2 = Fields.RemoveListNum(line);
            if (num2 is not null)
                return new WOldNumberedParagraph(num2, line);
            return line;
        }
        if (e is Table table)
            return new WTable(main, table);
        // if (e is SdtBlock) {
        //     DocPartGallery dpg = e.Descendants<DocPartGallery>().FirstOrDefault();
        //     if (dpg is not null && dpg.Val.Value == "Table of Contents") {
        //         logger.LogWarning("skipping table of contents");
        //         return null;
        //     }
        // }
        throw new System.Exception(e.GetType().ToString());
    }

}

class Resource : IResource {

    public ResourceType Type { get; init; }

    public string ID { get; init; }

    public string URI { get; init; }

    public string ShowAs { get; init; }

    public string ShortForm { get; init; }

}

class PSMetadata : IAknMetadata {

    private IResource TNA = new Resource { ID = "tna", URI = "https://www.nationalarchives.gov.uk", ShowAs = "The National Archives" };
    private IResource UKSC = new Resource { ID = "uksc", URI = "https://www.supremecourt.uk", ShowAs = "The Supreme Court" };

    public IResource Source { get => TNA; }

    public IResource Author { get => UKSC; }

    private string ShortUriComponent { get; init; }

    public string WorkURI { get => "https://caselaw.nationalarchives.gov.uk/id/" + ShortUriComponent; }

    public string ExpressionURI { get => "https://caselaw.nationalarchives.gov.uk/" + ShortUriComponent; }

    public INamedDate Date { get; private init; }

    public string Name { get; private init; }

    public string ProprietaryNamespace { get => "https://caselaw.nationalarchives.gov.uk/akn"; }

    public IList<Tuple<String, String>> Proprietary { get; private init; }

    public IDictionary<string, IDictionary<string, string>> CSSStyles { get; private init; }

    internal PSMetadata(MainDocumentPart main, IEnumerable<IBlock> contents) {
        Proprietary = new List<Tuple<String, String>>();
        WDocDate date = Util.Descendants<WDocDate>(contents).FirstOrDefault();
        if (date is not null) {
            Date = new WNamedDate { Name = "release", Date = date.Date };
        }
        WDocType docType = Util.Descendants<WDocType>(contents).FirstOrDefault();
        if (docType is not null) {
            Name = docType.Name();
        }
        WNeutralCitation cite = Util.Descendants<WNeutralCitation>(contents).FirstOrDefault();
        if (cite is not null) {
            string normalized = Citations.Normalize(cite.Text);
            ShortUriComponent = Citations.MakeUriComponent(normalized) + "/summary/1";
            // Proprietary.Add(new Tuple<string, string>("cite", normalized));
        }
        // CSSStyles = DOCX.CSS.Extract(main, "#main");
    }

}

class PSCite : CiteEnricher {

    private readonly int Limit = 6;

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        int count = 0;
        List<IBlock> enriched = new List<IBlock>(blocks.Count());
        var enumerator = blocks.GetEnumerator();
        while (enumerator.MoveNext()) {
            count += 1;
            if (count > Limit)
                break;
            IBlock block = enumerator.Current;
            IBlock enriched1 = Enrich(block);
            enriched.Add(enriched1);
            if (Object.ReferenceEquals(enriched1, block))
                continue;
            while (enumerator.MoveNext())
                enriched.Add(enumerator.Current);
            return enriched;
        }
        return blocks;
    }

}

class WDocType : IInlineContainer {

    public IEnumerable<IInline> Contents { get; init; } // init shouldn't need to be public

    public string Name() {
        string text = ILine.TextContent(Contents);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return Regex.Replace(text, @"([A-Z])([A-Z]+)\b", m => m.Groups[1].Value + m.Groups[2].Value.ToLower());
    }

    public WDocType(IEnumerable<IFormattedText> contents) {
        Contents = contents;
    }

}

class PSDocType : Enricher {

    private readonly int Limit = 3;

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        int count = 0;
        List<IBlock> enriched = new List<IBlock>(blocks.Count());
        var enumerator = blocks.GetEnumerator();
        while (enumerator.MoveNext()) {
            count += 1;
            if (count > Limit)
                break;
            IBlock block = enumerator.Current;
            IBlock enriched1 = Enrich(block);
            enriched.Add(enriched1);
            if (Object.ReferenceEquals(enriched1, block))
                continue;
            while (enumerator.MoveNext())
                enriched.Add(enumerator.Current);
            return enriched;
        }
        return blocks;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        if (!line.All(i => i is IFormattedText))
            return line;
        string text = ILine.TextContent(line);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (!string.Equals(text, "Press Summary", StringComparison.OrdinalIgnoreCase))
            return line;
        return new List<IInline>(1) {
            new WDocType(line.Cast<IFormattedText>())
        };
    }

}

class PSDate : Enricher {

    private readonly int Limit = 3;

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        int count = 0;
        List<IBlock> enriched = new List<IBlock>(blocks.Count());
        var enumerator = blocks.GetEnumerator();
        while (enumerator.MoveNext()) {
            count += 1;
            if (count > Limit)
                break;
            IBlock block = enumerator.Current;
            IBlock enriched1 = Enrich(block);
            enriched.Add(enriched1);
            if (Object.ReferenceEquals(enriched1, block))
                continue;
            while (enumerator.MoveNext())
                enriched.Add(enumerator.Current);
            return enriched;
        }
        return blocks;
    }

    protected override WLine Enrich(WLine line) {
        return Date0.Enrich(line, "release", 1);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new NotImplementedException();
    }

}

// class PSBundle : ILazyBundle {

//     private readonly WordprocessingDocument doc;
//     private readonly PressSummary ps;
//     private XmlDocument xml;

//     internal PSBundle(WordprocessingDocument doc, PressSummary ps) {
//         this.doc = doc;
//         this.ps = ps;
//         ImageConverter.ConvertImages(ps);
//     }

//     public string ShortUriComponent { get => judgment.Metadata.ShortUriComponent; }

//     public XmlDocument Judgment {
//         get {
//             if (xml is null)
//                 xml = JudgmentBuilder.Build(judgment);
//             return xml;
//         }
//     }

//     public IEnumerable<IImage> Images { get => judgment.Images; }

//     public void Close() {
//         doc.Close();
//     }

// }

}
