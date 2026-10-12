using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.Enrichment;

internal class EnrichFromBeginning
{
    internal delegate IInline Constructor(IList<IInline> inlines);

    internal static WLine Enrich(WLine raw, string pattern, Constructor constructor)
    {
        var enriched = Enrich(raw.Contents, pattern, constructor);
        if (ReferenceEquals(enriched, raw.Contents))
        {
            return raw;
        }

        return WLine.Make(raw, enriched);
    }

    internal static IEnumerable<IInline> Enrich(IEnumerable<IInline> raw, string pattern, Constructor constructor)
    {
        var rawArray = raw.ToArray(); // enumerate to array to prevent multiple enumeration

        List<int> inlinePositions = [0];
        var beginning = "";
        Match match = null;

        foreach (var inlineElement in rawArray)
        {
            beginning += IInline.GetText(inlineElement);
            inlinePositions.Add(beginning.Length);
            match = Regex.Match(beginning, pattern);
            if (match.Success)
            {
                break;
            }
        }

        if (match == null)
        {
            return raw;
        }

        var group = match.Groups[1];
        var groupStart = group.Index;
        var groupEnd = group.Index + group.Length;

        // Collect Inlines before group
        var before = rawArray.TakeWhile((_, index) =>
            inlinePositions.Count > index + 1 && inlinePositions[index + 1] <= groupStart
        ).ToList();

        // Handle boundary condition where an Inline crosses the start of the group.
        // In which case, the Inline must be split.
        List<IInline> remainder = [];
        var inlineIndex = before.Count;
        var start = inlinePositions[inlineIndex];
        if (start < groupStart)
        {
            var inlineToSplit = (WText)rawArray[inlineIndex];
            var portionBefore = new WText(inlineToSplit.Text[..(groupStart - start)], inlineToSplit.properties);
            var portionAfter = new WText(inlineToSplit.Text[(groupStart - start)..], inlineToSplit.properties);
            inlinePositions.Insert(inlineIndex + 1, start + portionBefore.Text.Length);
            before.Add(portionBefore);
            remainder.Add(portionAfter);
        }

        remainder.AddRange(rawArray.Skip(before.Count));

        // Collect Inlines inside group
        var inside = remainder.TakeWhile((_, index) =>
        {
            var adjustedIndex = index + before.Count;
            return inlinePositions.Count > adjustedIndex + 1 && inlinePositions[adjustedIndex + 1] <= groupEnd;
        }).ToList();

        // Handle boundary condition where an Inline crosses the end of the group.
        // In which case, the Inline must be split.
        List<IInline> after = [];
        inlineIndex = before.Count + inside.Count;
        var end = inlinePositions.ElementAtOrDefault(inlineIndex + 1);
        if (end > groupEnd)
        {
            start = inlinePositions[inlineIndex];
            var inlineToSplit = remainder.Skip(inside.Count).OfType<WText>().FirstOrDefault();
            if (inlineToSplit is not null)
            {
                var portionBefore = new WText(inlineToSplit.Text[..(groupEnd - start)], inlineToSplit.properties);
                var portionAfter = new WText(inlineToSplit.Text[(groupEnd - start)..], inlineToSplit.properties);
                inside.Add(portionBefore);
                after.Add(portionAfter);
            }
        }

        // Collect Inlines after group
        after.AddRange(remainder.Skip(inside.Count));
        var enrichedInline = constructor(inside);
        if (enrichedInline == null)
        {
            return [.. before, .. after];
        }

        return [.. before, enrichedInline, .. after];
    }
}
