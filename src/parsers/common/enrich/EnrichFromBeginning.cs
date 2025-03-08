
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.Enrichment
{

    class EnrichFromBeginning
    {

        internal static WLine Enrich(WLine raw, string pattern, Constructor constructor)
        {
            IEnumerable<IInline> enriched = Enrich(raw.Contents, pattern, constructor);
            if (ReferenceEquals(enriched, raw))
                return raw;
            return WLine.Make(raw, enriched);
        }

        internal static IEnumerable<IInline> Enrich(IEnumerable<IInline> raw, string pattern, Constructor constructor)
        {
            IEnumerator<IInline> enumerator = raw.Reverse().GetEnumerator();
            string beginning = "";
            while (enumerator.MoveNext())
            {
                if (enumerator.Current is not WText wText)
                    return raw;
                beginning += wText.Text;
                Match match = Regex.Match(beginning, pattern);
                if (!match.Success)
                    continue;
                List<IInline> replacement = [];
                Group group = match.Groups[1];
                if (group.Index > 0)
                {
                    string before = beginning[..group.Index];
                    WText leading = new(before, wText.properties);
                    replacement.Add(leading);
                }
                IInline middle = constructor(group.Value, wText.properties);
                replacement.Add(middle);
                if (group.Index + group.Length < beginning.Length)
                {
                    string after = beginning[(group.Index + group.Length)..];
                    WText trailing = new(after, wText.properties);
                    replacement.Add(trailing);
                }
                while (enumerator.MoveNext())
                    replacement.Add(enumerator.Current);
                return replacement;
            }
            return raw;
        }

    }

}
