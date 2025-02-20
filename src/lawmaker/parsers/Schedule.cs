
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private Schedule ParseSchedule(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return null;
            if (!IsCenterAligned(line))
                return null;
            if (i > Document.Body.Count - 3)
                return null;

            if (!Schedule.IsScheduleNumber(line.NormalizedContent))
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
                i += 1;
            }
            if (!IsCenterAligned(headingLine))
                return null;
            ILine heading = headingLine;

            IFormattedText referenceNote = new WText(
                referenceNoteLine is null ? "" : referenceNoteLine.NormalizedContent,
                null
            );

            var save1 = i;
            i += 2;

            List<IDivision> children = [];

            while (i < Document.Body.Count)
            {
                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (next is not CrossHeading && next is not Prov1) // TODO: change to schedule subdivisions
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
            // TODO: Uncomment this
            /*
            if (children.Count == 0)
            {
                i = save1;
                return null;
            }
            */
            return new Schedule { Number = number, Heading = heading, ReferenceNote = referenceNote, Contents = children };
        }

    }

}
