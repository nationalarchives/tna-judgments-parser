
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.Legislation.Lawmaker
{

    class QuotedTextEnricher
    {

        internal static void EnrichDivisions(List<IDivision> divisions)
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
            for (int i = 0; i < leaf.Contents.Count - 1; i++)
            {
                if (leaf.Contents[i] is not WLine line)
                    continue;
                if (leaf.Contents[i+1] is not BlockQuotedStructure qs)
                    continue;
                if (qs.StartQuote is not null)
                    continue;
                if (line.NormalizedContent.StartsWith('“'))
                    continue;
                string pattern = "(“[^“”]+)$";
                QuotedText qt = null;
                IInline constructor(string text, DocumentFormat.OpenXml.Wordprocessing.RunProperties props)
                {
                    WText wText = new(text[1..], props);
                    qt = new QuotedText() { Contents = [wText], StartQuote = text[..1] };
                    return qt;
                }
                WLine enriched = EnrichFromEnd.Enrich(line, pattern, constructor);
                if (ReferenceEquals(enriched, line))  // means there was no match found
                    continue;
                if (qt is null)  // should be impossible
                    continue;
                InlineQuotedStructure qs2 = new() {
                    Contents = qs.Contents,
                    StartQuote = qs.StartQuote,
                    EndQuote = qs.EndQuote
                };
                List<IInline> contents = [.. enriched.Contents.SkipLast(1), qt, qs2];
                if (qs.AppendText is not null)
                    contents.Add(qs.AppendText);
                //Mod mod = new() { Contents = contents };
               // WLine combined = WLine.Make(enriched, [mod]);
                //leaf.Contents.RemoveAt(i);
                //leaf.Contents.RemoveAt(i);
                //leaf.Contents.Insert(i, combined);
                break;
            }
        }

        internal static void EnrichBranch(Branch branch)
        {
            foreach (var child in branch.Children)
                EnrichDivision(child);
        }

    }

}
