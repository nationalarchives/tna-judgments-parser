
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.Enrichment
{

    internal delegate IInline Constructor(string ncn, RunProperties props);

    class EnrichFromEnd
    {

        internal static IEnumerable<IInline> Enrich(IEnumerable<IInline> raw, string pattern, Constructor constructor)
        {
            IEnumerator<IInline> reversed = raw.Reverse().GetEnumerator();
            string end = "";
            while (reversed.MoveNext())
            {
                if (reversed.Current is not WText wText)
                    return raw;
                end = wText.Text + end;
                Match match = Regex.Match(end, pattern);
                if (match.Success)
                {
                    List<IInline> before = new();
                    List<IInline> replacement = new();
                    Group group = match.Groups[1];
                    if (group.Index > 0)
                    {
                        WText leading = new WText(end.Substring(0, group.Index), wText.properties);
                        before.Add(leading);
                    }
                    IInline middle = constructor(group.Value, wText.properties);
                    replacement.Add(middle);
                    if (group.Index + group.Length < end.Length)
                    {
                        WText trailing = new WText(end.Substring(group.Index + group.Length), (raw.Last() as WText).properties);
                        replacement.Add(trailing);
                    }
                    while (reversed.MoveNext())
                        before.Insert(0, reversed.Current);
                    return Enumerable.Concat(before, replacement);
                }
            }
            return raw;
        }

    }

}
