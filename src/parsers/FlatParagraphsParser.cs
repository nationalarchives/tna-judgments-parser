
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.Parse {

class Attachment : IInternalAttachment {

    public AttachmentType Type { get; init; }

    public int Number { get; init; }

    public IEnumerable<IBlock> Contents { get; init; }

    public Dictionary<string, Dictionary<string, string>> Styles { private get; init; }

    public Dictionary<string, Dictionary<string, string>> CSSStyles() => this.Styles;

    public IEnumerable<IImage> Images { get; init; }

}

class FlatParagraphsParser {

    private static ILogger logger = Logging.Factory.CreateLogger<FlatParagraphsParser>();

    internal static IInternalAttachment Parse(WordprocessingDocument doc, AttachmentType type, int n) {
        var contents = doc.MainDocumentPart.Document.Body.ChildElements.Select(e => ParseParagraph(doc, e)).Where(p => p is not null);
        contents = new Merger().Enrich(contents);
        var styles = DOCX.CSS.Extract(doc.MainDocumentPart, "#" + Enum.GetName(typeof(AttachmentType), type).ToLower() + n);
        var images = WImage.Get(doc);
        return new Attachment() { Type = type, Number = n, Contents = contents, Styles = styles, Images = images };
    }

    private static IBlock ParseParagraph(WordprocessingDocument doc, OpenXmlElement e) {
        MainDocumentPart main = doc.MainDocumentPart;
        if (AbstractParser.IsSkippable(e))
            return null;
        if (e is Paragraph p) {
            WLine line = new WLine(main, p);
            DOCX.NumberInfo? info = DOCX.Numbering2.GetFormattedNumber(main, p);
            if (info is not null) {
                // ParagraphMarkRunProperties pMarkProps = p.ParagraphProperties.ParagraphMarkRunProperties;
                // string styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                // Style style = styleId is null ? null : DOCX.Styles.GetStyle(main, styleId);
                // DOCX.WNumber number = new DOCX.WNumber(main, info.Value.Number, info.Value.Props, pMarkProps, style, p.ParagraphProperties);
                return new WOldNumberedParagraph(info.Value.Number, line);
            }
            INumber num2 = Fields.RemoveListNum(line);
            if (num2 is not null)
                return new WOldNumberedParagraph(num2.Text, line);
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

}
