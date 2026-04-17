
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using DocumentFormat.OpenXml.VariantTypes;
using DocumentFormat.OpenXml.Vml;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.Imaging;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
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

        private HContainer ParseLine(System.Func<WLine, HContainer> nextExpected = null)
        {
            if (Current() is not WLine line)
                return null;

            HContainer hContainer = null;

            if (nextExpected != null)
            {
                hContainer = nextExpected(line);
            }
            if (hContainer != null)
            {
                return hContainer;
            }

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

            hContainer = ParseAndMemoize(line, "Signatures", ParseSignatures);
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
        private List<IBlock> HandleParagraphs(WLine line, System.Func<WLine, HContainer> nextExpected = null)
        {
            WLine first = (line is WOldNumberedParagraph np) ? WLine.RemoveNumber(np) : line;
            if (IsEndOfQuotedStructure(first.TextContent))
                return [first];

            List<IBlock> container = [];
            HandleMod(first, container);

            while (i < Body.Count)
            {
                int save = i;
                IList<IBlock> extraParagraph = GetExtraParagraph(line, nextExpected);
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
        private void HandleMod(IBlock block, List<IBlock> container, bool skipInitialLine = false)
        {
            if (block is not WLine line)
            {
                if (!skipInitialLine) container.Add(block);
                return;
            }
            List <IQuotedStructure> quotedStructures = HandleQuotedStructuresAfter(line);
            if (quotedStructures.Count == 0)
            {
                if (!skipInitialLine) container.Add(line);
                return;
            }
            List<IBlock> contents = [.. quotedStructures];
            if (!skipInitialLine) contents.Insert(0, line);
            Mod mod = new() { Contents = contents };
            container.Add(mod);
        }

        /*
         * Determines if the next line is an extra paragraph belonging to the current division.
         * If so, it returns the paragraph. If not, it returns null.
        */
        private IList<IBlock> GetExtraParagraph(WLine leader, System.Func<WLine, HContainer> nextExpected = null)
        {
            if (BreakFromProv1())
                return null;

            IDivision next = ParseNextBodyDivision(nextExpected);

            if (next is WDummyDivision dummy && dummy.Contents.Count() == 1)
            {

                if (dummy.Contents.First() is WTable table)
                    return [table];
                if (dummy.Contents.First() is LdappTableBlock tableBlock)
                    return [tableBlock];
            }
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
                if (!firstLine.IsLeftAligned())
                    return null;
                // If the leader is left-aligned, we enforce that extra pargraphs be must indented further than the leader.
                // Except when the leader is a Schedule number, which is potentially left-aligned if it has a reference 
                // note on the same line, in which case we pretend it's center-aligned.
                string numberText = GetScheduleNumber(leader, true);
                bool isScheduleNumber = LanguageService.IsMatch(numberText, Schedule.NumberPatterns);
                if (LineIsIndentedLessThan(firstLine, leader) && !isScheduleNumber)
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

        /// <summary>
        /// Peeks at the next line and breaks from the current Prov1/SchProv1 element if
        /// the line represents the beginning of a following sibling or grouping provision
        /// (which indicates that the current Prov1/SchProv1 element has come to an end).
        /// </summary>
        /// <remarks>
        /// This ensures that sequences of Prov1/SchProv1 elements are parsed iteratively rather than
        /// recursively, cutting down on the maximum recursion depth and lowering the risk of stack overflow.
        /// </remarks>
        /// <returns>Whether to break from the current Prov1/SchProv1 element.</returns>
        private bool BreakFromProv1()
        {
            if (Current() is not WLine line)
                return false;

            // If centre-aligned, it must be the start of a new grouping provision
            if (line.IsCenterAligned() && Peek(LdappTableNumber.Parse) is null)
                return true;

            // If we reach the heading of another Prov1, this Prov1 must be over
            if (provisionRecords.IsInProv1(quoteDepth) && PeekProv1(line))
                return true;

            // If we encounter what appears to be a headingless Prov1/SchProv1
            // whose number is the next number in the sequence, this Prov1 must be over
            if (isSubsequentProv1(line) || isSubsequentSchProv1(line))
            {
                string parentNum = GetNumString(provisionRecords.CurrentNumber(quoteDepth));
                string childNum = GetNumString(line);
                return IsSubsequentAlphanumeric(parentNum, childNum);
            }

            return false;
        }

        private bool isSubsequentProv1(WLine line) => provisionRecords.IsInProv1(quoteDepth) && PeekBareProv1(line);

        private bool isSubsequentSchProv1(WLine line) => provisionRecords.IsInSchProv1(quoteDepth) && PeekSchProv1(line);

        private string GetNumString(WLine line)
        {
            if (line is not WOldNumberedParagraph np)
                return null;
            return GetNumString(np.Number);
        }

        private string GetNumString(IFormattedText number)
        {
            return IgnoreQuotedStructureStart(number.Text, quoteDepth);
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="hi"/> immediately follows
        /// <paramref name="lo"/> in alphanumeric sequence.
        /// </summary>
        /// <param name="lo">
        /// The lower (preceding) alphanumeric value.
        /// </param>
        /// <param name="hi">
        /// The higher (succeeding) alphanumeric value.
        /// </param>
        /// <returns>
        /// <c>true</c> if <paramref name="hi"/> represents the next value
        /// in sequence after <paramref name="lo"/> otherwise, <c>false</c>.
        /// </returns>
        private static bool IsSubsequentAlphanumeric(string lo, string hi)
        {
            if (lo is null || hi is null)
                return false;

            lo = lo.Replace(".", " ").Trim();
            hi = hi.Replace(".", " ").Trim();

            // Check direct increments
            // For example: 1 -> 2, 1A -> 1B, 1Z1D -> 1Z1E
            if (IncrementAlphanumeric(lo) == hi)
                return true;

            // Check direct decrements
            // For example: A1 -> 1, AZ1 -> A1
            if (DecrementAlphanumeric(hi) == lo)
                return true;

            // Check for the addition of any number of 'Z' followed by an 'A'
            // For example: 1 -> 1A, 2B -> 2BA, 3 -> 3ZA, 7 -> 7ZZA
            if (hi.Length > lo.Length)
            {
                string diff = Regex.Replace(hi, @$"^{lo}", "");
                if (Regex.IsMatch(diff, @"^Z*A$"))
                    return true;
            }

            if (hi.Length < lo.Length)
            {
                // Check for when a character is dropped, then the number incremented
                // i.e. (1B -> 2, 1CA -> 1D)
                string loTrimmed = string.Concat(lo.SkipLast(1));
                if (IncrementAlphanumeric(loTrimmed) == hi)
                    return true;
                // Check for when every char after the final Z is dropped, then the number incremented
                // i.e. (1Z1 -> 2, 3Z10 -> 4)
                loTrimmed = Regex.Replace(lo, @"Z\d+$", "");
                if (IncrementAlphanumeric(loTrimmed) == hi)
                    return true;
                // Check for when every char after the final Z is dropped, and an 'A' or '1' is added
                // 5ZC -> 5A, AZ3 -> A1
                string diff = Regex.Replace(hi, @$"^{loTrimmed}", "");
                if (hi.StartsWith(loTrimmed) && (diff == "A" || diff == "1"))
                    return true;

            }
            return false;
        }

        /// <summary>
        /// Increments the given alphanumeric value by one step.
        /// </summary>
        /// <param name="numString">
        /// The alphanumeric value to increment. This may contain digits, letters, or a combination thereof.
        /// </param>
        /// <returns>
        /// A string representing the next value in the sequence after <paramref name="numString"/>.
        /// </returns>
        /// <remarks>
        /// Examples:
        /// <list type="bullet">
        /// <item><c>"1"</c> → <c>"2"</c></item>
        /// <item><c>"A3"</c> → <c>"A4"</c></item>
        /// <item><c>"5B"</c> → <c>"5C"</c></item>
        /// </list>
        /// </remarks>
        private static string IncrementAlphanumeric(string numString)
        {
            // If the num is entirely numeric, simply increment it
            // For example: 1 -> 2
            bool isInt = int.TryParse(numString, out int numInt);
            if (isInt)
                return (numInt + 1).ToString();

            // If the num ends with a numeric portion, increment that portion
            // For example: A9 -> A10, 1Z1 -> 1Z2
            string finalDigitsString = Regex.Match(numString, @"\d+$").Value;
            if (finalDigitsString.Length > 0)
            {
                int.TryParse(finalDigitsString, out int finalDigitsInt);
                return Regex.Replace(numString, @"\d+$", (finalDigitsInt + 1).ToString());
            }

            // If the num ends with an alphabetic portion, increment that portion
            // For example: 1A -> 1B, 1Z1D -> 1Z1E
            char finalChar = numString.Last();
            // Special case: the increment of 'Z' is 'Z1'
            if (finalChar == 'Z')
                return numString + '1';
            // Special case: the increment of 'N' is 'P' (skips 'O')
            if (finalChar == 'N')
                return new string(numString.SkipLast(1).Append('P').ToArray());
            // Typical case
            char charIncrement = (char)(finalChar + 1);
            return new string(numString.SkipLast(1).Append(charIncrement).ToArray());
        }

        /// <summary>
        /// Decrements the given alphanumeric value by one step.
        /// </summary>
        /// <remarks>
        /// Note that this is special logic used only when a child element is inserted before an existing element
        /// with a number ending in a '1' or 'A'. For example, A1 comes before 1, and 3ZA comes before 3A.
        /// </remarks>
        /// <param name="numString">
        /// The alphanumeric number to decrement.
        /// </param>
        /// <returns>
        /// A string representing the previous value in the sequence before <paramref name="numString"/>.
        /// </returns>
        private static string DecrementAlphanumeric(string numString)
        {
            bool isInt = int.TryParse(numString, out int numInt);
            // For purely numeric nums, 'A' is prepended to decrement
            // For example: A1 -> 1
            if (isInt)
                return 'A' + numString;

            // Otherwise, a 'Z' is placed in the second last position to decrement
            // For example: AZ1 -> A1, 1ZA -> 1A
            return numString.Substring(0, numString.Length - 1) + "Z" + numString.Substring(numString.Length - 1);
        }

        /// <summary>
        /// Determines whether <paramref name="hi"/> is the immediate subsequent
        /// alphabetical value after <paramref name="lo"/>.
        /// </summary>
        /// <param name="lo">The lower (preceding) alphanumeric string.</param>
        /// <param name="hi">The higher (succeeding) alphanumeric string.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="hi"/> immediately follows <paramref name="lo"/> 
        /// alphabetically; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Examples:
        /// <list type="bullet">
        /// <item><c>"A"</c> → <c>"B"</c></item>
        /// <item><c>"AA"</c> → <c>"AB"</c></item>
        /// <item><c>"AD"</c> → <c>"B"</c></item>
        /// <item><c>"D"</c> → <c>"DA"</c></item>
        /// </list>
        /// </remarks>
        private static bool IsSubsequentAlphabetic(string lo, string hi)
        {
            lo = lo.Trim('(', ')');
            hi = hi.Trim('(', ')');

            // Case 1: Strings are the same length, final character increments
            // i.e. (A -> B, AA -> AB)
            if (lo.Length == hi.Length)
            {
                char loFinalChar = char.ToUpperInvariant(lo.Last());
                char hiFinalChar = char.ToUpperInvariant(hi.Last());
                return hiFinalChar - loFinalChar == 1;
            }
            // Case 2: A character is dropped, then the prefix incremented
            // i.e. (AD -> B)
            if (lo.Length - hi.Length == 1)
            {
                return IsSubsequentAlphabetic(
                    lo.Substring(0, lo.Length - 1),
                    hi
                );
            }
            // Case 3: A character is added, must be 'A'
            // i.e. (D -> DA)
            if (hi.Length - lo.Length == 1)
            {
                return char.ToUpperInvariant(hi.Last()) == 'A';
            }

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
            if (!line.IsCenterAligned())
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

        #nullable enable
        /*
         * Attempts to identify the current line as one of a small number of provisions
         * which can exist as the very first provision in the body of a document.
         * Otherwise, returns null.
         */
        private HContainer? PeekBodyStartProvision()
        {
            if (Current() is not WLine line)
                return null;
            return PeekBodyStartProvision(line);
        }
        private HContainer? PeekBodyStartProvision(WLine? line)
        {

            if (line is null) return null;
            if (PeekGroupOfPartsHeading(line))
                return new GroupOfPartsLeaf { };
            if (PeekPartHeading(line))
                return new PartLeaf { };
            if (PeekProv1(line))
                return new Prov1Leaf { TagName = GetProv1Name() };
            if (PeekSchedules(line))
                return new Schedules { };
            if (PeekSchedule(line))
                return new ScheduleLeaf { };

            return null;
        }

        internal bool IsStartOfBody() => PeekBodyStartProvision() is not null;

        /// <summary>
        /// Returns the <c>Leaf</c> content (if present) following the <paramref name="heading"/>
        /// of a grouping provision. Otherwise, returns an empty list.
        /// </summary>
        /// <param name="heading">The line representing the schedule heading.</param>
        /// <returns>A list of <c>Leaf</c> content.</returns>
        private List<IBlock> ParseLeafContents(WLine heading)
        {
            int save = i;
            List<IBlock> contents = [];

            // Handle when schedule content begins immediately with one or more quoted structures.
            HandleMod(heading, contents, true);
            if (contents.Count > 0)
                return contents;

            // Handle all other schedule content.
            // If the next line(s) do not constitute a division, handle them as paragraphs (or tables).
            IDivision next = ParseNextBodyDivision();
            i = save;
            if (next is UnnumberedLeaf || next is UnknownLevel || next is WDummyDivision)
                contents = HandleParagraphs(heading).Skip(1).ToList();
            return contents;
        }

    }
}
