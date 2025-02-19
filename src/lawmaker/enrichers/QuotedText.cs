
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
                if (leaf.Contents[i+1] is not QuotedStructure qs)
                    continue;
                if (qs.StartQuote is not null)
                    continue;
                if (line.NormalizedContent.StartsWith('“'))
                    continue;
                string pattern = "(“[^“”]+)$";
                static IInline constructor(string text, DocumentFormat.OpenXml.Wordprocessing.RunProperties props)
                {
                    WText wText = new(text[1..], props);
                    return new QuotedText() { Contents = [wText], StartQuote = text[..1] };
                }
                WLine enriched = EnrichFromEnd.Enrich(line, pattern, constructor);
                if (ReferenceEquals(enriched, line))
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

    }

}
