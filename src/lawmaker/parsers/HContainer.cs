
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Vml;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.PressSummaries;

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

            if (frames.IsScheduleContext())
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
            // UnknownLevels are marked up as a single <p> element. If the line is numbered,
            // we must combine the Number with the Contents to ensure the number is not lost
            if (line is WOldNumberedParagraph np)
                line = new WLine(line, [np.Number, new WText(" ", null), .. np.Contents]);

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


        /*
         * Returns a list of blocks starting with the current paragraph, plus any
         * additional blocks (i.e extra paragraphs, quoted structures, and/or tables) 
        */
        private List<IBlock> HandleParagraphs(WLine line)
        {
            WLine first = (line is WOldNumberedParagraph np) ? WLine.RemoveNumber(np) : line;
            if (IsEndOfQuotedStructure(first.TextContent))
                return [first];

            List<IBlock> container = [];
            HandleMod(first, container);

            while (i < Document.Body.Count)
            {
                int save = i;
                IList<IBlock> extraParagraph = GetExtraParagraph(line);
                if (extraParagraph == null)
                {
                    i = save;
                    break;
                }
                foreach (IBlock block in extraParagraph)
                    HandleMod(block, container);

                if (extraParagraph.Last() is WLine lastLine && IsEndOfQuotedStructure(lastLine.TextContent))
                    break;
            }
            return container;
        }

        /*
         * Adds the given block to the given container.
         * If the block is followed by one or more quoted structures,
         * it is wrapped in a Mod together with the quoted structures.
        */
        private void HandleMod(IBlock block, List<IBlock> container)
        {
            if (block is not WLine line)
            {
                container.Add(block);
                return;
            }
            List <IQuotedStructure> quotedStructures = HandleQuotedStructuresAfter(line);
            if (quotedStructures.Count == 0)
            {
                container.Add(line);
                return;
            }
            List<IBlock> contents = [line, .. quotedStructures];
            Mod mod = new() { Contents = contents };
            container.Add(mod);
        }

        /*
         * Determines if the next line is an extra paragraph belonging to the current division.
         * If so, it returns the paragraph. If not, it returns null.
        */
        private IList<IBlock> GetExtraParagraph(WLine leader)
        {
            if (BreakFromProv1(leader))
                return null;

            IDivision next = ParseNextBodyDivision();

            if (next is WDummyDivision dummy && dummy.Contents.Count() == 1 && dummy.Contents.First() is WTable table)
                return [table];
            // UnknownLevels are treated as extra paragraphs of the previous division 
            if (next is not UnnumberedLeaf && next is not UnknownLevel)
                return null;
            Leaf leaf = next as Leaf;

            IBlock firstBlock = leaf.Contents.First();
            WLine firstLine = null;
            if (firstBlock is WLine)
                firstLine = firstBlock as WLine;
            else if (firstBlock is Mod mod && mod.Contents.First() is WLine firstModLine)
                firstLine = firstModLine;
            else
                return null;

            if (firstLine is WOldNumberedParagraph)
                return null;
            if (next is UnnumberedLeaf)
            {
                if (!IsLeftAligned(firstLine))
                    return null;
                if (LineIsIndentedLessThan(firstLine, leader))
                    return null;
            }
            return leaf.Contents;
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
