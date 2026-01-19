
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.Enrichment
{

    class EnrichFromBeginning
    {

        internal delegate IInline Constructor(IList<IInline> inlines);

        internal static WLine Enrich(WLine raw, string pattern, Constructor constructor)
        {
            IEnumerable<IInline> enriched = Enrich(raw.Contents, pattern, constructor);
            if (ReferenceEquals(enriched, raw))
                return raw;
            return WLine.Make(raw, enriched);
        }

        internal static IEnumerable<IInline> Enrich(IEnumerable<IInline> raw, string pattern, Constructor constructor)
        {
            IEnumerator<IInline> enumerator = raw.GetEnumerator();
            List<int> inlinePositions = [0];
            string beginning = "";
            Match match = null;
            while (enumerator.MoveNext())
            {
                beginning += IInline.GetText(enumerator.Current);
                inlinePositions.Add(beginning.Length);
                match = Regex.Match(beginning, pattern);
                if (match.Success)
                    break;
            }
            if (match == null)
                return raw;

            Group group = match.Groups[1];
            int groupStart = group.Index;
            int groupEnd = group.Index + group.Length;

            List<IInline> before, inside, after;

            // Collect Inlines before group
            before = raw.TakeWhile((inline, index) =>
                inlinePositions.Count > index + 1 && inlinePositions[index + 1] <= groupStart
            ).ToList();

            // Handle boundary condition where an Inline crosses the start of the group.
            // In which case, the Inline must be split.
            List<IInline> remainder = [];
            int inlineIndex = before.Count;
            int start = inlinePositions[inlineIndex];
            if (start < groupStart)
            {
                WText inlineToSplit = raw.ElementAt(inlineIndex) as WText;
                WText portionBefore = new WText(inlineToSplit.Text[..(groupStart - start)], inlineToSplit.properties);
                WText portionAfter = new WText(inlineToSplit.Text[(groupStart - start)..], inlineToSplit.properties);
                inlinePositions.Insert(inlineIndex + 1, start + portionBefore.Text.Length);
                before.Add(portionBefore);
                remainder.Add(portionAfter);
            }
            remainder.AddRange(raw.Skip(before.Count));

            // Collect Inlines inside group
            inside = remainder.TakeWhile((inline, index) =>
            {
                int adjustedIndex = index + before.Count;
                return inlinePositions.Count > adjustedIndex + 1 && inlinePositions[adjustedIndex + 1] <= groupEnd;
            }).ToList();

            // Handle boundary condition where an Inline crosses the end of the group.
            // In which case, the Inline must be split.
            after = [];
            inlineIndex = before.Count + inside.Count;
            int end = inlinePositions.ElementAtOrDefault(inlineIndex + 1);
            if (end > groupEnd)
            {
                start = inlinePositions[inlineIndex];
                WText inlineToSplit = remainder.Skip(inside.Count).OfType<WText>().First();
                if (inlineToSplit is not null)
                {
                    WText portionBefore = new WText(inlineToSplit.Text[..(groupEnd - start)], inlineToSplit.properties);
                    WText portionAfter = new WText(inlineToSplit.Text[(groupEnd - start)..], inlineToSplit.properties);
                    inside.Add(portionBefore);
                    after.Add(portionAfter);
                }
            }

            // Collect Inlines after group
            after.AddRange(remainder.Skip(inside.Count));
            IInline enrichedInline = constructor(inside);
            if (enrichedInline == null)
                return [.. before, .. after];
            return [.. before, enrichedInline, .. after];
        }
    }

}
