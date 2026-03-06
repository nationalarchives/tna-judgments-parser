
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer ParsePart(WLine line)
        {
            var save1 = i;

            if (!PeekPartHeading(line))
                return null;

            IFormattedText number = new WText(
                line.NormalizedContent[..1].ToUpper() + line.NormalizedContent[1..].ToLower(),
                line.Contents.Where(i => i is WText).Cast<WText>().Select(t => t.properties).FirstOrDefault()
            );

            if (IsEndOfQuotedStructure(line.NormalizedContent))
                return new PartLeaf { Number = number };

            if (Body[i + 1] is not WLine line2)
                return null;

            // Parts may have no heading
            ILine heading = null;
            // If line2 is centre aligned and does not match a chapter's num pattern, parse as the heading
            if (!(!line2.IsCenterAligned() || LanguageService.IsMatch(line2.TextContent, Chapter.NumberPatterns)))
                heading = line2;
            i += heading is null ? 1 : 2;
            if (IsEndOfQuotedStructure(line2.NormalizedContent))
                return new PartLeaf { Number = number, Heading = heading };

            List<IDivision> children = [];

            while (i < Body.Count)
            {
                HContainer peek = PeekGroupingProvision();
                if (peek != null && !Part.IsValidChild(peek))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!Part.IsValidChild(next))
                {
                    i = save;
                    break;
                }
                children.Add(next);

                if (IsEndOfQuotedStructure(next))
                    break;
            }
            if (children.Count == 0)
            {
                i = save1;
                return null;
            }
            return new PartBranch { Number = number, Heading = heading, Children = children };
        }

        private bool PeekPartHeading(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (!line.IsCenterAligned())
                return false;
            if (i > Body.Count - 3)
                return false;
            string numText = IgnoreQuotedStructureStart(line.NormalizedContent, quoteDepth);
            if (!LanguageService.IsMatch(numText, Part.NumberPatterns))
                return false;
            return true;
        }

    }

}
