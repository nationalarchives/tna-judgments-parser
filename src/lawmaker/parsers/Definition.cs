
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Bibliography;
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

            string startQuote = "[“]";
            string endQuote = "[”]";
            string defPattern = $@"({startQuote}.*?{endQuote})";

            String text = line.NormalizedContent;
            int startQuoteCount = text.Count(c => c == '“');
            int endQuoteCount = text.Count(c => c == '”');

            bool isDefinition = text.StartsWith("“") && !text.EndsWith("”") && startQuoteCount == endQuoteCount;
            //if (!isDefinition)
              //  return null;
            
            // Use enricher to create <def> element around defined term
            static IInline constructor(string text, DocumentFormat.OpenXml.Wordprocessing.RunProperties props)
            {
                WText wText = new(text[1..^1], props);
                return new Def() { Contents = [wText], StartQuote = text[..1], EndQuote = text[^1..] };
            }
            WLine enriched = EnrichFromEnd.Enrich(line, defPattern, constructor);
            //if (ReferenceEquals(enriched, line))
                //return null;
            
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
