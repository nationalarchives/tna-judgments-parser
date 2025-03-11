
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.Legislation.Lawmaker
{

    class QuotedTextEnricher
    {

        internal static void EnrichDivisions(IList<IDivision> divisions)
        {
            foreach (var div in divisions)
                EnrichDivision(div);
        }

        internal static void EnrichDivision(IDivision division)
        {
            if (division is Leaf leaf)
                EnrichLeaf(leaf);
            else if (division is Branch branch)
                EnrichBranch(branch);
        }

        internal static void EnrichLeaf(Leaf leaf)
        {
            string pattern = @"(\u201C[^\u201C\u201D]+?(?:\u201D|$))";
            QuotedText qt = null;
            IInline constructor(string text, DocumentFormat.OpenXml.Wordprocessing.RunProperties props)
            {
                string endQuote = text.EndsWith("\u201D") ? "\u201D" : null;
                string contents = (endQuote == null) ? text[1..] : text[1..^1];
                WText wText = new(contents, props);
                qt = new QuotedText() { Contents = [wText], StartQuote = text[..1], EndQuote = endQuote };
                return qt;
            }

            for (int i = 0; i < leaf.Contents.Count; i++)
            {
                IBlock enriched;
                if (leaf.Contents[i] is WLine line && !line.NormalizedContent.StartsWith('\u201C'))
                    enriched = EnrichLine(line, pattern, constructor);
                else if (leaf.Contents[i] is Mod mod)
                    enriched = EnrichMod(mod, pattern, constructor);
                else
                    continue;

                if (qt is null)  // should be impossible
                    continue;
                leaf.Contents.RemoveAt(i);
                leaf.Contents.Insert(i, enriched);
                break;
            }
        }

        internal static void EnrichBranch(Branch branch)
        {
            foreach (var child in branch.Children)
                EnrichDivision(child);
        }

        internal static IBlock EnrichLine(WLine raw, string pattern, Constructor constructor)
        {
            IEnumerable<IInline> enriched = Enrich(raw.Contents, pattern, constructor);
            if (!ReferenceEquals(enriched, raw))
                return new Mod() { Contents = [WLine.Make(raw, enriched)] };
            return raw;
        }

        internal static Mod EnrichMod(Mod raw, string pattern, Constructor constructor)
        {
            List<IBlock> enrichedBlocks = [];
            for (int i = 0; i < raw.Contents.Count; i++)
            {
                 // When a paragraph already belongs to a mod (due to a quoted structure), quoted text 
                 // elements (if any) must be added to the existing mod, rather than causing additional 
                 // mod elements to be created.
                if (raw.Contents[i] is WLine line)
                {
                    IEnumerable<IInline> enrichedInlines = Enrich(line.Contents, pattern, constructor);
                    WLine enrichedLine = WLine.Make(line, enrichedInlines);
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

        internal static IEnumerable<IInline> Enrich(IEnumerable<IInline> raw, string pattern, Constructor constructor)
        {
            if (raw.Last() is not WText current)
                return raw;

            MatchCollection matches = Regex.Matches(current.Text, pattern);
            string[] segments = Regex.Split(current.Text, pattern);

            if (segments.Count() == 1)
                return raw;

            List<IInline> enrichedInlines = [];
            foreach (string segment in segments)
            {
                if (Regex.IsMatch(segment, pattern))
                    enrichedInlines.Add(constructor(segment, current.properties));
                else
                    enrichedInlines.Add(new WText(segment, current.properties));
            }
            return enrichedInlines;
        }

    }

}
