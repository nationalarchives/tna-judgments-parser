using System;
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.Common {

class BaseMetadata : DocumentMetadata {

    public Tuple<string, int> AltNum { get; init; }

    internal static BaseMetadata Make(List<IBlock> header, WordprocessingDocument doc, LegislativeDocumentConfig config) {
        string name = BaseHeaderSplitter.GetDocumentType(header, config);
        // If header parsing fails to identify document type, use the default
        if (string.IsNullOrEmpty(name)) {
            name = config.DefaultDocumentType;
        }
        string number = BaseHeaderSplitter.GetDocumentNumber(header);
        string uri = number is null ? null : RegulationNumber.MakeURI(number) + config.UriSuffix;
        // Tuple<string, int> altNum = RegulationNumber.ExtractAltNumber(number);
        DateTime? modified = doc.PackageProperties.Modified;
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");
        return new BaseMetadata {
            ShortUriComponent = uri,
            ExpressionDate = Builder.FormatDateAndTime(modified),
            ExpressionDateName = modified is null ? null : "lastModified",
            Name = name,
            CSS = css
        };
    }

}

}
