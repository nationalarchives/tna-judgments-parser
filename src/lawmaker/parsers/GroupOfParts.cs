using System.Linq;
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{
    // TODO: Move this responsibility to the actual GroupOfParts object
    // e.g. GroupOfParts.Parse(WLine line)
    public partial class LegislationParser
    {
        private HContainer ParseGroupOfParts(WLine line)
        {
            if (!PeekGroupOfPartsHeading(line))
                return null;

            IFormattedText number = new WText(
                ToTitleCase(line.NormalizedContent),
                line.Contents.Where(i => i is WText).Cast<WText>().Select(t => t.properties).FirstOrDefault()
            );

            if (IsEndOfQuotedStructure(line.NormalizedContent))
                return new GroupOfPartsLeaf { Number = number };

            if (Body[i+1] is not WLine line2)
                return null;
            if (!line2.IsCenterAligned())
                return null;
            ILine heading = line2;

            if (IsEndOfQuotedStructure(line2.NormalizedContent))
                return new GroupOfPartsLeaf { Number = number, Heading = heading };

            var save1 = i;
            i += 2;

            List<IDivision> children = [];

            while (i < Body.Count)
            {
                HContainer peek = PeekGroupingProvision();
                if (peek != null && !GroupOfParts.IsValidChild(peek))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!GroupOfParts.IsValidChild(next)) {
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
            return new GroupOfPartsBranch { Number = number, Heading = heading, Children = children };

        }

        private bool PeekGroupOfPartsHeading(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (!line.IsCenterAligned())
                return false;
            // Group of parts **always** has a part num, part heading and something beneath it
            if (i > Body.Count - 3)
                return false;
            string numText = IgnoreQuotedStructureStart(line.NormalizedContent, quoteDepth);
            if (!LanguageService.IsMatch(numText, GroupOfParts.NumberPatterns))
                return false;
            return true;
        }

        private static string ToTitleCase(string line)
        {
            // ugly
            return line.Split(" ").Aggregate((acc, word) => {
                if (word.ToLower().Equals("of"))
                    return acc + " " + word;
                return acc + " " + word[..1].ToUpper() + word[1..].ToLower();
            });

        }
    }
}