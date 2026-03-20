
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Common;
using UK.Gov.Legislation.Models;
using CaseLaw = UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.ExplanatoryNotes {

partial class Parser : BaseLegislativeDocumentParser {

    private readonly string _filename;

    internal static IDocument Parse(WordprocessingDocument doc, string filename = null) {
        CaseLaw.WordDocument preParsed = new CaseLaw.PreParser().Parse(doc);
        Parser instance = new Parser(doc, preParsed, filename);
        return instance.Parse();
    }

    private static ILogger logger = Logging.Factory.CreateLogger<Parser>();

    private Parser(WordprocessingDocument doc, CaseLaw.WordDocument preParsed, string filename) : base(doc, preParsed, LegislativeDocumentConfig.ForExplanatoryNotes()) {
        _filename = filename;
    }

    // All parsing logic is inherited from BaseLegislativeDocumentParser

    protected override DocumentMetadata MakeMetadata(List<IBlock> header) {
        return ENMetadata.Make(header, doc, Config, _filename);
    }

    protected override List<IBlock> Header() {
        // EN header structure is different from EM/IA
        // For now, use a simple approach: look for "EXPLANATORY NOTES" title
        List<IBlock> header = new List<IBlock>();
        
        for (int j = 0; j < PreParsed.Body.Count && j < 10; j++) {
            IBlock block = PreParsed.Body[j].Block;
            if (block is not WLine line)
                continue;
                
            string content = line.NormalizedContent?.ToUpperInvariant() ?? "";
            
            header.Add(block);

            // Look for "EXPLANATORY NOTES" as the document type marker
            if (content.Contains("EXPLANATORY") && content.Contains("NOTES")) {
                i = j + 1;
                break;
            }
        }
        
        // If we didn't find the marker, assume first 3 blocks are header
        if (i == 0) {
            int headerSize = System.Math.Min(3, PreParsed.Body.Count);
            for (int j = 0; j < headerSize; j++) {
                header.Add(PreParsed.Body[j].Block);
            }
            i = headerSize;
        }
        
        return header;
    }

}

}
