using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parsers;

namespace UK.Gov.NationalArchives.CaseLaw.PressSummaries;

internal class Header
{
    internal static List<IBlock> Split(List<BlockWithBreak> contents)
    {
        var blocks = contents.Select(bb => bb.Block).ToList();
        var splitter = new Header(blocks);
        splitter.Split();
        return splitter.Enriched;
    }

    private readonly List<IBlock> All;

    private Header(List<IBlock> blocks)
    {
        All = blocks;
        lineDateEnricher = new LineDateEnricher();
    }

    private enum State
    {
        Start,
        AfterDateBeforeDocType, AfterDocTypeBeforeDate, // date and docType can be in either order
        AfterDateAndDocTypeBeforeCite, // cite is always after both date and docType
        AfterCiteBeforeOnAppealFrom,
        AfterOnAppealFromBeforeJustices,
        Done, // haven't necessarily found all but stop looking
        Fail
    }

    private readonly List<IBlock> Enriched = new(8);

    private State state = State.Start;

    private int Idx;
    private readonly ILineDateEnricher lineDateEnricher;

    private void Split()
    {
        while (Idx < All.Count - 1)
        {
            var block = All[Idx];
            switch (state)
            {
                case State.Start:
                    Start(block);
                    break;
                case State.AfterDateBeforeDocType:
                    AfterDateBeforeDocType(block);
                    break;
                case State.AfterDocTypeBeforeDate:
                    AfterDocTypeBeforeDate(block);
                    break;
                case State.AfterDateAndDocTypeBeforeCite:
                    AfterDateAndDocTypeBeforeCite(block);
                    break;
                case State.AfterCiteBeforeOnAppealFrom:
                    AfterCiteBeforeOnAppealFrom(block);
                    break;
                case State.AfterOnAppealFromBeforeJustices:
                    AfterOnAppealFromBeforeJustices(block);
                    break;
                case State.Done:
                    return;
                case State.Fail:
                    Enriched.Clear();
                    return;
                default:
                    throw new Exception();
            }

            Idx += 1;
        }

        Enriched.Clear();
    }

    private void Start(IBlock block)
    {
        if (block is not WLine line)
        {
            state = State.Fail;
            return;
        }

        if (line.NormalizedContent == "" && line.Contents.Any(i => i is WImageRef))
        {
            Enriched.Add(line);
            return;
        }

        if (Enricher.IsRestriction(line))
        {
            var restriction = new WRestriction(line);
            Enriched.Add(restriction);
            return;
        }

        var enriched1 = lineDateEnricher.Enrich(line, "release", 1);
        if (!ReferenceEquals(enriched1, line))
        {
            Enriched.Add(enriched1);
            state = State.AfterDateBeforeDocType;
            return;
        }

        enriched1 = Enricher.EnrichDocType(line);
        if (!ReferenceEquals(enriched1, line))
        {
            Enriched.Add(enriched1);
            state = State.AfterDocTypeBeforeDate;
            return;
        }

        state = State.Fail;
    }

    private void AfterDateBeforeDocType(IBlock block)
    {
        if (block is not WLine line)
        {
            state = State.Fail;
            return;
        }

        if (Enricher.IsRestriction(line))
        {
            var restriction = new WRestriction(line);
            Enriched.Add(restriction);
            return;
        }

        var enriched1 = Enricher.EnrichDocType(line);
        if (!ReferenceEquals(enriched1, line))
        {
            Enriched.Add(enriched1);
            state = State.AfterDateAndDocTypeBeforeCite;
            return;
        }

        state = State.Fail;
    }

    private void AfterDocTypeBeforeDate(IBlock block)
    {
        if (block is not WLine line)
        {
            Enriched.Add(block);
            state = State.Done;
            return;
        }

        if (Enricher.IsRestriction(line))
        {
            var restriction = new WRestriction(line);
            Enriched.Add(restriction);
            return;
        }

        var enriched1 = lineDateEnricher.Enrich(line, "release", 1);
        if (!ReferenceEquals(enriched1, line))
        {
            Enriched.Add(enriched1);
            state = State.AfterDateAndDocTypeBeforeCite;
            return;
        }

        // some have no date
        enriched1 = Enricher.EnrichCite(line);
        if (!ReferenceEquals(enriched1, line))
        {
            Enriched.Add(enriched1);
            state = State.AfterCiteBeforeOnAppealFrom;
            return;
        }

        if (Enricher.IsCaseName(line))
        {
            var title = WDocTitle2.ConvertContents(line);
            Enriched.Add(title);
            state = State.AfterDateAndDocTypeBeforeCite;
            return;
        }

        Enriched.Add(line);
        state = State.Done;
    }

    private void AfterDateAndDocTypeBeforeCite(IBlock block)
    {
        if (block is not WLine line)
        {
            Enriched.Add(block);
            state = State.Done;
            return;
        }

        if (Enricher.IsRestriction(line))
        {
            var restriction = new WRestriction(line);
            Enriched.Add(restriction);
            return;
        }

        var enriched1 = Enricher.EnrichCite(line);
        if (!ReferenceEquals(enriched1, line))
        {
            Enriched.Add(enriched1);
            state = State.AfterCiteBeforeOnAppealFrom;
            return;
        }

        if (Enricher.IsCaseName(line))
        {
            var title = WDocTitle2.ConvertContents(line);
            Enriched.Add(title);
            return;
        }

        if (NextLineIsCiteOnly())
        {
            // if the next line is the NCN, we'll assume the current line is the case name,
            // but only if the case name has not already been found
            if (CaseNameHasBeenFound())
            {
                Enriched.Add(line);
            }
            else
            {
                var title = WDocTitle2.ConvertContents(line);
                Enriched.Add(title);
            }

            return;
        }

        Enriched.Add(line);
        state = State.Done;
    }

    private bool NextLineIsCiteOnly()
    {
        var nextIdx = Idx + 1;
        if (nextIdx == All.Count)
        {
            return false;
        }

        var nextBlock = All[nextIdx];
        if (nextBlock is not WLine line)
        {
            return false;
        }

        return Enricher.IsCiteOnly(line);
    }

    private bool CaseNameHasBeenFound()
    {
        return Enriched.Where(block => block is WLine)
                       .Cast<WLine>()
                       .Where(line => line.Contents.Where(inline => inline is WDocTitle || inline is WDocTitle2).Any())
                       .Any();
    }

    private void AfterCiteBeforeOnAppealFrom(IBlock block)
    {
        if (block is not WLine line)
        {
            Enriched.Add(block);
            state = State.Done;
            return;
        }

        var enriched1 = Enricher.EnrichOnAppealFrom(line);
        if (!ReferenceEquals(enriched1, line))
        {
            Enriched.Add(enriched1);
            state = State.AfterOnAppealFromBeforeJustices;
            return;
        }

        if (line.NormalizedContent.StartsWith("On appeal from "))
        {
            // for NICA
            Enriched.Add(enriched1);
            state = State.AfterOnAppealFromBeforeJustices;
            return;
        }

        Enriched.Add(line);
        state = State.Done;
    }

    private void AfterOnAppealFromBeforeJustices(IBlock block)
    {
        if (block is not WLine line)
        {
            Enriched.Add(block);
            state = State.Done;
            return;
        }

        var enriched1 = Enricher.EnrichJustices(line);
        if (!ReferenceEquals(enriched1, line))
        {
            Enriched.Add(enriched1);
            state = State.Done;
            return;
        }

        Enriched.Add(line);
        state = State.Done;
    }
}
