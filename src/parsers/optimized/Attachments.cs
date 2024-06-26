

using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class Attachment : IInternalAttachment {

    public AttachmentType Type { get; init; }

    public int Number { get; init; }

    public IEnumerable<IBlock> Contents { get; init; }

    public Dictionary<string, Dictionary<string, string>> Styles { private get; init; }

    public Dictionary<string, Dictionary<string, string>> CSSStyles() => this.Styles;

    public IEnumerable<IImage> Images { get; init; }

}

class AttachmentParser {

    internal static IInternalAttachment Parse(WordprocessingDocument doc, AttachmentType type, int n) {
        WordDocument preParsed = new PreParser().Parse(doc);
        return Parse(doc, preParsed, type, n);
    }

    internal static IInternalAttachment Parse(WordprocessingDocument doc, WordDocument preParsed, AttachmentType type, int n) {
        var contents = Enumerable.Concat( preParsed.Header, preParsed.Body.Select(bb => bb.Block) );
        var styles = DOCX.CSS.Extract(doc.MainDocumentPart, "#" + Enum.GetName(typeof(AttachmentType), type).ToLower() + n);
        var images = WImage.Get(doc);
        return new Attachment() { Type = type, Number = n, Contents = contents, Styles = styles, Images = images };
    }

}

}
