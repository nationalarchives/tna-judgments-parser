
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Lawmaker
{

    class QuotedTextEnricher : LineEnricher
    {
        private string Pattern;

        public QuotedTextEnricher(string startPattern, string endPattern)
        {
            Pattern = @$"{startPattern}(?'contents'.+?)(?:{endPattern}|$)";
        }

        internal override Mod EnrichMod(Mod raw)
        {
            List<IBlock> enrichedBlocks = [];
            for (int i = 0; i < raw.Contents.Count; i++)
            {
                 // When a paragraph already belongs to a mod (due to a quoted structure), quoted text 
                 // elements (if any) must be added to the existing mod, rather than causing additional 
                 // mod elements to be created.
                if (raw.Contents[i] is WLine line)
                {
                    WLine enrichedLine = Enrich(line);
                    enrichedBlocks.Add(enrichedLine);
                }
                // Must enrich the divisions inside quoted structures
                else if (raw.Contents[i] is BlockQuotedStructure qs)
                {
                    EnrichDivisions(qs.Contents);
                    enrichedBlocks.Add(qs);
                }
                else
                {
                    enrichedBlocks.Add(raw.Contents[i]);
                }
            }
            return new Mod() { Contents = enrichedBlocks };
        }

        /*
         * A wrapper for the 'Enrich' method that wraps the given line in a 
         * Mod element if it contains any QuotedText elements. 
         */
        internal override IBlock EnrichLine(WLine raw)
        {
            if (raw.NormalizedContent.StartsWith('\u201C'))
                return raw;

            WLine enriched = Enrich(raw);
            if (!ReferenceEquals(enriched, raw))
                return new Mod() { Contents = [enriched] };
            return raw;
        }

        /*
         * Identifies and extracts any QuotedText instances from the plaintext of a given line. 
         */
        internal WLine Enrich(WLine raw)
        {
            if (raw.Contents.Count() == 0 || raw.Contents.Last() is not WText current)
                return raw;
            string text = current.Text;
            MatchCollection matches = Regex.Matches(text, Pattern);
            if (matches.Count == 0)
                return raw;

            int charIndex = 0;
            List<IInline> enrichedInlines = raw.Contents.SkipLast(1).ToList();
            foreach (Match match in matches)
            {
                if (match.Index > charIndex)
                    enrichedInlines.Add(new WText(text[charIndex..match.Index], current.properties));
                charIndex = match.Index + match.Length;
                enrichedInlines.Add(constructQuotedText(match, current.properties));
            }
            if (charIndex != text.Length)
                enrichedInlines.Add(new WText(text[charIndex..], current.properties));
            return WLine.Make(raw, enrichedInlines);
        }

        IInline constructQuotedText(Match match, RunProperties props)
        {
            string startQuote = match.Groups["startQuote"].Value;
            string contents = match.Groups["contents"].Value;
            string endQuote = match.Groups["endQuote"].Value;
            return new QuotedText() { Contents = [new WText(contents, props)], StartQuote = startQuote, EndQuote = endQuote };
        }

    }

}
