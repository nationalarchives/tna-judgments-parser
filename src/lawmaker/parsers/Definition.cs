
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private static string defPattern;

        private static string DefPattern()
        {
            if (defPattern is not null)
                return defPattern;

            string startQuote = "[\u201C]";
            string endQuote = "[\u201D]";
            defPattern = $@"({startQuote}(?:(?!{startQuote}|{endQuote}).)*{endQuote})";
            return defPattern;
        }

        private HContainer ParseDefinition(WLine line)
        {
            if (!PeekDefinition(line))
                return null;

            // Use enricher to create <def> element around defined term
            static IInline constructor(IEnumerable<IInline> inlines)
            {
                string startQuote = null;
                string endQuote = null;
                if (inlines.FirstOrDefault() is WText first)
                {
                    startQuote = first.Text[..1];
                    WText fixedFirst = new(first.Text[1..], first.properties);
                    inlines = inlines.Skip(1).Prepend(fixedFirst);
                }
                if (inlines.LastOrDefault() is WText last)
                {
                    endQuote = last.Text[^1..];
                    WText fixedLast = new(last.Text[..^1], last.properties);
                    inlines = inlines.SkipLast(1).Append(fixedLast);
                }
                return new Def() { Contents = inlines.ToList(), StartQuote = startQuote, EndQuote = endQuote };
            }
            WLine enriched = EnrichFromBeginning.Enrich(line, defPattern, constructor);
            if (ReferenceEquals(enriched, line))
                return null;

            i += 1;

            List<IBlock> intro = [enriched];

            if (i == Body.Count)
            {
                return new DefinitionLeaf { Contents = intro };
            }

            List<IDivision> children = [];

            while (i < Body.Count)
            {
                if (PeekDefinition(Current()))
                    break;
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

        private bool PeekDefinition(IBlock block)
        {
            if (block is not WLine line)
                return false;
            if (line is WOldNumberedParagraph)
                return false;
            if (!IsLeftAligned(line))
                return false;

            string definitionPattern = $@"^{QuotedStructureStartPattern()}?{DefPattern()}.*\w+.*$";
            if (!Regex.IsMatch(line.NormalizedContent, definitionPattern))
                return false;
            return true;
        }

    }

}
