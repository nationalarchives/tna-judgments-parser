using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Models;
using CaseLaw = UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.Common {

partial class BaseLegislativeDocumentParser : CaseLaw.OptimizedParser {

    protected readonly LegislativeDocumentConfig Config;
    private static ILogger logger = Logging.Factory.CreateLogger<BaseLegislativeDocumentParser>();
    
    // Parsing diagnostics
    private int parseDepth = 0;
    private int parseDepthMax = 0;
    private int totalBlocks = 0;

    protected BaseLegislativeDocumentParser(WordprocessingDocument doc, CaseLaw.WordDocument preParsed, LegislativeDocumentConfig config) 
        : base(doc, preParsed, null, null) {
        Config = config;
        totalBlocks = preParsed.Body.Count;
    }

    protected virtual IDocument Parse() {
        logger.LogInformation("Starting parse of document with {} total blocks", totalBlocks);

        List<IBlock> header = Header();
        header.InsertRange(0, PreParsed.Header);
        logger.LogDebug("Parsed header with {} blocks, starting body at block {}", header.Count, i);

        List<IDivision> body = Body2();
        IEnumerable<IAnnex> annexes = Annexes();
        
        // Enhanced completion tracking
        int blocksProcessed = i;
        int blocksRemaining = totalBlocks - blocksProcessed;
        
        if (blocksRemaining > 0) {
            logger.LogWarning("Parsing did not complete: {} of {} blocks processed, {} blocks remaining", 
                blocksProcessed, totalBlocks, blocksRemaining);
        } else {
            logger.LogInformation("Parsing completed successfully: all {} blocks processed", totalBlocks);
        }
        
        logger.LogInformation("Maximum parse depth reached: {}", parseDepthMax);

        IEnumerable<IImage> images = WImage.Get(doc);
        DocumentMetadata metadata = MakeMetadata(header);
        logger.LogInformation("Document type: {}", metadata.Name);
        logger.LogInformation("Document URI: {}", metadata.ShortUriComponent);
        logger.LogInformation("Parsed {} divisions in body, {} annexes, {} images", 
            body.Count, annexes.Count(), images.Count());
            
        return new DividedDocument {
            Header = header,
            Body = body,
            Annexes = annexes,
            Images = images,
            Meta = metadata 
        };
    }

    protected virtual DocumentMetadata MakeMetadata(List<IBlock> header) {
        return BaseMetadata.Make(header, doc, Config);
    }

    protected override List<IBlock> Header() {
        List<IBlock> header = BaseHeaderSplitter.Split(PreParsed.Body, Config);
        i = header.Count;
        return header;
    }

    protected override List<IDecision> Body() {
        throw new System.NotImplementedException();
    }

    private List<IDivision> Body2() {       
        List<IDivision> body = new List<IDivision>();
        while (i < PreParsed.Body.Count()) {
            if (NextBlockIsAnnexHeading())
                break;
            IDivision div = ParseDivsion();
            body.Add(div);
        }
        return body;
    }

    protected IDivision ParseDivsion() {
        EnterParseDepth();
        try {
            int save = i;
            IDivision div = ParseCrossHeading();
            if (div is not null)
                return div;
            i = save;
            div = ParseSection();
            if (div is not null)
                return div;
            i = save;
            return ParseParagraph();
        }
        finally {
            ExitParseDepth();
        }
    }

    private void EnterParseDepth() {
        parseDepth++;
        if (parseDepth > parseDepthMax) {
            parseDepthMax = parseDepth;
        }
    }

    private void ExitParseDepth() {
        parseDepth--;
    }

    /* cross-headings */

    private bool IsCrossHeading(WLine line) {
        if (i < 3)
            return false;
        if (line is WOldNumberedParagraph)
            return false;
        if (line.Style == Config.SectionTitleStyle)
            return false;
        float left = line.LeftIndentInches ?? 0f;
        if (left != 0)
            return false;
        float first = line.FirstLineIndentInches ?? 0f;
        if (first != 0)
            return false;
        // if (!line.IsFlushLeft())
        //     return false;
        if (!line.Contents.All(i => i is IFormattedText ft && (ft.Bold ?? false))) // IsAllBold
            return false;
        return true;
    }

    private IDivision ParseCrossHeading() {
        IBlock block1 = PreParsed.Body[i].Block;
        if (block1 is not WLine line1)
            return null;
        if (!IsCrossHeading(line1))
            return null;
        i += 1;

        List<Section> children = new ();
        while (i < PreParsed.Body.Count) {
            Section child = ParseSection();
            if (child is null)
                break;
            children.Add(child);
        }
        if (!children.Any())
            return null;

        return new CrossHeading{ Heading = line1, Children = children };
    }

    /* sections */

    [GeneratedRegex("^\\d+\\.$")]
    private static partial Regex SectionNumberRegex();

    private bool IsSectionHeading(IBlock block) {
        if (block is WOldNumberedParagraph np) {
            if (np.Style != Config.SectionTitleStyle)
                return false;
            if (!SectionNumberRegex().IsMatch(np.Number.Text))
                return false;
            return true;
        }
        if (!Config.SectionTitleRequiresNumber && block is WLine line) {
            if (line.Style != Config.SectionTitleStyle)
                return false;
            return true;
        }
        return false;
    }

    private Section ParseSection() {
        EnterParseDepth();
        try {
            logger.LogTrace("Parsing section at block {} (depth {})", i, parseDepth);
            IBlock block = PreParsed.Body.ElementAt(i).Block;
            if (!IsSectionHeading(block)) {
                logger.LogTrace("Block {} is not a section heading: {}", i, block.GetType().Name);
                return null;
            }
            WLine line = block as WLine;
            i += 1;

            IFormattedText num;
            ILine heading;
            if (line is WOldNumberedParagraph np) {
                num = np.Number;
                heading = WLine.RemoveNumber(np);
                logger.LogTrace("Found numbered section: {}", num.Text);
            } else {
                num = null;
                heading = line;
                logger.LogTrace("Found unnumbered section");
            }
            List<IDivision> children = ParseSectionChildren();
            logger.LogTrace("Section parsed with {} children", children.Count);
            return new Section { Number = num, Heading = heading, Children = children };
        }
        catch (System.Exception ex) {
            logger.LogError(ex, "Error parsing section at block {}", i);
            throw;
        }
        finally {
            ExitParseDepth();
        }
    }

    private List<IDivision> ParseSectionChildren() {
        List<IDivision> children = new List<IDivision>();
        IDivision child = ParseSectionChild();
        while (child is not null) {
            children.Add(child);
            child = ParseSectionChild();
        }
        return children;
    }

    private IDivision ParseSectionChild() {
        if (i == PreParsed.Body.Count)
            return null;
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        if (IsFirstLineOfAnnex(block))
            return null;
        if (IsSectionHeading(block))
            return null;
        if (IsLevel1Subheading(block))
            return ParseLevel1Subheading(block as WLine);
        if (IsSectionCrossHeading(block))
            return ParseSectionCrossHeading(block as WLine);
        return ParseParagraph();
    }

    /* section cross-headings (legacy documents without named subheading styles) */

    // Detects all-bold flush-left unnumbered paragraphs used as informal sub-headings
    // in legacy IAs that pre-date the IALevel1Subheading style convention.
    private bool IsSectionCrossHeading(IBlock block) {
        if (block is not WLine line)
            return false;
        if (block is WOldNumberedParagraph)
            return false;
        if (IsLevel1Subheading(block) || IsLevel2Subheading(block))
            return false;
        if (line.Contents is null || !line.Contents.Any())
            return false;
        if (!line.Contents.All(run => run is IFormattedText ft && (ft.Bold ?? false)))
            return false;
        float left = line.LeftIndentInches ?? 0f;
        if (left > 0.1f)
            return false;
        return true;
    }

    private Subheading ParseSectionCrossHeading(WLine heading) {
        i += 1;
        List<IDivision> children = new();
        while (i < PreParsed.Body.Count) {
            IBlock block = PreParsed.Body.ElementAt(i).Block;
            if (IsSectionHeading(block)) break;
            if (IsLevel1Subheading(block)) break;
            if (IsSectionCrossHeading(block)) break;
            IDivision child = ParseParagraph();
            if (child is null) break;
            children.Add(child);
        }
        return new Subheading { Heading = heading, Children = children };
    }

    /* sub-headings */

    private bool IsLevel1Subheading(IBlock block) {
        if (block is not WLine line)
            return false;
        if (block is WOldNumberedParagraph)
            return false;
        if (line.Style != Config.Level1SubheadingStyle)
            return false;
        return true;
    }

    private Subheading ParseLevel1Subheading(WLine subhead) {
        i += 1;
        List<IDivision> children = ParseLevel1SubheadingChildren();
        return new Subheading { Heading = subhead, Children = children };
    }

    private List<IDivision> ParseLevel1SubheadingChildren() {
        List<IDivision> children = new();
        IDivision child = ParseLevel1SubheadingChild();
        while (child is not null) {
            children.Add(child);
            child = ParseLevel1SubheadingChild();
        }
        return children;
    }

    private IDivision ParseLevel1SubheadingChild() {
        if (i == PreParsed.Body.Count)
            return null;
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        if (IsSectionHeading(block))
            return null;
        if (IsLevel1Subheading(block))
            return null;
        if (IsLevel2Subheading(block))
            return ParseLevel2Subheading(block as WLine);
        return ParseParagraph();
    }

    private bool IsLevel2Subheading(IBlock block) {
        if (block is not WLine line)
            return false;
        if (block is WOldNumberedParagraph)
            return false;
        if (line.Style != Config.Level2SubheadingStyle)
            return false;
        return true;
    }

    private Subheading ParseLevel2Subheading(WLine subhead) {
        i += 1;
        List<IDivision> children = ParseLevel2SubheadingChildren();
        return new Subheading { Heading = subhead, Children = children };
    }

    private List<IDivision> ParseLevel2SubheadingChildren() {
        List<IDivision> children = new();
        IDivision child = ParseLevel2SubheadingChild();
        while (child is not null) {
            children.Add(child);
            child = ParseLevel2SubheadingChild();
        }
        return children;
    }

    private IDivision ParseLevel2SubheadingChild() {
        if (i == PreParsed.Body.Count)
            return null;
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        if (IsSectionHeading(block))
            return null;
        if (IsLevel1Subheading(block))
            return null;
        if (IsLevel2Subheading(block))
            return null;
        return ParseParagraph();
    }

    /* paragraphs */

    override protected bool CannotBeSubparagraph(WLine line) {
        if (IsFirstLineOfAnnex(line))
            return true;
        if (IsSectionHeading(line))
            return true;
        if (IsLevel1Subheading(line))
            return true;
        if (IsLevel2Subheading(line))
            return true;
        return false;
    }

    /* annexes */

    private bool NextBlockIsAnnexHeading() {
        if (i == PreParsed.Body.Count)
            return false;
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        return IsFirstLineOfAnnex(block);
    }

    protected override bool IsFirstLineOfAnnex(IBlock block) {
        if (block is not WLine line) return false;
        return IsLegAnnexHeading(line.NormalizedContent);
    }

    private const int MaxAnnexHeadingLength = 120;

    internal static bool IsLegAnnexHeading(string content) {
        string text = content?.Trim() ?? "";
        if (text.Length == 0 || text.Length > MaxAnnexHeadingLength) return false;

        if (text.Equals("Annex", System.StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Equals("- Annex -", System.StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Equals("Appendix", System.StringComparison.OrdinalIgnoreCase)) return true;
        if (Regex.IsMatch(text, @"^Annex \d+$", RegexOptions.IgnoreCase)) return true;

        string stripped = Regex.Replace(text, @"^[A-Za-z0-9]{1,3}[\.\):]\s+", "");
        if (stripped.Length == 0) return false;

        if (Regex.IsMatch(stripped, @"^(Annexes|Appendices)[\s:]*$",
                          RegexOptions.IgnoreCase))
            return true;
        return Regex.IsMatch(
            stripped,
            @"^(Annex|Appendix)\s+[A-Z]{1,3}(?:[\s\.\:\-\u2013\u2014]+(?:[A-Za-z0-9][^\.]{0,100})?)?$",
            RegexOptions.IgnoreCase);
    }

}

}
