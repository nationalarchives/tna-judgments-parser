
#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker;

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
            while (i < Body.Count - 10)
            {
                List<IBlock> blocks = [];
                blocks.Add(Body[i]);
                blocks.Add(Body[i + 1]);
                blocks.Add(Body[i + 2]);

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
         Ordering of the header:
         Header = CoverPage?, Preface, Preamble
         */
        private void ParseSecondaryHeader()
        {
            bool foundContents = false;
            bool isWelshSecondary = frames.CurrentDocName.IsWelshSecondary();

            // coverPage and preface share many elements.
            // We always expect a preface so we assume the last banner
            // before the start of the body indicates the preface
            // note: the preface can also start with a ProceduralRubric
            // or a CorrectionRubric
            IBlock? lastBannerInBody = Body
                .TakeWhile(block => PeekBodyStartProvision(block as WLine) is null)
                .OfType<WLine>()
                .Where(Preface.Banner.IsBanner)
                .LastOrDefault();
            IBlock? lastBannerPassed = null;
            // If we encounter a provision heading (outside of the ToC),
            // then the body must have started.
            while (i < Body.Count && PeekBodyStartProvision() is null)
            {
                IBlock block = Body[i];
                if (Preface.Banner.IsBanner(block as WLine))
                {
                    lastBannerPassed = block;
                }
                if (isWelshSecondary && ParseWelshBlockContainer() is BlockContainer blockContainer
                    && lastBannerPassed != lastBannerInBody) // intentional referential equality
                {
                    coverPage.Add(blockContainer);
                    continue;
                }

                if (!foundContents)
                    foundContents = Match(TableOfContents.Parse) is not null;



                if (Current() is not WLine line)
                {
                    i += 1;
                    continue;
                }

                if (Preamble.IsStart(line))
                {
                    preamble.Add(line);
                    i += 1;
                } else if (!foundContents && (Match(Preface.SIPreface.Parse) is IBlock prefaceBlock))
                {
                    // SIPreface.Parse appropriately increments i
                    preface.Add(prefaceBlock);
                } else
                {
                    i += 1;
                }

            }
            if (PeekBodyStartProvision() != null)
            {
                return;
            }
            // we haven't found the body or we've misparsed the body as the heading
            coverPage.Clear();
            preface.Clear();
            preamble.Clear();
            i = 0;
        }

        private BlockContainer? ParseWelshBlockContainer()
        {
            ExplanatoryNote? explanatoryNote = ParseExplanatoryNote();
            if (explanatoryNote is not null)
                return explanatoryNote;

            CommencementHistory? commencementHistory = ParseCommencementHistory();
            if (commencementHistory is not null)
                return commencementHistory;

            return null;
        }
    }

public class Preamble
{
    internal static bool IsStart(WLine line) =>
        line.IsLeftAligned()
            && line.IsFlushLeft()
            && !line.IsAllItalicized();
}