
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private ScheduleCrossHeading ParseScheduleCrossheading(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return null;
            if (!IsCenterAligned(line))
                return null;
            if (!line.IsAllItalicized())
                return null;
            if (i == Document.Body.Count - 1)
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
                if (next is not SchProv1)
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
            isInSchedules = isInSchedulesSave;
            if (children.Count == 0)
            {
                i = save1;
                return null;
            }
            return new ScheduleCrossHeading { Heading = heading, Children = children };
        }

    }

}
