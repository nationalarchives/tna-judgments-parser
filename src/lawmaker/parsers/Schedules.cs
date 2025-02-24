
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private Schedules ParseSchedules(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return null;
            if (!IsCenterAligned(line))
                return null;
            if (i > Document.Body.Count - 3)
                return null;
            if (!Schedules.IsSchedulesHeading(line.NormalizedContent))
                return null;

            // Schedules container must be followed by Schedule
            if (Document.Body[i + 1].Block is not WLine line2)
                return null;
            if (!IsCenterAligned(line2))
                return null;

            IFormattedText headingText = new WText("Schedules", null);
            ILine heading = WLine.Make(line, new List<IInline>(1) { headingText });

            var save1 = i;
            i += 1;

            List<IDivision> children = ParseSchedulesChildren();
            if (children.Count == 0)
            {
                i = save1;
                return null;
            }
            return new Schedules { Number = null, Heading = heading, Children = children };
        }

        internal List<IDivision> ParseSchedulesChildren()
        {
            bool isInSchedulesSave = isInSchedules;
            isInSchedules = true;

            List<IDivision> children = [];
            while (i < Document.Body.Count)
            {

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (next is not Schedule)
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
            return children;
        }

    }

}
