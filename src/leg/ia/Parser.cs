
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

    // All parsing logic is now inherited from BaseLegislativeDocumentParser
    
    protected override List<IBlock> Header() {
        List<IBlock> header = BaseHeaderSplitter.Split(PreParsed.Body, Config);
        i = header.Count;
        return header;
    }

    protected override IDocument Parse() {
        logger.LogInformation("Starting parse of IA document with filename: {Filename}", _filename ?? "(not provided)");

        List<IBlock> header = Header();
        header.InsertRange(0, PreParsed.Header);
        
        List<IDivision> body = ParseBody();
        IEnumerable<IAnnex> annexes = Annexes();
        IEnumerable<IImage> images = WImage.Get(doc);
        
        // Use IA-specific metadata creation with filename
        DocumentMetadata metadata = IAMetadata.Make(header, doc, Config, _filename);
        
        logger.LogInformation("Document type: {}", metadata.Name);
        logger.LogInformation("Document URI: {}", metadata.ShortUriComponent);
        if (metadata.LegislationUri is not null) {
            logger.LogInformation("Legislation URI: {}", metadata.LegislationUri);
        }
            
        return new DividedDocument {
            Header = header,
            Body = body,
            Annexes = annexes,
            Images = images,
            Meta = metadata 
        };
    }

    private List<IDivision> ParseBody() {       
        List<IDivision> body = new List<IDivision>();
        while (i < PreParsed.Body.Count()) {
            if (NextBlockIsAnnexHeading())
                break;
            IDivision div = ParseDivsion();
            body.Add(div);
        }
        return body;
    }

    private bool NextBlockIsAnnexHeading() {
        if (i == PreParsed.Body.Count)
            return false;
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        return IsFirstLineOfAnnex(block);
    }

}

}
