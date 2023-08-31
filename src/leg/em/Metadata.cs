
using System;
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

class Metadata : DocumentMetadata {

    public Tuple<string, int> AltNum { get; init; }

    internal static Metadata Make(List<IBlock> header, WordprocessingDocument doc) {
        string name = HeaderSplitter.GetDocumentType(header);
        string number = HeaderSplitter.GetDocumentNumber(header);
        string uri = number is null ? null : RegulationNumber.MakeURI(number) + "/em";
        // Tuple<string, int> altNum = RegulationNumber.ExtractAltNumber(number);
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");
        return new Metadata {
            Name = name,
            ShortUriComponent = uri,
            // AltNum = altNum,
            CSS = css
        };
    }

}

}
