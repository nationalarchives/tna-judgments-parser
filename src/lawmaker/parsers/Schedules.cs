
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
            if (!line.IsCenterAligned())
                return false;
            if (i > Body.Count - 3)
                return false;
            string heading = IgnoreQuotedStructureStart(line.NormalizedContent, quoteDepth);
            if (!LanguageService.IsMatch(heading, Schedules.HeadingPatterns))
                return false;

            // Schedules container must be followed by Schedule
            if (Body[i + 1] is not WLine line2)
                return false;
            DocName docname = frames.CurrentDocName;
            if (!docname.IsWelshSecondary() && !line.IsCenterAligned())
                return false;
            return true;
        }

        internal List<IDivision> ParseSchedulesChildren()
        {
            frames.PushScheduleContext();
            List<IDivision> children = [];
            while (i < Body.Count)
            {
                // If we hit the conclusions, the schedules have finished
                if (ExplanatoryNote.IsHeading(LanguageService, Current()))
                    break;

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
