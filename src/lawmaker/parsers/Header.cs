
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Lawmaker.Headers;

namespace UK.Gov.Legislation.Lawmaker;

    public partial class LegislationParser
    {


        private IHeader? header;

        private void ParseAndEnrichHeader()
        {
            if (frames.IsSecondaryDocName())
            {
                ParseSecondaryHeader();
            }
            else
            {
                try
                {
                    ParsePrimaryHeader();
                } catch (NotImplementedException)
                {
                    Logger.LogError("{} not supported for header parsing", frames.CurrentDocName);
                }
                EnrichPrimaryHeader();
            }

        }

        /*
         * Parses the header of a piece of primary legislation (i.e. a Bill or Act).
         */
        private void ParsePrimaryHeader()
        {
            header = frames.CurrentDocName switch
            {
                DocName.NIPUBB => Match(NIHeader.Parse),
                DocName.SPPUBB or DocName.SPPRIB or DocName.SPHYBB => Match(SPHeader.Parse),
                DocName.UKPUBB or DocName.UKHYBB => Match(UKHeader.Parse(Preamble.BeItEnacted, UKPreface.Parse)),
                DocName.UKPRIB => Match(UKHeader.Parse(Preamble.MayItTherefore, UKPreface.Parse)),
                DocName.UKCM or DocName.UKDCM => Match(CMHeader.Parse),
                DocName.SCPUBB or DocName.SCPRIB or DocName.SCHYBB => Match(UKHeader.Parse(Preamble.HavingPassedSeneddCymru, SCPreface.Parse)),

                _ => throw new NotImplementedException(),
            };
        }

        private void EnrichPrimaryHeader()
        {
            EnrichCoverPage();
        }

        private void EnrichCoverPage()
        {
            if (header is not NIHeader niHeader)
            {
                return;
            }
            if (niHeader.CoverPage?.Blocks.Count() == 0)
                return;
            if (niHeader.CoverPage?.Blocks.FirstOrDefault() is not WLine first)
                return;
            if (!first.NormalizedContent.EndsWith(" Bill"))
                return;
            ShortTitle shortTitle = new() { Contents = [.. first.Contents] };
            WLine replacement = WLine.Make(first, [shortTitle]);
            header = niHeader with {
                CoverPage = new NICoverPage(niHeader.CoverPage.Blocks.Skip(1).Prepend(replacement))
            };
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

            List<IBlock> coverPage = [];
            List<IBlock> preamble = [];
            List<IBlock> preface = [];

            // coverPage and preface share many elements.
            // We always expect a preface so we assume the last banner
            // before the start of the body indicates the preface
            // note: the preface can also start with a ProceduralRubric
            // or a CorrectionRubric
            IBlock? lastBannerInBody = Body
                .TakeWhile(block => PeekBodyStartProvision(block as WLine) is null)
                .OfType<WLine>()
                .Where(Headers.Banner.IsBanner)
                .LastOrDefault();
            IBlock? lastBannerPassed = null;
            // If we encounter a provision heading (outside of the ToC),
            // then the body must have started.
            while (i < Body.Count && PeekBodyStartProvision() is null)
            {
                IBlock block = Body[i];
                if (Headers.Banner.IsBanner(block as WLine))
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
                } else if (!foundContents && (Match(Headers.SIPreface.Parse) is IBlock prefaceBlock))
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
            header = new NIHeader(new NICoverPage(coverPage), new NIPreface(preface), new Headers.Preamble(preamble));
                return;
            }
            // we haven't found the body or we've misparsed the body as the heading
            i = 0;
        }

        private BlockContainer? ParseWelshBlockContainer()
        {
            ExplanatoryNote? explanatoryNote = Match(ExplanatoryNote.Parse);
            if (explanatoryNote is not null)
                return explanatoryNote;

            CommencementHistory? commencementHistory = ParseCommencementHistory();
            if (commencementHistory is not null)
                return commencementHistory;

            return null;
        }
    }
