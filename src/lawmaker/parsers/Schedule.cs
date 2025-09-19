
#nullable enable

using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using System.Text.RegularExpressions;
using System;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer? ParseSchedule(WLine line)
        {
            var save = i;

            if (!PeekSchedule(line))
                return null;

            // Handle number
            WLine numberLine = line;
            string numberText = GetNumber(numberLine, false);
            IFormattedText number = new WText(numberText, null);

            if (Document.Body[i + 1].Block is not WLine line2)
                return null;

            // Num can be followed by a reference note & heading, or just a heading.
            WLine headingLine;
            WLine? referenceNoteLine;
            if (IsReferenceNote(line2))
            {
                if (Document.Body[i + 2].Block is not WLine line3)
                    return null;
                referenceNoteLine = line2;
                headingLine = line3;
                i += 3;
            }
            else
            {
                referenceNoteLine = null;
                headingLine = line2;
                i += 2;
            }

            // SI documents occasionally have schedule reference notes on the same line as the schedule number
            // (separated by one or more tab characters) as opposed to having their own distinct line.
            string referenceNoteText;
            if (referenceNoteLine is null && frames.IsSecondaryDocName())
                referenceNoteText = GetRightTabbedText(numberLine);
            else
                referenceNoteText = referenceNoteLine?.NormalizedContent ?? "";
            IFormattedText referenceNote = new WText(referenceNoteText, null);

            if (!IsCenterAligned(headingLine))
            {
                i = save;
                return null;
            }

            var save1 = i;

            HContainer schedule;
            IDivision next = ParseNextBodyDivision();
            if (next is UnnumberedLeaf content)
            {
                schedule = new ScheduleLeaf { Number = number, Heading = headingLine, ReferenceNote = referenceNote, Contents = content.Contents };
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
                schedule = new ScheduleBranch { Number = number, Heading = headingLine, ReferenceNote = referenceNote, Children = children };
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
            string numText = GetNumber(line, true);
            if (!langService.IsMatch(numText, Schedule.NumberPatterns))
                return false;
            return true;
        }

        internal List<IDivision> ParseScheduleChildren()
        {
            frames.PushScheduleContext();
            List<IDivision> children = [];
            while (i < Document.Body.Count)
            {
                HContainer peek = PeekGroupingProvision();
                if (peek != null && !Schedule.IsValidChild(peek))
                    break;

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

        private string GetNumber(WLine line, bool ignoreQuotedStructureStart)
        {
            string number = frames.IsSecondaryDocName() ? IgnoreRightTabbedText(line) : line.NormalizedContent;
            if (ignoreQuotedStructureStart)
                number = IgnoreQuotedStructureStart(number, quoteDepth);
            return Regex.Replace(number, @"SCHEDULE", "Schedule", RegexOptions.IgnoreCase);
        }

        private bool IsReferenceNote(WLine line)
        {
            if (DocNames.IsPrimarySP(docName))
            {
                // Reference notes in SP Bills/Acts are formatted differently
                if (IsCenterAligned(line) && line.IsAllItalicized())
                    return true;
                StringComparison ignoreCase = StringComparison.CurrentCultureIgnoreCase;
                if (line.NormalizedContent.StartsWith("(introduced by", ignoreCase))
                    return true;
            }
            else
                return IsRightAligned(line);
            return false;
        }


    }

}
