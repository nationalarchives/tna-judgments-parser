
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private int quoteDepth = 0;

        private static string startQuotePattern;
        private static string endQuotePattern;
        private static string quotedStructureStartPattern;
        private static string quotedStructureEndPattern;

        /*
         * A quoted structure must begin with a start quote. Optionally, there may be a 
         * 'code' before the start quote surrounded in braces i.e. {ukpga-sch} which dictates 
         * the 'doctype' and 'context' with which to parse the contents of the quoted structure.
         */
        private static string QuotedStructureStartPattern()
        {
            if (quotedStructureStartPattern is not null)
                return quotedStructureStartPattern;

            string codeRegex = "({.*?})?";
            quotedStructureStartPattern = $"^{codeRegex}{StartQuotePattern()}";
            return quotedStructureStartPattern;
        }

        /*
         * Returns a regular expression representing a single possible start quote. 
         */
        private static string StartQuotePattern()
        {
            if (startQuotePattern is not null)
                return startQuotePattern;

            string[] possibleStartQuotes =
            {
                "\u201C"
            };
            startQuotePattern = $"({string.Join("|", possibleStartQuotes)})";
            return startQuotePattern;
        }

        /*
         * A quoted structure must terminate with an end quote. Optionally, there may be 
         * 'following text' after the end quote, which has a small number of possible patterns.
         */
        private static string QuotedStructureEndPattern()
        {
            if (quotedStructureEndPattern is not null)
                return quotedStructureEndPattern;

            string[] possibleFollowingTexts = {
                ".",
                ";",
                ",",
                ", or",
                ", and",
                "; or",
                "; and"
            };
            string followingTextRegex = $"({string.Join("|", possibleFollowingTexts)})";
            followingTextRegex = followingTextRegex.Replace(".", "\\.");
            quotedStructureEndPattern = $"{EndQuotePattern()}{followingTextRegex}?$";
            return quotedStructureEndPattern;
        }

        /*
         * Returns a regular expression representing a single possible end quote. 
         */
        private static string EndQuotePattern()
        {
            if (endQuotePattern is not null)
                return endQuotePattern;

            string[] possibleEndQuotes =
            {
                "\u201D"
            };
            endQuotePattern = $"({string.Join("|", possibleEndQuotes)})";
            return endQuotePattern;
        }

        private static (int, int) CountStartAndEndQuotes(string text)
        {
            int start = Regex.Matches(text, StartQuotePattern()).Count;
            int end = Regex.Matches(text, EndQuotePattern()).Count;
            return (start, end);
        }

        private static (int, int) CountStartAndEndQuotes(WLine line)
        {
            return CountStartAndEndQuotes(line.TextContent);
        }

        private static (int, int) CountStartAndEndQuotes(IEnumerable<IBlock> blocks)
        {
            int start = 0;
            int end = 0;
            foreach (IBlock block in blocks)
            {
                if (block is not WLine line)
                    continue;
                (int addToStart, int addToEnd) = CountStartAndEndQuotes(line);
                start += addToStart;
                end += addToEnd;
            }
            return (start, end);
        }

        /*
         * Determines if a given block begins with a quoted structure. 
         */
        private bool IsStartOfQuotedStructure(IBlock block)
        {
            if (block is not WLine line)
                return false;
            if (!Regex.IsMatch(line.NormalizedContent, QuotedStructureStartPattern()))
                return false;

            var (left, right) = CountStartAndEndQuotes(line);
            // Handle start of multi-line quoted structures.
            // These feature more left quotes than right quotes, as the end quote will be on a later line. 
            if (left > right)
                return true;
            // Handle single-paragraph quoted structures
            // Number of left and right quotes must match.
            else if (left == right && Regex.IsMatch(line.NormalizedContent, QuotedStructureEndPattern()))
                return true;
            return false;
        }

        private static bool IsEndOfQuotedStructure(IDivision division)
        {
            string lastParagraphText = LastLine.GetLastParagraphText(division);
            return IsEndOfQuotedStructure(lastParagraphText);
        }
        
        private static bool IsEndOfQuotedStructure(IList<IBlock> contents, ILine heading = null, IFormattedText number = null, bool headingPrecedesNumber = false)
        {
            // Squash text content into single string
            List<IInline> inlines = [];
            if (headingPrecedesNumber)
            {
                if (heading != null)
                    inlines.AddRange(heading.Contents);
                if (number != null)
                    inlines.Add(number);
            }
            else
            {
                if (number != null)
                    inlines.Add(number);
                if (heading != null)
                    inlines.AddRange(heading.Contents);
            }
            foreach (IBlock block in contents)
            {
                if (block is ILine line)
                    inlines.AddRange(line.Contents);
            }
            return IsEndOfQuotedStructure(IInline.ToString(inlines));
        }

        private static bool IsEndOfQuotedStructure(string text)
        {
            if (text == null)
                return false;
            bool isEndQuoteAtEnd = Regex.IsMatch(text, QuotedStructureEndPattern());
            if (!isEndQuoteAtEnd)
                return false;

            bool isStartQuoteAtStart = Regex.IsMatch(text, QuotedStructureStartPattern());
            (int left, int right) = CountStartAndEndQuotes(text);

            bool isSingleLine = (isStartQuoteAtStart && isEndQuoteAtEnd);
            bool isEndOfMultiLine = (!isStartQuoteAtStart && isEndQuoteAtEnd && right > left);
            return isSingleLine || isEndOfMultiLine;
        }
        

        /*
         * Strips the quoted structure start pattern (if present) from the beginning of the given string. 
         */
        private static string IgnoreQuotedStructureStart(string text, int quoteDepth)
        {
            if (quoteDepth == 0)
                return text;
            return Regex.Replace(text, QuotedStructureStartPattern(), "");
        }

        /*
         * Determines if a given line is followed by any quoted structures, and if so, 
         * parses each quoted structure and returns them in a list. 
         */
        private List<IQuotedStructure> HandleQuotedStructuresAfter(WLine line)
        {
            List<IQuotedStructure> quotedStructures = [];
            if (i == Document.Body.Count)
                return [];
            int save = i;

            // Handle the case where the start quote of the first quoted structure is NOT on a new line
            (int left, int right) = CountStartAndEndQuotes(line);
            if (left > right)
            {
                BlockQuotedStructure qs = ParseQuotedStructure();
                if (qs != null)
                    quotedStructures.Add(qs);
                else
                    i = save;
            }
            // Handle regular quoted structures
            while (i < Document.Body.Count && IsStartOfQuotedStructure(Current()))
            {
                save = i;
                BlockQuotedStructure qs = ParseQuotedStructure();
                // For now, quoted structures cannot begin with unnumbered paragraphs
                // as they are confused with extra paragraphs of the parent division
                if (qs == null || qs.Contents.First() is UnnumberedParagraph)
                {
                    i = save;
                    break;
                }
                quotedStructures.Add(qs);
            }
            return quotedStructures;
        }

        private BlockQuotedStructure ParseQuotedStructure()
        {
            if (i == Document.Body.Count)
                return null;
            IBlock block = Document.Body[i].Block;
            if (block is not WLine line)
                return null;
            return ParseAndMemoize(line, "QuotedStructure", ParseQuotedStructure);
        }
  
        private BlockQuotedStructure ParseQuotedStructure(WLine line)
        {
            List<IDivision> contents = [];
            quoteDepth += 1;
            // Assume the quoted structure contains section-based content
            bool isInSchedulesSave = isInSchedules;
            isInSchedules = false;
            while (i < Document.Body.Count)
            {
                int save = i;
                var child = ParseLine();
                if (child is null)
                {
                    i = save;
                    break;
                }
                contents.Add(child);
                if (IsEndOfQuotedStructure(child))
                    break;
            }
            quoteDepth -= 1;
            isInSchedules = isInSchedulesSave;
            if (contents.Count == 0)
                return null;
            return new BlockQuotedStructure { Contents = contents };
        }

        // extract start and end quote marks and appended text

        internal static void ExtractAllQuotesAndAppendTexts(IList<IDivision> body)
        {
            Util.WithEachBlock.Do(body, ExtractQuotesAndAppendTexts);
        }

        private static void ExtractQuotesAndAppendTexts(IBlock block)
        {
            if (block is not BlockQuotedStructure qs)
                return;

            ExtractStartQuote(qs);

            var f = new ExtractAndReplace(QuotedStructureEndPattern());
            LastLine.Replace(qs.Contents, f.Invoke);
            qs.EndQuote = f.EndQuote;
            qs.AppendText = f.AppendText;
        }

        private static void ExtractStartQuote(BlockQuotedStructure qs)
        {
            if (qs.StartQuote != null)
                return;
            if (qs.Contents.FirstOrDefault() is not HContainer hContainer)
                return;

            string startQuote = "\u201C";

            // First text item can be in the num, heading, intro, OR content.
            if (hContainer.HeadingPrecedesNumber && hContainer.Heading is WLine heading)
            {
                // Todo: this currently ONLY removes the start quote when the first inline is a WText.
                if (heading.Contents.First() is WText firstText && firstText.Text.StartsWith(startQuote))
                {
                    WText modified = new WText(firstText.Text[1..], firstText.properties);
                    heading.Contents = [modified, ..heading.Contents.Skip(1)];
                    qs.StartQuote = startQuote;
                }
            }
            else if (hContainer.Number is not null && hContainer.Number.Text.StartsWith(startQuote))
            {
                if (hContainer.Number is WText wText)
                    hContainer.Number = new WText(hContainer.Number.Text[1..], wText.properties);
                else
                    hContainer.Number = new WText(hContainer.Number.Text[1..], null);
                qs.StartQuote = startQuote;
            }
            else
            {
                IList<IBlock> container;
                if (hContainer is Leaf leaf)
                    container = leaf.Contents;
                else if (hContainer is Branch branch)
                    container = branch.Intro;
                else
                    return;

                // Todo: this currently ONLY removes the start quote when the first block is a WLine
                // and the inline is a WText.

                if (container is null || container.First() is null)
                    return;
                if (container.First() is not WLine line)
                    return;
                if (line.Contents.First() is not WText firstText)
                    return;
                if (!firstText.Text.StartsWith(startQuote))
                    return;

                WText modified = new WText(firstText.Text[1..], firstText.properties);
                line.Contents = [modified, .. line.Contents.Skip(1)];
                container.RemoveAt(0);
                container.Insert(0, line);
                qs.StartQuote = startQuote;
            }
        }

        /// <summary>
        /// This function removes the end quote and appended text from a line and returns a new line.
        /// It retains the extracted bits for insertion into the QuotedStructure model.
        /// </summary>
        private class ExtractAndReplace
        {
            internal string EndQuote { get; private set; } = null;
            internal AppendText AppendText { get; private set; } = null;

            internal string EndPattern { private get; set; }

            public ExtractAndReplace(string endPattern)
            {
                EndPattern = endPattern;
            }

            /*
              Note that the end quote and appended text may span across multiple of the line's inline children. 
              For example, when portions of the line have different styling.  
            */
            public WLine Invoke(WLine line)
            {
                Match match = null;
                string endText = "";
                int inlineCount = 0;
                foreach (IInline inline in line.Contents.Reverse())
                {
                    EndQuote = "\u201D";
                    AppendText = new WText(match.Groups[1].Value, last.properties);
                    WText replacement = new(last.Text[..match.Index], last.properties);
                    return WLine.Make(line, line.Contents.SkipLast(1).Append(replacement));
                }
                if (match is null || !match.Success)
                    return null;

                EndQuote = match.Groups[1].Value;
                WText lastWText = line.Contents.Last() as WText;
                AppendText = new AppendText(match.Groups[2].Value, lastWText.properties);
                WText replacement = new(endText[..match.Index], lastWText.properties);
                return WLine.Make(line, line.Contents.SkipLast(inlineCount).Append(replacement));
            }

        }

    }

}
