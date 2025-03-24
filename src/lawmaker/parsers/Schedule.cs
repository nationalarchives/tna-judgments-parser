
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseSchedule(WLine line)
        {
            if (!PeekSchedule(line))
                return null;

            string numberString = Regex.Replace(
                line.NormalizedContent,
                @"SCHEDULE",
                "Schedule",
                RegexOptions.IgnoreCase
            );
            IFormattedText number = new WText(numberString, null);

            if (Document.Body[i + 1].Block is not WLine line2)
                return null;

            WLine referenceNoteLine = null;
            WLine headingLine = line2;
            if (IsRightAligned(line2))
            {
                // Handle reference note
                if (Document.Body[i + 2].Block is not WLine line3)
                    return null;
                referenceNoteLine = line2;
                headingLine = line3;
            }
            if (!IsCenterAligned(headingLine))
                return null;
            ILine heading = headingLine;

            IFormattedText referenceNote = new WText(
                referenceNoteLine is null ? "" : referenceNoteLine.NormalizedContent,
                null
            );

            var save = i;
            i += (referenceNoteLine is null) ? 2 : 3;
            var save1 = i;

            HContainer schedule;
            IDivision next = ParseNextBodyDivision();
            if (next is UnnumberedLeaf content)
            {
                schedule = new ScheduleLeaf { Number = number, Heading = heading, ReferenceNote = referenceNote, Contents = content.Contents };
            }
            else
            {
                i = save1;
                List<IDivision> children = ParseScheduleChildren();
                if (children.Count == 0)
                {
                    i = save;
                    return null;
                }
                schedule = new ScheduleBranch { Number = number, Heading = heading, ReferenceNote = referenceNote, Children = children };
            }

            if (frames.IsScheduleContext() || quoteDepth > 0)
                return schedule;

            // If we encounter a non-quoted Schedule outside of a Schedules container, it must be wrapped
            return new Schedules { Number = null, Children = [schedule] };
        }

        private bool PeekSchedule(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (!IsCenterAligned(line))
                return false;
            if (i > Document.Body.Count - 3)
                return false;
            string num = IgnoreQuotedStructureStart(line.NormalizedContent, quoteDepth);
            if (!Schedule.IsValidNumber(num))
                return false;
            return true;
        }

        internal List<IDivision> ParseScheduleChildren()
        {
            frames.PushScheduleContext();
            List<IDivision> children = [];
            while (i < Document.Body.Count)
            {
                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!Schedule.IsValidChild(next))
                {
                    i = save;
                    break;
                }
                children.Add(next);

                if (IsEndOfQuotedStructure(next))
                    break;
            }
            frames.Pop();
            return children;
        }

    }

}
