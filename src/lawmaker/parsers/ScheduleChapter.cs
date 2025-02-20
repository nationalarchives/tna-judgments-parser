
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private ScheduleChapter ParseScheduleChapter(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return null;
            if (!IsCenterAligned(line))
                return null;
            if (i > Document.Body.Count - 3)
                return null;

            if (!ScheduleChapter.IsChapterNumber(line.NormalizedContent))
                return null;
            IFormattedText number = new WText(
                line.NormalizedContent,
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

            bool isInScheduleSave = isInSchedule;
            isInSchedule = true;
            while (i < Document.Body.Count)
            {

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (next is not ScheduleCrossHeading && next is not SchProv1)
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
            isInSchedule = isInScheduleSave;
            if (children.Count == 0)
            {
                i = save1;
                return null;
            }
            return new ScheduleChapter { Number = number, Heading = heading, Children = children };
        }

    }

}