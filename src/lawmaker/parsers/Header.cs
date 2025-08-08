
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private readonly List<IBlock> coverPage = [];

        private readonly List<IBlock> preface = [];

        private readonly List<IBlock> preamble = [];

        private void ParseAndEnrichHeader()
        {
            if (frames.IsSecondaryDocName())
            {
                ParseSecondaryHeader();
            }
            else
            {
                ParsePrimaryHeader();
                EnrichPrimaryHeader();
            }

        }

        /*
         * Parses the header of a piece of primary legislation (i.e. a Bill or Act).
         */
        private void ParsePrimaryHeader()
        {
            bool foundPreface = false;
            while (i < Document.Body.Count - 10)
            {
                List<IBlock> blocks = [];
                blocks.Add(Document.Body[i].Block);
                blocks.Add(Document.Body[i + 1].Block);
                blocks.Add(Document.Body[i + 2].Block);

                if (!foundPreface && blocks.All(b => b is WLine))
                {
                    IEnumerable<string> textContent = blocks
                        .Select(b => (b as WLine).TextContent)
                        .Select(s => Regex.Replace(s, @"\s", ""));

                    string longTitle = string.Join(" ", textContent).ToUpper();
                    if (longTitle == "A BILL TO")
                    {
                        preface.AddRange(blocks);
                        i += 3;
                        foundPreface = true;
                        continue;
                    }
                }
                if (foundPreface && blocks[0] is WLine line)
                {
                    string enactingText = Regex.Replace(line.TextContent, @"\s", "").ToUpper();
                    if (enactingText.StartsWith("BEITENACTEDBY"))
                    {
                        preamble.Add(blocks[0]);
                        i += 1;
                        return;
                    }
                }
                if (foundPreface)
                    preface.Add(blocks[0]);
                else
                    coverPage.Add(blocks[0]);
                i += 1;
            }
            coverPage.Clear();
            preface.Clear();
            preamble.Clear();
            i = 0;
        }

        private void EnrichPrimaryHeader()
        {
            EnrichCoverPage();
        }

        private void EnrichCoverPage()
        {
            if (coverPage.Count == 0)
                return;
            if (coverPage[0] is not WLine first)
                return;
            if (!first.NormalizedContent.EndsWith(" Bill"))
                return;
            ShortTitle shortTitle = new() { Contents = [.. first.Contents] };
            WLine replacement = WLine.Make(first, [shortTitle]);
            coverPage.RemoveAt(0);
            coverPage.Insert(0, replacement);
        }


        /*
         * Parses the header of Statutory Instrument (whether draft or enacted).
         */
        private void ParseSecondaryHeader()
        {
            bool foundContents = false;
            while (i < Document.Body.Count)
            {
                if (!foundContents)
                    foundContents = SkipTableOfContents();

                IBlock block = Document.Body[i].Block;

                // If we encounter a provision heading (outside of the ToC),
                // then the body must have started.
                HContainer peek = PeekBodyStartProvision();
                if (peek != null)
                    return;

                if (!(block is WLine line))
                {
                    i += 1;
                    continue;
                }

                if (IsLeftAligned(line) && IsFlushLeft(line))
                    preamble.Add(block);
                /* TODO: Handle the preface of Statutory Instruments (in an upcoming ticket)
                else if (!foundContents)
                    preface.Add(block);
                */

                i += 1;
            }
            coverPage.Clear();
            preface.Clear();
            preamble.Clear();
            i = 0;
        }

        /*
         * Identifies and skips over the Table of Contents.
         */
        private bool SkipTableOfContents()
        {
            // Identify 'CONTENTS' heading
            IBlock block = Document.Body[i].Block;
            if (!(block is WLine line))
                return false;
            if (!IsCenterAligned(line))
                return false;
            if (!line.NormalizedContent.ToUpper().Equals("CONTENTS"))
                return false;

            // Skip contents
            while (i < Document.Body.Count - 1)
            {
                i += 1;
                block = Document.Body[i].Block;
                if (!(block is WLine contentsLine))
                    break;

                // ToC Grouping provisions are center aligned
                if (IsCenterAligned(contentsLine))
                    continue;
                // ToC Prov1 elements are numbered
                if (contentsLine is WOldNumberedParagraph)
                    continue;
                // ToC Schedules (and associated grouping provisions) have hanging indents
                if (contentsLine.FirstLineIndentWithNumber < 0)
                    continue;

                break;
            }
            return true;
        }

    }

}
