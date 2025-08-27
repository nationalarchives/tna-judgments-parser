
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

    internal static IDocument Parse(WordprocessingDocument doc) {
        CaseLaw.WordDocument preParsed = new CaseLaw.PreParser().Parse(doc);
        Parser instance = new Parser(doc, preParsed);
        return instance.Parse();
    }

    private static ILogger logger = Logging.Factory.CreateLogger<Parser>();

    private Parser(WordprocessingDocument doc, CaseLaw.WordDocument preParsed) : base(doc, preParsed, LegislativeDocumentConfig.ForImpactAssessments()) { }

    // All parsing logic is now inherited from BaseLegislativeDocumentParser
    
    protected override List<IBlock> Header() {
        List<IBlock> header = BaseHeaderSplitter.Split(PreParsed.Body, Config);
        i = header.Count;
        return header;
    }

}

}
