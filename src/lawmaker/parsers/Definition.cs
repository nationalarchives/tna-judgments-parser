
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseDefinition(WLine line)
        {
            if (line is WOldNumberedParagraph)
                return null;
            if (!IsLeftAligned(line))
                return null;

            string text = line.NormalizedContent;

            string startQuote = "[\u201C]";
            string endQuote = "[\u201D]";
            string defPattern = $@"({startQuote}.*?{endQuote})";

            string definitionPattern;
            if (quoteDepth > 1)
                definitionPattern = $@"^{startQuote}?{defPattern}.*\w+.*$";
            else
                definitionPattern = $@"^{defPattern}.*\w+.*$";
            if (!Regex.IsMatch(text, definitionPattern))
                return null;

            // Use enricher to create <def> element around defined term
            static IInline constructor(string text, DocumentFormat.OpenXml.Wordprocessing.RunProperties props)
            {
                WText wText = new(text[1..^1], props);
                return new Def() { Contents = [wText], StartQuote = text[..1], EndQuote = text[^1..] };
            }
            WLine enriched = EnrichFromBeginning.Enrich(line, defPattern, constructor);
            if (ReferenceEquals(enriched, line))
                return null;

            i += 1;

            List<IBlock> intro = [enriched];

            if (i == Document.Body.Count)
            {
                return new DefinitionLeaf { Contents = intro };
            }

            List<IDivision> children = [];

            while (i < Document.Body.Count)
            {
                if (CurrentLineIsIndentedLessThan(line))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (next is not Para1)
                {
                    i = save;
                    break;
                }
                children.Add(next);
            }
            if (children.Count == 0)
            {
                return new DefinitionLeaf { Contents = intro };
            }
            else
            {
                return new DefinitionBranch { Intro = intro, Children = children };
            }
        }

    }

}
