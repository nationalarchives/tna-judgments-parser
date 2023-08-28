
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

class HeaderSplitter {

    internal static List<IBlock> Split(IEnumerable<BlockWithBreak> blocks) {
        return Split(blocks.Select(bb => bb.Block));
    }
    internal static List<IBlock> Split(IEnumerable<IBlock> blocks) {
        var enricher = new HeaderSplitter(blocks);
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

    private readonly IEnumerable<IBlock> Blocks;

    private readonly List<IBlock> Enriched = new List<IBlock>(3);

    private HeaderSplitter(IEnumerable<IBlock> blocks) {
        Blocks = blocks;
    }

    private void Enrich() {
        foreach (var block in Blocks)
            switch (state) {
                case State.Start:
                    Start(block);
                    break;
                case State.AfterDocType:
                    AfterDocType(block);
                    break;
                case State.AfterRegulationTitle:
                    AfterRegulationTitle(block);
                    break;
                case State.AfterDocNumber:
                    AfterDocNumber(block);
                    break;
                case State.Done:
                    return;
                case State.Fail:
                    Enriched.Clear();
                    return;
                default:
                    throw new System.Exception();
            }
    }

    string[] Titles = { "Explanatory Memorandum To", "Policy Note" };

    internal static string GetDocumentType(List<IBlock> header) {
        Model.DocType2 docType = Util.Descendants<Model.DocType2>(header).FirstOrDefault();
        if (docType is null)
            return null;
        string name = IInline.ToString(docType.Contents);
        name = Regex.Replace(name, @"\s+", " ").Trim();
        if ("Explanatory Memorandum To".Equals(name, System.StringComparison.InvariantCultureIgnoreCase))
            return "ExplanatoryMemorandum";
        if ("Policy Note".Equals(name, System.StringComparison.InvariantCultureIgnoreCase))
            return "PolicyNote";
        return null;
    }

    private void Start(IBlock block) {
        if (block is not WLine line) {
            state = State.Fail;
            return;
        }
        if (line is WOldNumberedParagraph) {
            state = State.Fail;
            return;
        }
        bool isTitle = Titles.Any(title => title.Equals(line.NormalizedContent, System.StringComparison.InvariantCultureIgnoreCase));
        if (!isTitle) {
            state = State.Fail;
            return;
        }
        Model.DocType2 docType = new Model.DocType2 { Contents = line.Contents };
        WLine newLine = WLine.Make(line, new List<IInline>(1) { docType });
        Enriched.Add(newLine);
        state = State.AfterDocType;
    }

    private void AfterDocType(IBlock block) {
        if (block is not WLine line) {
            state = State.Fail;
            return;
        }
        Enriched.Add(line);
        state = State.AfterRegulationTitle;
    }

    private void AfterRegulationTitle(IBlock block) {
        if (block is not WLine line) {
            state = State.Fail;
            return;
        }
        if (!RegulationNumber.Is(line.NormalizedContent)) {
            state = State.Fail;
            return;
        }
        Model.DocNumber2 docNumber = new Model.DocNumber2 { Contents = line.Contents };
        WLine newLine = WLine.Make(line, new List<IInline>(1) { docNumber });
        Enriched.Add(newLine);
        state = State.AfterDocNumber;
    }

    internal static string GetDocumentNumber(List<IBlock> header) {
        Model.DocNumber2 docNumber = Util.Descendants<Model.DocNumber2>(header).FirstOrDefault();
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
        state = State.Done;
    }

}

}
