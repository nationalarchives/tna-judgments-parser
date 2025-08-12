
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.ImpactAssessments {

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

    private readonly List<IBlock> Blocks;

    private int I = 0;

    private readonly List<IBlock> Enriched = new List<IBlock>(3);

    private HeaderSplitter(IEnumerable<IBlock> blocks) {
        Blocks = blocks is List<IBlock> list ? list : new List<IBlock>(blocks);
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
    }

    readonly string[] Titles = { "Impact Assessment" };

    internal static string GetDocumentType(List<IBlock> header) {
        Model.DocType2 docType = Util.Descendants<Model.DocType2>(header).FirstOrDefault();
        if (docType is null)
            return null;
        string name = IInline.ToString(docType.Contents);
        name = Regex.Replace(name, @"\s+", " ").Trim();
        if ("Impact Assessment".Equals(name, System.StringComparison.InvariantCultureIgnoreCase))
            return "ImpactAssessment";
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
        bool isTitle = Titles.Any(title => title.Equals(line1.NormalizedContent, System.StringComparison.InvariantCultureIgnoreCase));
        if (isTitle) {
            Model.DocType2 docType2 = new Model.DocType2 { Contents = line1.Contents };
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
        Model.DocNumber2 docNumber2 = new Model.DocNumber2 { Contents = line2.Contents };
        WLine newLine2 = WLine.Make(line2, new List<IInline>(1) { docNumber2 });
        temp.Add(newLine2);

        Enriched.AddRange(temp);
        I += temp.Count;
    }

}

}
