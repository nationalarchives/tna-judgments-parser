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

        // Match against the fully assembled text rather than growing it run-by-run: a pattern
        // with an optional trailing component (e.g. "...\)?") could otherwise appear to succeed
        // before a later run (e.g. a closing ")" in its own run) has even been considered.
        var beginning = string.Concat(rawArray.Select(IInline.GetText));
        var match = Regex.Match(beginning, pattern);
        if (!match.Success)
        {
            return raw;
        }

        var group = match.Groups[1];
        return Enrich(rawArray, group.Index, group.Index + group.Length, constructor);
    }

    /// <summary>
    /// Wraps the characters from <paramref name="groupStart"/> to <paramref name="groupEnd"/> of the text of the
    /// inlines, splitting the runs at either end where the range starts or ends part-way through one
    /// </summary>
    internal static IInline[] Enrich(IInline[] rawArray, int groupStart, int groupEnd, Constructor constructor)
    {
        List<int> inlinePositions = [0];
        var length = 0;

        foreach (var inlineElement in rawArray)
        {
            length += IInline.GetText(inlineElement).Length;
            inlinePositions.Add(length);
        }

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
            // The start check excludes a zero-width Inline (e.g. a line break) that sits exactly
            // at the end of the group: it contributes no characters to the match, so it belongs
            // after the group, not inside it.
            return inlinePositions.Count > adjustedIndex + 1
                && inlinePositions[adjustedIndex] < groupEnd
                && inlinePositions[adjustedIndex + 1] <= groupEnd;
        }).ToList();

        // Handle boundary condition where an Inline crosses the end of the group.
        // In which case, the Inline must be split.
        List<IInline> after = [];
        inlineIndex = before.Count + inside.Count;
        start = inlinePositions[inlineIndex];
        var end = inlinePositions.ElementAtOrDefault(inlineIndex + 1);
        if (start < groupEnd && end > groupEnd)
        {
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
