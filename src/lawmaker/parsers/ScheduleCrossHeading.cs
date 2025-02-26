
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private ScheduleCrossHeading ParseScheduleCrossheading(WLine line)
        {
            if (!PeekScheduleCrossHeading(line))
                return null;

            var save1 = i;
            i += 1;

            ILine heading = line;

            List<IDivision> children = [];

            bool isInSchedulesSave = isInSchedules;
            isInSchedules = true;
            while (i < Document.Body.Count)
            {
                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!ScheduleCrossHeading.IsValidChild(next))
                {
                    i = save;
                    break;
                }
                children.Add(next);
            }
            isInSchedules = isInSchedulesSave;
            if (children.Count == 0)
            {
                i = save1;
                return null;
            }
            return new ScheduleCrossHeading { Heading = heading, Children = children };
        }

        private bool PeekScheduleCrossHeading(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (!IsCenterAligned(line))
                return false;
            if (!line.IsAllItalicized())
                return false;
            if (i == Document.Body.Count - 1)
                return false;
            return true;
        }

    }

}
