
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
            if (line is WOldNumberedParagraph np)
                return null;
            if (i > Document.Body.Count - 3)
                return null;

            if (!Chapter.IsChapterNumber(line.NormalizedContent))
                return null;
            IFormattedText number = new WText(
                line.NormalizedContent[..1].ToUpper() + line.NormalizedContent[1..].ToLower(),
                line.Contents.Where(i => i is WText).Cast<WText>().Select(t => t.properties).FirstOrDefault()
            );

            // next line should always be a Chapter heading
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

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!Chapter.IsValidChild(next))
                {
                    i = save;
                    break;
                }
                if (!NextChildIsAcceptable(children, next))
                {
                    i = save;
                    break;
                }
                children.Add(next);
            }
            // Chapters should always have children
            if (children.Count == 0)
            {
                i = save1;
                return null;
            }
            return new Chapter { Number = number, Heading = heading, Children = children };
        }

    }

}