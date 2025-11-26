
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

        private bool PeekSchedule(WLine line)
        {
            if (line is WOldNumberedParagraph)
                return false;
            DocName docname = frames.CurrentDocName;
            if (!docname.IsWelshSecondary() && !IsCenterAligned(line))
                return false;
            if (i > Body.Count - 3)
                return false;
            string numberText = GetScheduleNumber(line, true);
            if (!LanguageService.IsMatch(numberText, Schedule.NumberPatterns))
                return false;
            return true;
        }

        private HContainer? ParseSchedule(WLine line)
        {
            var save = i;

            if (!PeekSchedule(line))
                return null;

            HContainer schedule;
            IFormattedText number;
            IFormattedText referenceNote;
            WLine heading;
            if (ParseScheduleHeader(line) is var result && result.HasValue)
                (number, referenceNote, heading) = result.Value;
            else
                return null;

            frames.PushScheduleContext();
            List<IBlock> contents = ParseScheduleLeafContent(heading);
            if (contents.Count > 0)
            {
                schedule = new ScheduleLeaf { Number = number, Heading = heading, ReferenceNote = referenceNote, Contents = contents };
            }
            else
            {
                List<IDivision> children = ParseScheduleBranchChildren();
                // A ScheduleBranch must have at least 1 child.
                if (children.Count == 0)
                {
                    i = save;
                    return null;
                }
                schedule = new ScheduleBranch { Number = number, Heading = heading, ReferenceNote = referenceNote, Children = children };
            }
            frames.Pop();

            // If we encounter a non-quoted Schedule outside of a Schedules container, it must be wrapped.
            if (frames.IsScheduleContext() || quoteDepth > 0)
                return schedule;
            return new Schedules { Number = null, Children = [schedule] };
        }

        /// <summary>
        /// Returns the number, reference note, and heading of the <c>Schedule</c> starting at <paramref name="line"/>.
        /// Or <c>null</c> if unsuccessful.
        /// </summary>
        /// <param name="line">The first line of the schedule.</param>
        /// <returns>A tuple containing the number, reference note, and heading of the <c>Schedule</c>.</returns>
        internal (IFormattedText number, IFormattedText referenceNote, WLine heading)? ParseScheduleHeader(WLine line)
        {
            int save = i;

            // Handle number
            WLine numberLine = line;
            string numberText = GetScheduleNumber(numberLine, false);
            IFormattedText number = new WText(numberText, null);

            if (Body[i + 1] is not WLine line2)
                return null;

            // Number can be followed by a reference note & heading, or just a heading.
            WLine heading;
            WLine? referenceNoteLine;
            if (IsScheduleReferenceNote(line2))
            {
                if (Body[i + 2] is not WLine line3)
                    return null;
                referenceNoteLine = line2;
                heading = line3;
                i += 3;
            }
            else
            {
                referenceNoteLine = null;
                heading = line2;
                i += 2;
            }

            if (!IsCenterAligned(heading))
            {
                i = save;
                return null;
            }

            // SI documents occasionally have schedule reference notes on the same line as the schedule number
            // (separated by one or more tab characters) as opposed to having their own distinct line.
            string referenceNoteText;
            if (referenceNoteLine is null && frames.IsSecondaryDocName())
                referenceNoteText = GetRightTabbedText(numberLine);
            else
                referenceNoteText = referenceNoteLine?.NormalizedContent ?? "";
            IFormattedText referenceNote = new WText(referenceNoteText, null);

            return (number, referenceNote, heading);
        }

        /// <summary>
        /// Returns the <c>ScheduleLeaf</c> content following the <paramref name="heading"/>, if present. 
        /// Otherwise, returns an empty list.
        /// </summary>
        /// <param name="heading">The line representing the schedule heading.</param>
        /// <returns>A list of <c>ScheduleLeaf</c> content.</returns>
        internal List<IBlock> ParseScheduleLeafContent(WLine heading)
        {
            int save = i;
            List<IBlock> contents = [];

            // Handle when schedule content begins immediately with a quoted structure.
            HandleMod(heading, contents, true);
            if (contents.Count > 0)
                return contents;
            
            // Handle all other schedule content.
            IDivision nextDivision = ParseNextBodyDivision();
            if (nextDivision is UnnumberedLeaf leaf)
                contents.AddRange(leaf.Contents);
            else
                i = save;
            return contents;
        }

        /// <summary>
        /// Parses and returns a list of child divisions belonging to the <c>ScheduleBranch</c>.
        /// </summary>
        /// <returns>A list of child divisions belonging to the <c>ScheduleBranch</c></returns>
        internal List<IDivision> ParseScheduleBranchChildren()
        {
            List<IDivision> children = [];
            while (i < Body.Count)
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
            return children;
        }

        /// <summary>
        /// Obtains and formats the <c>Schedule</c> number from the given <paramref name="line"/>.
        /// </summary>
        /// <param name="line">The line from which to obtain the <c>Schedule</c> number.</param>
        /// <param name="ignoreQuotedStructureStart">
        /// Whether to strip the quoted structure start pattern (if present) from the beginning of the number.
        /// </param>
        /// <returns>The formatted <c>Schedule</c> number.</returns>
        private string GetScheduleNumber(WLine line, bool ignoreQuotedStructureStart)
        {
            string number = frames.IsSecondaryDocName() ? IgnoreRightTabbedText(line) : line.NormalizedContent;
            if (ignoreQuotedStructureStart)
                number = IgnoreQuotedStructureStart(number, quoteDepth);
            return Regex.Replace(number, @"SCHEDULE", "Schedule", RegexOptions.IgnoreCase);
        }


        /// <summary>
        /// Determines whether the given <paramref name="line"/> represents a <c>Schedule</c> reference note.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <returns>Whether <paramref name="line"/> represents a <c>Schedule</c> reference note.</returns>
        private bool IsScheduleReferenceNote(WLine line)
        {
            if (docName.IsScottishPrimary())
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
