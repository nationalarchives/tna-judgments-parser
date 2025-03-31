
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private readonly Dictionary<(string, int, int, DocName, Context), (object Result, int NextPosition)> memo = [];

        // call only if line is Current()
        private T ParseAndMemoize<T>(WLine line, string name, System.Func<WLine, T> parseFunction) {
            parseAndMemoizeDepth += 1;
            if (parseAndMemoizeDepth > parseAndMemoizeDepthMax)
                parseAndMemoizeDepthMax = parseAndMemoizeDepth;
            var key = (name, i, quoteDepth, frames.CurrentDocName, frames.CurrentContext);
            if (memo.TryGetValue(key, out var cached)) {
                i = cached.NextPosition;
                parseAndMemoizeDepth -= 1;
                return (T)cached.Result;
            }
            int save = i;
            parseDepth += 1;
            if (parseDepth > parseDepthMax)
                parseDepthMax = parseDepth;
            T result = parseFunction(line);
            if (result is null)
                i = save;
            memo[key] = (result, i);
            parseAndMemoizeDepth -= 1;
            parseDepth -= 1;
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
            // If we've reached here then this is an element we don't know how to handle
            // UnknownLevels are marked up as a single <p> element. If the line is numbered,
            // we must combine the Number with the Contents to ensure the number is not lost
            this.Logger.LogWarning("Encountered an unknown provision at " +
            "i: {Position} with Contents: {Contents}"
            , this.i
            , line.NormalizedContent);
            line = line switch {
                WOldNumberedParagraph np => new WUnknownLine(line, [np.Number, new WText(" ", null), .. np.Contents]),
                _ => new WUnknownLine(line),

            };

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

            hContainer = ParseAndMemoize(line, "ScheduleGroupingSection", ParseScheduleGroupingSection);
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

            hContainer = ParseAndMemoize(line, "GroupingSection", ParseGroupingSection);
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
            if (BreakFromProv1())
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

        /*
         * Prevents the ParseAndMemoize method from recursing too deep. 
         * If we are inside a Prov1/SchProv1 or any of their descendants, and we
         * encounter another (non-quoted) Prov1/SchProv1 or a grouping provision, 
         * then we know the current Prov1/SchProv1 must have ended, and must break.
         */
        private bool BreakFromProv1()
        {
            if (Current() is not WLine line)
                return false;

            // The following provisions cannot occur inside a Prov1/SchProv1
            // If we encounter one, we must step out of the Prov1/SchProv1 

            // Sections cannot occur in a Schedule context, so no need to check for them
            if (!frames.IsScheduleContext() && PeekProv1(line))
                return true;
            if (PeekSchProv1(line))
                return true;
            // If centre-aligned, it must be a grouping provision
            if (IsCenterAligned(line))
                return true;
            if (PeekScheduleCrossHeading(line))
                return true;
            return false;
        }

        /*
         * Prevents the ParseAndMemoize method from recursing too deep.
         * Given that we are currently inside a grouping provision, if we encounter
         * another (non-quoted) grouping provision that is not a valid child of
         * the current one, then the current one must have ended, and we must break. 
         * Importantly, this is determined by 'peeking' rather than 'parsing', which
         * significantly cuts down on recursion depth.
         */
        private HContainer PeekGroupingProvision()
        {
            if (Current() is not WLine line)
                return null;
            if (!IsCenterAligned(line))
                return null;
            if (frames.IsScheduleContext())
            {
                if (PeekSchedules(line))
                    return new Schedules { };
                if (PeekSchedule(line))
                    return new ScheduleLeaf { };
                if (PeekSchedulePartHeading(line))
                    return new SchedulePartLeaf { };
                if (PeekScheduleChapterHeading(line))
                    return new ScheduleChapterLeaf { };
                if (PeekScheduleGroupingSectionHeading(line))
                    return new ScheduleGroupingSectionLeaf { };
                if (PeekScheduleCrossHeading(line))
                    return new ScheduleCrossHeadingLeaf { };
            }
            else
            {
                if (PeekGroupOfPartsHeading(line))
                    return new GroupOfPartsLeaf { };
                if (PeekPartHeading(line))
                    return new PartLeaf { };
                if (PeekChapterHeading(line))
                    return new ChapterLeaf { };
                if (PeekGroupingSectionHeading(line))
                    return new GroupingSectionLeaf { };
                if (PeekCrossHeading(line))
                    return new CrossHeadingLeaf { };
            }
            return null;
        }

    }
}