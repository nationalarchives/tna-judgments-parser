
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
            if (line is WOldNumberedParagraph np)
                return null;
            if (!IsCenterAligned(line))
                return null;
            if (i > Document.Body.Count - 3)
                return null;
            string numText = IgnoreQuotedStructureStart(line.NormalizedContent, quoteDepth);
            if (!Part.IsValidNumber(numText))
                return null;
            IFormattedText number = new WText(
                line.NormalizedContent[..1].ToUpper() + line.NormalizedContent[1..].ToLower(),
                line.Contents.Where(i => i is WText).Cast<WText>().Select(t => t.properties).FirstOrDefault()
            );

            if (IsEndOfQuotedStructure(line.NormalizedContent))
                return new PartLeaf { Number = number };

            if (Document.Body[i+1].Block is not WLine line2)
                return null;
            if (!IsCenterAligned(line2))
                return null;
            ILine heading = line2;

            if (IsEndOfQuotedStructure(line2.NormalizedContent))
                return new PartLeaf { Number = number, Heading = heading };

            var save1 = i;
            i += 2;

            List<IDivision> children = [];

            while (i < Document.Body.Count)
            {
                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!Part.IsValidChild(next)) {
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

    }

}
