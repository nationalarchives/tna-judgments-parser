
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

namespace UK.Gov.Legislation.ImpactAssessments {

partial class Parser : BaseLegislativeDocumentParser {

    private readonly string _filename;

    internal static IDocument Parse(WordprocessingDocument doc, string filename = null) {
        CaseLaw.WordDocument preParsed = new CaseLaw.PreParser().Parse(doc);
        Parser instance = new Parser(doc, preParsed, filename);
        return instance.Parse();
    }

    private static ILogger logger = Logging.Factory.CreateLogger<Parser>();

    private Parser(WordprocessingDocument doc, CaseLaw.WordDocument preParsed, string filename) 
        : base(doc, preParsed, LegislativeDocumentConfig.ForImpactAssessments()) {
        _filename = filename;
    }

    protected override List<IBlock> Header() {
        List<IBlock> header = BaseHeaderSplitter.Split(PreParsed.Body, Config);
        i = header.Count;
        
        SemanticEnricher enricher = new SemanticEnricher();
        header = enricher.Enrich(header).ToList();
        
        return header;
    }

    protected override IDocument Parse() {
        var result = base.Parse();
        
        if (result is DividedDocument dividedDoc) {
            SemanticEnricher enricher = new SemanticEnricher();
            var enrichedBody = enricher.Enrich(dividedDoc.Body).ToList();
            
            return new DividedDocument {
                Header = dividedDoc.Header,
                Body = enrichedBody,
                Annexes = dividedDoc.Annexes,
                Images = dividedDoc.Images,
                Meta = dividedDoc.Meta
            };
        }
        
        return result;
    }

    protected override DocumentMetadata MakeMetadata(List<IBlock> header) {
        return IAMetadata.Make(header, doc, Config, _filename);
    }

}

}
