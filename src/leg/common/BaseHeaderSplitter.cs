using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Models;
using UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Walks the opening blocks of a leg .docx and pulls out the document
/// type, title, and regulation number, returning them wrapped with
/// DocType2/DocNumber2 markers so the AKN Builder can render them as
/// the &lt;preface&gt; (centred docType + title + docNumber).
///
/// State machine:
///
///   Start           → AfterDocType         (matched DocumentTitle)
///   Start           → AfterRegulationTitle (matched "&lt;DocumentTitle&gt; &lt;title&gt;" on one line)
///   AfterDocType    → AfterRegulationTitle (consumed the title line)
///   AfterRegTitle   → AfterRegulationTitle (consumed a multi-line title continuation)
///   AfterRegTitle   → AfterDocNumber       (matched <see cref="RegulationNumber.Is"/>)
///   AfterDocNumber  → Done                 (optional second paired EM via TryParseSecondTitleAndNumber)
///   any             → Fail                 (clears Enriched; body parser sees the original blocks)
///
/// Variants supported (real production patterns; each has a repro in
/// the validator's results.ndjson):
///
///  * Leading blank paragraphs before "EXPLANATORY MEMORANDUM TO"
///  * Leading non-paragraph blocks (cover-page tables)
///  * Blank paragraphs between heading lines
///  * Multi-line titles (continuation paragraphs before the number)
///  * "EXPLANATORY MEMORANDUM TO &lt;title&gt;" on a single line
///  * "EXPLANATORY MEMORANDUM" with no "TO"
///  * Draft / unassigned regulation numbers (see <see cref="RegulationNumber"/>)
///
/// Returning an empty Enriched list is the explicit signal "no header
/// here" — the body parser will then see the original block list
/// unchanged.
/// </summary>
class BaseHeaderSplitter {

    protected readonly LegislativeDocumentConfig Config;

    internal static List<IBlock> Split(IEnumerable<BlockWithBreak> blocks, LegislativeDocumentConfig config) {
        return Split(blocks.Select(bb => bb.Block), config);
    }
    
    internal static List<IBlock> Split(IEnumerable<IBlock> blocks, LegislativeDocumentConfig config) {
        var enricher = new BaseHeaderSplitter(blocks, config);
        enricher.Enrich();
        return enricher.Enriched;
    }

    private enum State {
        Start,
        AfterDocType,
        AfterRegulationTitle,
        AfterDocNumber,
        Done,
        Fail
    };

    private State state = State.Start;

    private readonly List<IBlock> Blocks;

    private int I = 0;

    private readonly List<IBlock> Enriched = new List<IBlock>(3);

    protected BaseHeaderSplitter(IEnumerable<IBlock> blocks, LegislativeDocumentConfig config) {
        Blocks = blocks is List<IBlock> list ? list : new List<IBlock>(blocks);
        Config = config;
    }

    private void Enrich() {
        while (I < Blocks.Count) {
            var block = Blocks[I];
            if (state == State.Start) {
                Start(block);
                I += 1;
                continue;
            }
            if (state == State.AfterDocType) {
                AfterDocType(block);
                I += 1;
                continue;
            }
            if (state == State.AfterRegulationTitle) {
                AfterRegulationTitle(block);
                I += 1;
                continue;
            }
            if (state == State.AfterDocNumber) {
                AfterDocNumber(block);
                I += 1;
                continue;
            }
            if (state == State.Done) {
                break;
            }
            if (state == State.Fail) {
                Enriched.Clear();
                return;
            }
            throw new System.NotImplementedException();
        }
        // If we ran out of blocks without ever reaching AfterDocNumber, the
        // header was never confirmed (most often: a multi-line-title scan
        // that never found a regulation number). Clear partial output so the
        // body parser sees the original blocks, matching the explicit Fail
        // path above.
        if (state != State.Done && state != State.AfterDocNumber)
            Enriched.Clear();
    }

    internal static string GetDocumentType(List<IBlock> header, LegislativeDocumentConfig config) {
        DocType2 docType = Util.Descendants<DocType2>(header).FirstOrDefault();
        if (docType is null)
            return null;
        string name = IInline.ToString(docType.Contents);
        name = Regex.Replace(name, @"\s+", " ").Trim();
        
        foreach (var mapping in config.DocumentTypeMapping) {
            if (mapping.Key.Equals(name, System.StringComparison.InvariantCultureIgnoreCase))
                return mapping.Value;
        }
        return null;
    }

    private static bool IsBlank(WLine line) =>
        string.IsNullOrWhiteSpace(line.NormalizedContent);

    private void Start(IBlock block) {
        // Skip non-paragraph leading blocks (tables, figures) — some
        // templates put a cover-page table before "EXPLANATORY MEMORANDUM
        // TO" — and stay in Start so the splitter picks the heading up
        // on the next iteration.
        if (block is not WLine line)
            return;
        // Some templates open with one or more empty paragraphs before
        // "EXPLANATORY MEMORANDUM TO"; skip them and stay in Start.
        if (IsBlank(line))
            return;
        if (line is WOldNumberedParagraph) {
            state = State.Fail;
            return;
        }
        string content = line.NormalizedContent;
        bool isTitle = Config.DocumentTitles.Any(title => title.Equals(content, System.StringComparison.InvariantCultureIgnoreCase));
        if (isTitle) {
            DocType2 docType = new DocType2 { Contents = line.Contents };
            WLine newLine = WLine.Make(line, new List<IInline>(1) { docType });
            Enriched.Add(newLine);
            state = State.AfterDocType;
            return;
        }
        // Some templates run "EXPLANATORY MEMORANDUM TO <regulation title>"
        // on a single line; tag the whole line as DocType and skip the
        // separate title state, since the title text is embedded inside.
        bool isPrefix = Config.DocumentTitles.Any(title =>
            content.StartsWith(title + " ", System.StringComparison.InvariantCultureIgnoreCase));
        if (isPrefix) {
            DocType2 docType = new DocType2 { Contents = line.Contents };
            WLine newLine = WLine.Make(line, new List<IInline>(1) { docType });
            Enriched.Add(newLine);
            state = State.AfterRegulationTitle;
            return;
        }
        // Aggressive fallback: some EMs omit the "EXPLANATORY MEMORANDUM TO"
        // label and open with the regulation title directly. If a known
        // regulation-number shape appears within the next few non-blank
        // blocks, treat this line as a single-line DocType (same as the
        // prefix-match path) so the body parser doesn't see the heading
        // as body content.
        if (HasRegulationNumberShapeWithin(maxNonBlankLookAhead: 4)) {
            DocType2 docType = new DocType2 { Contents = line.Contents };
            WLine newLine = WLine.Make(line, new List<IInline>(1) { docType });
            Enriched.Add(newLine);
            state = State.AfterRegulationTitle;
            return;
        }
        state = State.Fail;
    }

    private bool HasRegulationNumberShapeWithin(int maxNonBlankLookAhead) {
        int seen = 0;
        for (int j = I + 1; j < Blocks.Count && seen < maxNonBlankLookAhead; j++) {
            if (Blocks[j] is not WLine candidate)
                continue;
            if (IsBlank(candidate))
                continue;
            seen += 1;
            if (RegulationNumber.Is(candidate.NormalizedContent))
                return true;
        }
        return false;
    }

    private void AfterDocType(IBlock block) {
        if (block is not WLine line) {
            state = State.Fail;
            return;
        }
        // Skip blank paragraphs between the document-type line and the title.
        if (IsBlank(line))
            return;
        Enriched.Add(line);
        state = State.AfterRegulationTitle;
    }

    private void AfterRegulationTitle(IBlock block) {
        if (block is not WLine line) {
            state = State.Fail;
            return;
        }
        // Skip blank paragraphs between title and number.
        if (IsBlank(line))
            return;
        if (RegulationNumber.Is(line.NormalizedContent)) {
            DocNumber2 docNumber = new DocNumber2 { Contents = line.Contents };
            WLine newLine = WLine.Make(line, new List<IInline>(1) { docNumber });
            Enriched.Add(newLine);
            state = State.AfterDocNumber;
            return;
        }
        // Structural fallback: if we hit a numbered body paragraph
        // ("1. This explanatory memorandum…") and we've already collected
        // a title line, the most recent collected line is almost certainly
        // the regulation number in a shape we don't recognise (broken
        // brackets, asterisks, etc.). Promote it to DocNumber so the
        // preface has the expected three-line structure rather than
        // failing the whole header. The body parser handles this block
        // (we don't add it to Enriched).
        if (line is WOldNumberedParagraph && PromoteLastTitleLineToDocNumber()) {
            state = State.Done;
            return;
        }
        // Some templates split the title across multiple paragraphs (e.g.
        // an Order title followed by "(NORTHERN IRELAND) 2016" on its own
        // line). Treat any non-blank, non-number line as a title
        // continuation and stay in this state until we find the number.
        Enriched.Add(line);
    }

    /// <summary>
    /// Take the last accumulated title line out of Enriched and wrap it
    /// as a DocNumber2 marker. Used by the structural fallback when we
    /// reach the body without finding a recognised regulation number.
    /// Returns false if there's no title line yet (only DocType has been
    /// added), in which case the caller should let the splitter Fail.
    /// </summary>
    private bool PromoteLastTitleLineToDocNumber() {
        if (Enriched.Count < 2)
            return false;
        int idx = Enriched.Count - 1;
        if (Enriched[idx] is not WLine last)
            return false;
        DocNumber2 docNumber = new DocNumber2 { Contents = last.Contents };
        Enriched[idx] = WLine.Make(last, new List<IInline>(1) { docNumber });
        return true;
    }

    internal static string GetDocumentNumber(List<IBlock> header) {
        DocNumber2 docNumber = Util.Descendants<DocNumber2>(header).FirstOrDefault();
        if (docNumber is null)
            return null;
        string text = IInline.ToString(docNumber.Contents);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private void AfterDocNumber(IBlock block) {
        if (block is WTable table && table.Rows.Count() == 1 && table.Rows.First() is WRow row)
        {
            if (row.Cells.Count() == 1 && row.Cells.First() is WCell cell)
            {
                if (cell.Contents.Count() == 1 && cell.Contents.First() is WLine x) {
                    if (x.NormalizedContent.StartsWith("The purpose of the instrument is") || x.NormalizedContent.StartsWith("Purpose of the instrument."))
                    {
                        Enriched.Add(x);
                        return;
                    }
                }
            }
        }
        if (block is not WLine line) {
            state = State.Done;
            return;
        }
        if (line.NormalizedContent.StartsWith("The above instrument")) {
            Enriched.Add(line);
            return;
        }
        TryParseSecondTitleAndNumber();
        state = State.Done;
    }

    private void TryParseSecondTitleAndNumber() {
        List<WLine> temp = new();

        /* this first line must be "AND" */
        if (I == Blocks.Count)
            return;
        IBlock next0 = Blocks[I];
        if (next0 is not WLine line0)
            return;
        if (!line0.NormalizedContent.Equals("AND", System.StringComparison.InvariantCultureIgnoreCase))
            return;
        temp.Add(line0);

        /* the next line can be either the type again or a title */
        if (I + 1 == Blocks.Count)
            return;
        IBlock next1 = Blocks[I + 1];
        if (next1 is not WLine line1)
            return;
        bool isTitle = Config.DocumentTitles.Any(title => title.Equals(line1.NormalizedContent, System.StringComparison.InvariantCultureIgnoreCase));
        if (isTitle) {
            DocType2 docType2 = new DocType2 { Contents = line1.Contents };
            WLine newLine1 = WLine.Make(line1, new List<IInline>(1) { docType2 });
            temp.Add(newLine1);
            if (I + 2 == Blocks.Count)
                return;
            IBlock next1bis = Blocks[I + 2];
            if (next1bis is not WLine line1bis)
                return;
            temp.Add(line1bis);
        } else {
            temp.Add(line1);
        }

        /* the last line must be a document number */
        if (I + temp.Count == Blocks.Count)
            return;
        IBlock next2 = Blocks[I + temp.Count];
        if (next2 is not WLine line2)
            return;
        if (!RegulationNumber.Is(line2.NormalizedContent))
            return;
        DocNumber2 docNumber2 = new DocNumber2 { Contents = line2.Contents };
        WLine newLine2 = WLine.Make(line2, new List<IInline>(1) { docNumber2 });
        temp.Add(newLine2);

        Enriched.AddRange(temp);
        I += temp.Count;
    }

}

}
