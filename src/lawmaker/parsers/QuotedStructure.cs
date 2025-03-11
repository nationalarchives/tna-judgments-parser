
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

        private static (int, int) CountLeftAndRightQuotes(string line)
        {
            int left = 0;
            int right = 0;
            foreach (char c in line)
            {
                if (c == '\u201C')
                    left++;
                else if (c == '\u201D')
                    right++;
            }
            return (left, right);
        }

        private static (int, int) CountLeftAndRightQuotes(WLine line)
        {
            return CountLeftAndRightQuotes(line.TextContent);
        }

        private static (int, int) CountLeftAndRightQuotes(IEnumerable<IBlock> blocks)
        {
            int left = 0;
            int right = 0;
            foreach (IBlock block in blocks)
            {
                if (block is not WLine line)
                    continue;
                (int addToLeft, int addtoRight) = CountLeftAndRightQuotes(line);
                left += addToLeft;
                right += addtoRight;
            }
            return (left, right);
        }

        private (bool, string) IsFirstLineOfQuotedStructure(WLine line)
        {
            string text;
            if (line is WOldNumberedParagraph np)
                text = np.Number.Text + " " + line.NormalizedContent;
            else
                text = line.NormalizedContent;

            if (text.StartsWith('\u201C'))
            {
                var (left, right) = CountLeftAndRightQuotes(line);
                if (left > right)
                    return (true, "\u201C");
                // Handle single-paragraph quoted structures
                else if (left == right && Regex.IsMatch(text, QuotedStructureEndPattern()))
                    return (true, "\u201C");
            }
            if (i == 0)
                return (false, null);
            if (Document.Body[i - 1].Block is not WLine previous)
                return (false, null);
            // currently IsFirstLineOfQuotedStructure works only with current line
            // if (IsFirstLineOfQuotedStructure(previous))
            //     return false;
            if (previous.NormalizedContent.StartsWith('\u201C'))
                return (false, null);
            var (prevLeft, prevRight) = CountLeftAndRightQuotes(previous);
            return (prevLeft > prevRight, null);
        }

        private string GetStartQuote()
        {
            if (Current() is not WLine line)
                return null;

            string text;
            if (line is WOldNumberedParagraph np)
                text = np.Number.Text + " " + line.NormalizedContent;
            else
                text = line.NormalizedContent;

            if (text.StartsWith('\u201C'))
            {
                var (left, right) = CountLeftAndRightQuotes(line);
                if (left > right)
                    return "\u201C";
                // Handle single-paragraph quoted structures
                else if (left == right && Regex.IsMatch(text, QuotedStructureEndPattern()))
                    return "\u201C";
            }
            return null;
        }

        /*
        private static bool IsEndOfQuotedStructureOld(IDivision division)
        {
            WLine line = LastLine.GetLastLine(division);
            return IsEndOfQuotedStructure(line);
        }
        */

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
            bool isEndQuoteAtEnd = Regex.IsMatch(text, QuotedStructureEndPattern());
            if (!isEndQuoteAtEnd)
                return false;

            bool isStartQuoteAtStart = text.StartsWith("\u201C");
            (int left, int right) = CountLeftAndRightQuotes(text);

            bool isSingleLine = (isStartQuoteAtStart && isEndQuoteAtEnd);
            bool isEndOfMultiLine = (!isStartQuoteAtStart && isEndQuoteAtEnd && right > left);
            return isSingleLine || isEndOfMultiLine;
        }

        private static string QuotedStructureEndPattern()
        {
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
            return $"\u201D{followingTextRegex}?$";
        }

        private static string IgnoreStartQuote(string text, int quoteDepth)
        {
            if (quoteDepth > 0 && text.StartsWith("\u201C"))
                return text[1..];
            return text;
        }

        private List<IQuotedStructure> HandleQuotedStructures(WLine line)
        {
            List<IQuotedStructure> quotedStructures = [];
            if (i == Document.Body.Count)
                return [];
            int save = i;

            (int left, int right) = CountLeftAndRightQuotes(line);
            // This extra left/right quote check ensures that definitions
            // are not misparsed as quoted structures
            bool hasStartQuote = line.TextContent.StartsWith("\u201C") && (left == right + 1);

            // Handle the case where the first quoted structure has no start quote
            if (!hasStartQuote && left > right)
            {
                BlockQuotedStructure qs = ParseQuotedStructure();
                if (qs != null)
                    quotedStructures.Add(qs);
                else
                    i = save;
            }

            // Handle regular quoted structures
            while (i < Document.Body.Count)
            {
                save = i;
                string startQuote = GetStartQuote();
                if (startQuote == null)
                {
                    i = save;
                    break;
                }
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
            if (block is Mod mod)
            {
                foreach (IBlock modBlock in mod.Contents)
                    ExtractQuotesAndAppendTexts(modBlock);
                return;
            }

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

            public WLine Invoke(WLine line)
            {
                if (line.Contents.LastOrDefault() is not WText last)
                    return null;
                Match match = Regex.Match(last.Text, EndPattern);
                if (match.Success)
                {
                    EndQuote = "\u201D";
                    AppendText = new WText(match.Groups[1].Value, last.properties);
                    WText replacement = new(last.Text[..match.Index], last.properties);
                    return WLine.Make(line, line.Contents.SkipLast(1).Append(replacement));
                }
                return null;
            }

        }

    }

}
