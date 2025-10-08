
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Lawmaker
{

    /// <summary>
    /// Identifies strings of text which are flanked by quotation marks, 
    /// and encapsulates them in either <c>QuotedText</c> or <c>Def</c> elements.
    /// </summary>
    /// <remarks>
    /// <c>QuotedText</c> elements are created when a quoted string is 'amending'. Which is to say, 
    /// preceded by a phrase such as 'substitute', 'insert', 'leave out', or 'omit'. 
    /// Otherwise, a quoted string is assumed to be a definition, and a <c>Def</c> element is created.
    /// </remarks>
    class QuotationEnricher : LineEnricher
    {
        private string Pattern;
        private LanguageService LangService;

        public QuotationEnricher(LanguageService langService, string startPattern, string endPattern)
        {
            LangService = langService;
            Pattern = @$"{startPattern}(?'contents'.+?)(?:{endPattern}|$)";
        }

        /// <summary> Enriches the given <paramref name="mod"/>.</summary>
        /// <param name="mod">The mod to enrich.</param>
        /// <returns>The enriched mod.</returns>
        internal override Mod EnrichMod(Mod mod)
        {
            List<IBlock> enrichedBlocks = [];
            for (int i = 0; i < mod.Contents.Count; i++)
            {
                // When a paragraph already belongs to a mod (due to a quoted structure), quoted text 
                // elements (if any) must be added to the existing mod, rather than causing additional 
                // mod elements to be created.
                if (mod.Contents[i] is WLine line)
                {
                    WLine enrichedLine = Enrich(line);
                    enrichedBlocks.Add(enrichedLine);
                }
                // Must enrich the divisions inside quoted structures
                else if (mod.Contents[i] is BlockQuotedStructure qs)
                {
                    EnrichDivisions(qs.Contents);
                    enrichedBlocks.Add(qs);
                }
                else
                {
                    enrichedBlocks.Add(mod.Contents[i]);
                }
            }
            return new Mod() { Contents = enrichedBlocks };
        }

        /// <summary>
        /// A wrapper for the <c>QuotationEnricher.Enrich</c> method which wraps the enriched <paramref name="line"/> 
        /// in a <c>Mod</c> element if it has any <c>QuotedText</c> children.
        /// </summary>
        /// <remarks>
        /// Like <c>QuotedStructure</c> elements, <c>QuotedText</c> elements are used to amend other pieces of legislation. 
        /// Lines contanining either of these elements must be wrapped in a <c>Mod</c> element to acknowledge that
        /// they are 'modifications'.
        /// </remarks>
        /// <param name="line">The line to enrich.</param>
        /// <returns>The enriched <c>IBlock</c>. Either a <c>WLine</c> or a <c>Mod</c> element.</returns>
        internal override IBlock EnrichLine(WLine line)
        {
            if (line.NormalizedContent.StartsWith('\u201C'))
                return line;

            WLine enriched = Enrich(line);
            if (!ReferenceEquals(enriched, line))
                if (enriched.Contents.Any(content => content is QuotedText))
                    return new Mod() { Contents = [enriched] };
                else
                    return enriched;
                return line;
        }

        /// <summary> Enriches the given <paramref name="line"/>.</summary>
        /// <param name="line">The line to enrich.</param>
        /// <returns>The enriched line.</returns>
        internal WLine Enrich(WLine line)
        {
            // TODO: This only enriches the FINAL WText inline element.
            // We need this to enrich all inline children of the line.
            if (!line.Contents.Any() || line.Contents.Last() is not WText current)
                return line;
            string text = current.Text;
            MatchCollection matches = Regex.Matches(text, Pattern);
            if (matches.Count == 0)
                return line;

            int charIndex = 0;
            List<IInline> enrichedInlines = line.Contents.SkipLast(1).ToList();

            foreach (Match match in matches)
            {
                if (match.Index > charIndex)
                    enrichedInlines.Add(new WText(text[charIndex..match.Index], current.properties));
                charIndex = match.Index + match.Length;
                if (IsQuotedText(text, matches))
                    enrichedInlines.Add(ConstructQuotedText(match, current.properties));
                // If not QuotedText then parse as a defined term (Def)
                else
                    enrichedInlines.Add(ConstructDef(match, current.properties));
            }
            if (charIndex != text.Length)
                enrichedInlines.Add(new WText(text[charIndex..], current.properties));
            return WLine.Make(line, enrichedInlines);
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
        /// Combines all non-matched parts into one string to check if it contains 'insert', 'leave out', 'omit' or 'substitute'.
        /// If at least one of the phrases are present, then the match should be treated as <c>QuotedText</c>
        /// </summary>
        bool IsQuotedText(string text, MatchCollection matches)
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

            return LangService.IsMatch(nonMatchedParts, QuotedText.AmendingPrefixes);
        }

    }

}