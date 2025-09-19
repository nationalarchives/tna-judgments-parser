
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private Schedules ParseSchedules(WLine line)
        {
            if (!PeekSchedules(line))
                return null;

            var save1 = i;
            i += 1;

            List<IDivision> children = ParseSchedulesChildren();
            if (children.Count == 0)
            {
                i = save1;
                return null;
            }
            return new Schedules { Number = null, Heading = line, Children = children };
        }

        private bool PeekSchedules(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (!IsCenterAligned(line))
                return false;
            if (i > Document.Body.Count - 3)
                return false;
            string heading = IgnoreQuotedStructureStart(line.NormalizedContent, quoteDepth);
            if (!langService.IsMatch(heading, Schedules.HeadingPatterns))
                return false;

            // Schedules container must be followed by Schedule
            if (Document.Body[i + 1].Block is not WLine line2)
                return false;
            if (!IsCenterAligned(line2))
                return false;
            return true;
        }

        internal List<IDivision> ParseSchedulesChildren()
        {
            frames.PushScheduleContext();
            List<IDivision> children = [];
            while (i < Document.Body.Count)
            {
                HContainer peek = PeekGroupingProvision();
                if (peek != null && !Schedules.IsValidChild(peek))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!Schedules.IsValidChild(next))
                {
                    i = save;
                    break;
                }
                children.Add(next);
            }
            frames.Pop();
            return children;
        }

    }

}
