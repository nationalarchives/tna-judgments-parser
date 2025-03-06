
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Vml;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private readonly Dictionary<(string, int, int), (object Result, int NextPosition)> memo = [];

        // call only if line is Current()
        private T ParseAndMemoize<T>(WLine line, string startQuote, string name, System.Func<WLine, string, T> parseFunction) {
            var key = (name, i, quoteDepth);
            if (memo.TryGetValue(key, out var cached)) {
                i = cached.NextPosition;
                return (T)cached.Result;
            }
            int save = i;
            T result = parseFunction(line, startQuote);
            if (result is null)
                i = save;
            memo[key] = (result, i);
            return result;
        }

        private HContainer ParseLine(string startQuote = null)
        {
            if (Current() is not WLine line)
                return null;

            HContainer hContainer;

            if (isInSchedules)
                hContainer = ParseScheduleLine(line, startQuote);
            else
                hContainer = ParseNonScheduleLine(line, startQuote);

            if (hContainer != null)
                return hContainer;

            // Parse divisions which can occur both inside AND outside schedules

            hContainer = ParseAndMemoize(line, startQuote, "Schedules", ParseSchedules);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "Schedule", ParseSchedule);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "Para1", ParsePara1);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "Para2", ParsePara2);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "Para3", ParsePara3);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "Definition", ParseDefinition);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "UnnumberedParagraph", ParseUnnumberedParagraph);
            if (hContainer != null)
                return hContainer;

            i += 1;
            if (line is WOldNumberedParagraph np)
                return new UnknownLevel() { Number = np.Number, Contents = [WLine.RemoveNumber(np)] };
            else
                return new UnknownLevel() { Contents = [line] };
        }

        // Parse divisions that only occur INSIDE Schedules
        private HContainer ParseScheduleLine(WLine line, string startQuote)
        {
            HContainer hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "SchedulePart", ParseSchedulePart);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "ScheduleChapter", ParseScheduleChapter);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "ScheduleCrossHeading", ParseScheduleCrossheading);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "SchProv1", ParseSchProv1);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "SchProv2", ParseSchProv2);
            if (hContainer != null)
                return hContainer;

            return null;
        }

        // Parse divisions that only occur OUTSIDE Schedules
        private HContainer ParseNonScheduleLine(WLine line, string startQuote)
        {
            HContainer hContainer;
            hContainer = ParseAndMemoize(line, startQuote, "GroupOfParts", ParseGroupOfParts);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "Part", ParsePart);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "Chapter", ParseChapter);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "CrossHeading", ParseCrossheading);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "Prov1", ParseProv1);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, startQuote, "Prov2", ParseProv2);
            if (hContainer != null)
                return hContainer;

            return null;
        }

        /* helper functions for content, intro and wrapUp */

        private void AddFollowingToContent(WLine leader, List<IBlock> container)
        {
            while (i < Document.Body.Count)
            {
                IBlock current = Current();
                if (Current() is not WLine line)
                {
                    i += 1;
                    container.Add(current);
                    continue;
                }
                /* TODO: Remove
                BlockQuotedStructure qs = ParseQuotedStructure(line);
                if (qs is not null)
                {
                    container.Add(qs);
                    continue;
                }
                */
                if (line is WOldNumberedParagraph)
                    break;
                if (!IsLeftAligned(line))
                    break;
                if (LineIsIndentedLessThan(line, leader))
                    break;
                int save = i;
                HContainer test = ParseProv1(line);
                i = save;
                if (test is not null)
                    break;
                i += 1;
                container.Add(line);
            }
        }

        private void HandleExtraParagraphs(WLine leader, List<IBlock> container) {
            while (i < Document.Body.Count)
            {
                int save = i;
                if (BreakFromProv1(leader))
                {
                    i = save;
                    break;
                }
                IDivision next = ParseNextBodyDivision();
                IBlock extraParagraph = GetExtraParagraph(next, leader);
                if (extraParagraph == null)
                {
                    i = save;
                    break;
                }
                container.Add(extraParagraph);
            }
        }

        private IBlock GetExtraParagraph(IDivision division, WLine leader)
        {
            if (division is WDummyDivision dummy && dummy.Contents.Count() == 1 && dummy.Contents.First() is WTable table)
                return table;
            if (division is not UnnumberedLeaf leaf)
                return null;
            if (leaf.Contents.Count != 1)
                return null;
            if (leaf.Contents.First() is not WLine line)
                return null;
            if (line is WOldNumberedParagraph)
                return null;
            if (!IsLeftAligned(line))
                return null;
            if (LineIsIndentedLessThan(line, leader))
                return null;
            return line;
        }

        // DEANTODO: Remove
        private bool IsExtraIntroLine(IDivision division, IBlock line, WLine leader, int childCount)
        {
            if (childCount > 0)
                return false;
            if (division is WDummyDivision dummy && dummy.Contents.Count() == 1 && dummy.Contents.First() is WTable)
                return true;
            if (division is not UnnumberedLeaf)
                return false;
            if (line is not WLine wLine)
                return false;
            if (line is WOldNumberedParagraph)
                return false;
            if (!IsLeftAligned(wLine))
                return false;
            if (LineIsIndentedLessThan(wLine, leader))
                return false;
            return true;
        }

        private List<IBlock> HandleWrapUp(List<IDivision> children, int save)
        {
            List<IBlock> wrapUp = [];
            if (children.Count == 0)
                return wrapUp;
            if (children.Last() is not UnnumberedLeaf leaf)
                // Closing Words must be the final child 
                return wrapUp;
            if (children.Count == 1)
            {
                // This *is* Closing Words, but Closing words cannot be an only child,
                // so it must belong to an ancestor provision
                children.RemoveAt(children.Count - 1);
                i = save;
                return wrapUp;
            }
            children.RemoveAt(children.Count - 1);
            return [..leaf.Contents];
        }

        private List<IBlock> HandleWrapUp2(IDivision next, int childCount)
        {
            if (childCount == 0)
                return [];
            if (next is not UnnumberedLeaf leaf)
                // Closing Words must be the final child 
                return [];
            if (childCount == 1)
            {
                // This *is* Closing Words, but Closing words cannot be an only child,
                // so it must belong to an ancestor provision
                return [];
            }
            return [.. leaf.Contents];
        }



        private bool BreakFromProv1(WLine leader)
        {
            if (Current() is not WLine line)
                return false;

            // The following provisions cannot occur inside a Prov1/SchProv1
            // If we encounter one, we must step out of the Prov1/SchProv1 
            if (PeekProv1(line))
                return true;
            if (PeekSchedule(line))
                return true;
            if (PeekSchedules(line))
                return true;
            if (PeekScheduleCrossHeading(line))
                return true;
            // Todo: Add other grouping provisions?
            return false;
        }

    }

}
