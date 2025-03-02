
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using System.Text.RegularExpressions;
using UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

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

            var save1 = i;
            List<IDivision> children;
            if (isInSchedules || quoteDepth > 0)
            {
                i += (referenceNoteLine is null) ? 2 : 3;
                children = ParseScheduleChildren();
                if (children.Count == 0)
                {
                    i = save1;
                    return null;
                }
                return new Schedule { Number = number, Heading = heading, ReferenceNote = referenceNote, Contents = children };
            }
            else
            {
                // If we encounter a Schedule outside of a Schedules container, it must be wrapped
                // Note: Does not apply inside quoted structures
                children = ParseSchedulesChildren();
                if (children.Count == 0)
                {
                    i = save1;
                    return null;
                }
                return new Schedules { Number = null, Heading = null, Children = children };
            }
        }

        private bool PeekSchedule(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (!IsCenterAligned(line))
                return false;
            if (i > Document.Body.Count - 3)
                return false;
            if (!Schedule.IsValidNumber(line.NormalizedContent))
                return false;
            return true;
        }

        internal List<IDivision> ParseScheduleChildren()
        {
            bool isInSchedulesSave = isInSchedules;
            isInSchedules = true;

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
            }
            isInSchedules = isInSchedulesSave;
            return children;
        }

    }

}
