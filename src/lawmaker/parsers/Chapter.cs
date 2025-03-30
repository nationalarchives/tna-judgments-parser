
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private Chapter ParseChapter(WLine line)
        {
            if (!PeekChapterHeading(line))
                return null;

            IFormattedText number = new WText(
                line.NormalizedContent[..1].ToUpper() + line.NormalizedContent[1..].ToLower(),
                line.Contents.Where(i => i is WText).Cast<WText>().Select(t => t.properties).FirstOrDefault()
            );

            if (Document.Body[i + 1].Block is not WLine line2)
                return null;
            if (!IsCenterAligned(line2))
                return null;
            ILine heading = line2;

            var save1 = i;
            i += 2;

            List<IDivision> children = [];

            while (i < Document.Body.Count)
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
            return new Chapter { Number = number, Heading = heading, Children = children };
        }

        private bool PeekChapterHeading(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (i > Document.Body.Count - 3)
                return false;
            string numText = IgnoreStartQuote(line.NormalizedContent, quoteDepth);
            if (!Chapter.IsValidNumber(numText))
                return false;
            return true;
        }

    }

}