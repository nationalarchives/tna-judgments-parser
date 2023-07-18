
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

class Metadata {

    internal static DocumentMetadata Make(WordprocessingDocument doc) {
        string name = "Explanatory Memorandum";
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");
        return new DocumentMetadata {
            Name = name,
            ShortUriComponent = null,
            CSS = css
        };
    }

}

}
