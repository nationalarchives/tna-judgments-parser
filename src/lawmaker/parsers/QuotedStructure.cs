
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

        private static (int, int) CountLeftAndRightQuotes(WLine line)
        {
            int left = 0;
            int right = 0;
            foreach (char c in line.TextContent)
            {
                if (c == '\u201C')
                    left++;
                else if (c == '\u201D')
                    right++;
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

        private static bool IsEndOfQuotedStructure(HContainer hContainer)
        {
            WLine line = LastLine.GetLastLine(hContainer);
            if (line is null)
                return false;
            string text = line.NormalizedContent;
            return Regex.IsMatch(text, QuotedStructureEndPattern());
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

        private BlockQuotedStructure ParseQuotedStructure(int childCount)
        {
            if (childCount > 0)
                // A quoted structure cannot occur after a child
                return null;
            if (i == Document.Body.Count)
                return null;
            IBlock block = Document.Body[i].Block;
            if (block is not WLine line)
                return null;
            return ParseAndMemoize(line, null, "QuotedStructure", ParseQuotedStructure);
        }

        private BlockQuotedStructure ParseQuotedStructure(WLine line, string startQuote)
        {
            (bool isFirstLineOfQuotedStructure, startQuote) = IsFirstLineOfQuotedStructure(line);
            if (!isFirstLineOfQuotedStructure)
                return null;

            List<IDivision> contents = [];
            quoteDepth += 1;
            while (i < Document.Body.Count)
            {
                int save = i;
                var child = ParseLine(startQuote);
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
                    AppendText = new WText(match.Groups[1].Value, last.properties);
                    WText replacement = new(last.Text[..match.Index], last.properties);
                    return WLine.Make(line, line.Contents.SkipLast(1).Append(replacement));
                }
                return null;
            }

        }

    }

}
