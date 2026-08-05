using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

/// <summary>
/// Enriches lines of text by identifying and annotating judge names
/// </summary>
internal class JudgeNames : Enricher
{
    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        List<IBlock> enriched = [];
        var i = 0;
        var foundStart = false;
        while (!foundStart && i < blocks.Count())
        {
            var block = blocks.ElementAt(i);
            if (BeforePlusJudgeName(block))
            {
                var x = EnrichBeforePlusJudgeName(block);
                enriched.Add(x);
                enriched.AddRange(blocks.Skip(i + 1));
                return enriched;
            }

            if (IsDecidedBy(block))
            {
                foundStart = true;
            }

            enriched.Add(block);
            i += 1;
        }

        if (!foundStart)
        {
            return blocks;
        }

        if (i == blocks.Count())
        {
            return blocks;
        }

        var next = blocks.ElementAt(i);
        if (next is not WLine line)
        {
            return blocks;
        }

        if (!IsJudgeName(line))
        {
            return blocks;
        }

        var rich = WLine.Make(line, line.Contents.Cast<WText>().Select(t => new WJudge(t)));
        enriched.Add(rich);
        i += 1;
        enriched.AddRange(blocks.Skip(i));
        return enriched;
    }

    private static bool IsDecidedBy(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

        if (line.Contents.Count() != 1)
        {
            return false;
        }

        if (line.Contents.First() is not WText text)
        {
            return false;
        }

        if (text.Text.TrimEnd(':') == "Before")
        {
            return true;
        }

        return text.Text.TrimEnd(':') == "Decided by";
    }

    private static bool IsJudgeName(WLine line)
    {
        var normal = line.NormalizedContent;

        var isJudgeName =
            normal.StartsWith("Tribunal Judge", StringComparison.InvariantCultureIgnoreCase)
            || normal.StartsWith("Upper Tribunal Judge", StringComparison.InvariantCultureIgnoreCase)
            || normal.StartsWith("judge of the first-tier tribunal", StringComparison.InvariantCultureIgnoreCase);

        return isJudgeName;
    }

    private static bool BeforePlusJudgeName(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

        if (line.Contents.Count() == 1)
        {
            if (line.NormalizedContent.StartsWith("Before: Tribunal Judge",
                    StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (line.NormalizedContent.StartsWith("Before: Upper Tribunal Judge",
                    StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            return false;
        }

        if (line.Contents.Count() == 3)
        {
            if (line.Contents.First() is not WText wText1 ||
                !wText1.Text.Trim().Equals("Before:", StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            if (line.Contents.ElementAt(1) is not WTab)
            {
                return false;
            }

            if (line.Contents.ElementAt(2) is not WText wText3)
            {
                return false;
            }

            if (wText3.Text.TrimStart().StartsWith("Tribunal Judge", StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (wText3.Text.TrimStart().StartsWith("Upper Tribunal Judge", StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            return false;
        }

        return false;
    }

    private WLine EnrichBeforePlusJudgeName(IBlock block)
    {
        var old = block as WLine;
        if (old.Contents.Count() == 1)
        {
            return EnrichFromEnd.Enrich(old, @"Before:\s+(.+)", (a, b) => new WJudge(a, b));
        }

        if (old.Contents.Count() == 3)
        {
            var third = old.Contents.ElementAt(2) as WText;
            WJudge judge = new(third.Text, third.properties);
            return WLine.Make(old, old.Contents.Take(2).Append(judge));
        }

        return old;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        throw new NotImplementedException();
    }
}
