
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
            Leaf finalLeaf = FinalLeaf(division);
            return IsEndOfQuotedStructure(
                finalLeaf.Contents,
                finalLeaf.Heading,
                finalLeaf.Number,
                finalLeaf.HeadingPrecedesNumber
            );
        }

        /*
        private static bool IsEndOfQuotedStructureOld(IBlock block)
        {
            if (block is null)
                return false;
            if (block is not WLine line)
                return false;

            string text = line.NormalizedContent;
            bool isEndQuoteAtEnd = Regex.IsMatch(text, QuotedStructureEndPattern());
            if (!isEndQuoteAtEnd)
                return false;

            bool isStartQuoteAtStart = startQuote != null;
            (int left, int right) = CountLeftAndRightQuotes(line);
            //if (line.TextContent.StartsWith(startQuote)) left -= 1;

            bool isSingleLine = (isStartQuoteAtStart && isEndQuoteAtEnd);
            bool isEndOfMultiLine = (!isStartQuoteAtStart && isEndQuoteAtEnd && right > left);
            return isSingleLine || isEndOfMultiLine;
        }*/

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
            string allTextContent = IInline.ToString(inlines);

            // Analyse text
            bool isEndQuoteAtEnd = Regex.IsMatch(allTextContent, QuotedStructureEndPattern());
            if (!isEndQuoteAtEnd)
                return false;

            bool isStartQuoteAtStart = allTextContent.StartsWith("\u201C");
            (int left, int right) = CountLeftAndRightQuotes(allTextContent);

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

            // Handle the case where the first quoted structure has no start quote
            (int left, int right) = CountLeftAndRightQuotes(line);
            if (left > right)
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
                if (qs == null)
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
            if (block is not BlockQuotedStructure qs)
                return;
            if (qs.StartQuote is null && qs.Contents.FirstOrDefault() is HContainer first)
            {
                // First text item can be in the num, heading, intro, OR content.
                // Depends on the HContainer
                if (first.HeadingPrecedesNumber)
                {
                    /*
                    ILine line = first.Heading;
                    IInline firstInline = line.Contents.FirstOrDefault();
                    line.Contents.RemoveAt()
                    first.Heading = new WLine(line, )
                    */
                }
                else
                {
                    if (first.Number is not null && first.Number.Text.StartsWith('\u201C'))
                    {
                        if (first.Number is WText wText)
                            first.Number = new WText(first.Number.Text[1..], wText.properties);
                        else
                            first.Number = new WText(first.Number.Text[1..], null);
                        qs.StartQuote = "\u201C";
                    }
                }
            }
            var f = new ExtractAndReplace(QuotedStructureEndPattern());
            LastLine.Replace(qs.Contents, f.Invoke);
            qs.EndQuote = f.EndQuote;
            qs.AppendText = f.AppendText;
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
                    AppendText = new AppendText(match.Groups[1].Value, last.properties);
                    WText replacement = new(last.Text[..match.Index], last.properties);
                    return WLine.Make(line, line.Contents.SkipLast(1).Append(replacement));
                }
                return null;
            }

        }

    }

}
