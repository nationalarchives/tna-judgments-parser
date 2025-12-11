
#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Lawmaker.Header;

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
            if (Match(NIHeader.Parse) is not NIHeader header)
            {
                return;
            }
            coverPage.AddRange(header?.CoverPage?.Blocks ?? []);
            preface.AddRange(header?.Preface?.Blocks ?? []);
            preamble.AddRange(header?.Preamble?.Blocks ?? []);

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
                .Where(Header.Banner.IsBanner)
                .LastOrDefault();
            IBlock? lastBannerPassed = null;
            // If we encounter a provision heading (outside of the ToC),
            // then the body must have started.
            while (i < Body.Count && PeekBodyStartProvision() is null)
            {
                IBlock block = Body[i];
                if (Header.Banner.IsBanner(block as WLine))
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
                    foundContents = Match(TableOfContents.Parse(_ => true)) is not null;



                if (Current() is not WLine line)
                {
                    i += 1;
                    continue;
                }

                if (Preamble.IsStart(line))
                {
                    preamble.Add(line);
                    i += 1;
                } else if (!foundContents && (Match(Header.SIPreface.Parse) is IBlock prefaceBlock))
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