
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private readonly Dictionary<(string, int, int), (object Result, int NextPosition)> memo = [];

        // call only if line is Current()
        private T ParseAndMemoize<T>(WLine line, string name, System.Func<WLine, T> parseFunction) {
            var key = (name, i, quoteDepth);
            if (memo.TryGetValue(key, out var cached)) {
                i = cached.NextPosition;
                return (T)cached.Result;
            }
            int save = i;
            T result = parseFunction(line);
            if (result is null)
                i = save;
            memo[key] = (result, i);
            return result;
        }

        private HContainer ParseLine()
        {
            if (Current() is not WLine line)
                return null;

            HContainer hContainer;

            if (isInSchedules)
                hContainer = ParseScheduleLine(line);
            else
                hContainer = ParseNonScheduleLine(line);

            if (hContainer != null)
                return hContainer;

            // Parse divisions which can occur both inside AND outside schedules

            hContainer = ParseAndMemoize(line, "Schedules", ParseSchedules);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Schedule", ParseSchedule);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Para1", ParsePara1);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Para2", ParsePara2);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Para3", ParsePara3);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Definition", ParseDefinition);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "UnnumberedParagraph", ParseUnnumberedParagraph);
            if (hContainer != null)
                return hContainer;

            i += 1;
            if (line is WOldNumberedParagraph np)
                return new UnknownLevel() { Number = np.Number, Contents = [WLine.RemoveNumber(np)] };
            else
                return new UnknownLevel() { Contents = [line] };
        }

        // Parse divisions that only occur INSIDE Schedules
        private HContainer ParseScheduleLine(WLine line)
        {
            HContainer hContainer;

            hContainer = ParseAndMemoize(line, "SchedulePart", ParseSchedulePart);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "ScheduleChapter", ParseScheduleChapter);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "ScheduleCrossHeading", ParseScheduleCrossheading);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "SchProv1", ParseSchProv1);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "SchProv2", ParseSchProv2);
            if (hContainer != null)
                return hContainer;

            return null;
        }

        // Parse divisions that only occur OUTSIDE Schedules
        private HContainer ParseNonScheduleLine(WLine line)
        {
            HContainer hContainer;
            hContainer = ParseAndMemoize(line, "GroupOfParts", ParseGroupOfParts);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Part", ParsePart);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Chapter", ParseChapter);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "CrossHeading", ParseCrossheading);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Prov1", ParseProv1);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Prov2", ParseProv2);
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
                QuotedStructure qs = ParseQuotedStructure(line);
                if (qs is not null)
                {
                    container.Add(qs);
                    continue;
                }
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
