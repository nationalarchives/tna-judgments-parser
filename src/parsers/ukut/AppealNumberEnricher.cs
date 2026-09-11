#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

internal partial class AppealNumberEnricher : Enricher
{
    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        var lineContents = line.ToArray();
        if (!lineContents.OfType<WText>().Any(t => AppealNumberRegex().IsMatch(t.Text)))
        {
            return line;
        }

        var enriched = new List<IInline>();

        foreach (var inline in lineContents)
        {
            if (inline is WText textToSplit && AppealNumberRegex().IsMatch(textToSplit.Text))
            {
                enriched.AddRange(SplitTextContainingAppealNumber(textToSplit));
            }
            else
            {
                enriched.Add(inline);
            }
        }

        return enriched;
    }

    private static List<IInline> SplitTextContainingAppealNumber(WText textToSplit)
    {
        var enriched = new List<IInline>();
        var currentTextIndex = 0;

        foreach (Match appealNumberMatch in AppealNumberRegex().Matches(textToSplit.Text))
        {
            if (appealNumberMatch.Index > currentTextIndex)
            {
                enriched.Add(new WText(textToSplit.Text[currentTextIndex..appealNumberMatch.Index],
                    textToSplit.properties));
            }

            enriched.Add(new WAppealNo(appealNumberMatch.Value, textToSplit.properties));

            currentTextIndex = appealNumberMatch.Index + appealNumberMatch.Length;
        }

        if (currentTextIndex < textToSplit.Text.Length)
        {
            enriched.Add(new WText(textToSplit.Text[currentTextIndex..], textToSplit.properties));
        }

        return enriched;
    }

    [GeneratedRegex(@"[A-Z]{2,4}[/-]\d{2,6}(?:[/-]\d{2,6})+(?:[/-][A-Z])?", RegexOptions.IgnoreCase)]
    private static partial Regex AppealNumberRegex();
}
