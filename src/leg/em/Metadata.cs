
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

class Metadata {

    internal static DocumentMetadata Make(List<IBlock> header, WordprocessingDocument doc) {
        string name = HeaderSplitter.GetDocumentType(header);
        string number = HeaderSplitter.GetDocumentNumber(header);
        string uri = number is null ? null : RegulationNumber.MakeURI(number) + "/em";
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");
        return new DocumentMetadata {
            Name = name,
            ShortUriComponent = uri,
            CSS = css
        };
    }

}

}