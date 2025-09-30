
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
                if (enriched.Contents.Any(content => content is QuotedText))
                    return new Mod() { Contents = [enriched] };
                else
                    return enriched;
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
                if (IsQuotedText(text, matches))
                    enrichedInlines.Add(ConstructQuotedText(match, current.properties));
                // If not quotedText then parse as a defined term, //def
                else
                    enrichedInlines.Add(ConstructDef(match, current.properties));
            }
            if (charIndex != text.Length)
                enrichedInlines.Add(new WText(text[charIndex..], current.properties));
            return WLine.Make(raw, enrichedInlines);
        }

        static IInline ConstructQuotedText(Match match, RunProperties props)
        {
            string startQuote = match.Groups["startQuote"].Value;
            string contents = match.Groups["contents"].Value;
            string endQuote = match.Groups["endQuote"].Value;
            return new QuotedText() { Contents = [new WText(contents, props)], StartQuote = startQuote, EndQuote = endQuote };
        }
        
        static IInline ConstructDef(Match match, RunProperties props)
        {
            string startQuote = match.Groups["startQuote"].Value;
            string contents = match.Groups["contents"].Value;
            string endQuote = match.Groups["endQuote"].Value;
            return new Def() { Contents = [new WText(contents, props)], StartQuote = startQuote, EndQuote = endQuote };
        }

        /// <summary>
        /// Combines all non-matched parts into one string to check if it contains "leave out", "substitute", "omit" or "insert"
        /// If it doesn't then return false
        /// </summary>
        static bool IsQuotedText(string text, MatchCollection matches)
        {
            string nonMatchedParts = "";
            int lastIndex = 0;
            foreach (Match match in matches)
            {
                // Add the text before the current match
                if (match.Index > lastIndex)
                    nonMatchedParts += text[lastIndex..match.Index];
                // Update lastIndex to the end of the current match
                lastIndex = match.Index + match.Length;
            }
            // Add any remaining text after the last match
            if (lastIndex < text.Length)
                nonMatchedParts += text[lastIndex..];
            List<string> words = ["leave out", "substitute", "omit", "insert"];

            return words.Any(word => nonMatchedParts.Contains(word, System.StringComparison.InvariantCultureIgnoreCase));
        }

    }

}
