
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer ParseChapter(WLine line)
        {
            var save1 = i;

            if (!PeekChapterHeading(line))
                return null;

            IFormattedText number = new WText(
                line.NormalizedContent[..1].ToUpper() + line.NormalizedContent[1..].ToLower(),
                line.Contents.Where(i => i is WText).Cast<WText>().Select(t => t.properties).FirstOrDefault()
            );

            if (IsEndOfQuotedStructure(line.NormalizedContent))
                return new ChapterLeaf { Number = number };

            if (Body[i + 1] is not WLine line2)
                return null;

            // Chapters may have no heading
            ILine heading = null;
            // If line2 is center aligned, parse as the heading
            if (line2.IsCenterAligned())
                heading = line2;
            i += heading is null ? 1 : 2;

            if (IsEndOfQuotedStructure(line2.NormalizedContent))
                return new ChapterLeaf { Number = number, Heading = heading };

            List<IDivision> children = [];

            while (i < Body.Count)
            {
                HContainer peek = PeekGroupingProvision();
                if (peek != null && !Chapter.IsValidChild(peek))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!Chapter.IsValidChild(next))
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
            return new ChapterBranch { Number = number, Heading = heading, Children = children };
        }

        private bool PeekChapterHeading(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (i > Body.Count - 3)
                return false;
            string numText = IgnoreQuotedStructureStart(line.NormalizedContent, quoteDepth);
            if (!LanguageService.IsMatch(numText, Chapter.NumberPatterns))
                return false;
            return true;
        }

    }

}