
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Wordprocessing;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private int quoteDepth = 0;

        private static string startQuotePattern;
        private static string endQuotePattern;
        private static string quotedStructureStartPattern;
        private static string quotedStructureEndPattern;
        private static string quotedStructureInfoPattern = @"(?'info'{(?'docName'.*?)(?:-(?'context'.*?))?}\s*)?";

        public static int QuoteDistance = 0;

        /*
         * A quoted structure must begin with a start quote. Optionally, there may be 'info'
         * before the start quote surrounded in braces i.e. {ukpga-sch} which dictates the
         * 'doctype' and 'context' with which to parse the contents of the quoted structure.
         */
        private static string QuotedStructureStartPattern()
        {
            if (quotedStructureStartPattern is not null)
                return quotedStructureStartPattern;

            quotedStructureStartPattern = @$"^\s*({quotedStructureInfoPattern}{StartQuotePattern()})";
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
            startQuotePattern = $"(?'startQuote'{string.Join("|", possibleStartQuotes)})";
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
            string followingTextRegex = $"(?'followingText'{string.Join("|", possibleFollowingTexts)})";
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
            endQuotePattern = $"(?'endQuote'{string.Join("|", possibleEndQuotes)})";
            return endQuotePattern;
        }

        /*
         * Extracts the 'frame' info from the braces at the start of the quoted structure (if present),
         * and adds the frame to the stack. i.e. Given the quoted structure: {UKPGA-SCH}�quoted content�
         * The DocName is set to 'UKPGA', and the Context is set to 'SCH'.
         * If 'frame' info cannot be determined (i.e. is absent or malformed), the Context and DocName
         * of the quoted structure default to those of the overall document.
         * Returns false specifically when the 'frame' info is present, but malformed.
         */
        private bool AddQuotedStructureFrame(IBlock block)
        {
            if (block is not WLine line)
            {
                frames.PushDefault();
                return true;
            }
            string pattern = $"{quotedStructureInfoPattern}{StartQuotePattern()}";
            string text = Regex.Replace(line.NormalizedContent, @"\s+", string.Empty);
            MatchCollection matches = Regex.Matches(text, pattern);
            if (matches.Count == 0)
            {
                // Quoted structure start pattern could not be matched.
                // This should not be possible in practice.
                frames.PushDefault();
                return true;
            }
            GroupCollection groups = matches.First().Groups;
            if (!groups["info"].Success || !groups["docName"].Success)
            {
                // No frame info present - valid scenario.
                frames.PushDefault();
                return true;
            }
            DocName docName;
            if (!Enum.TryParse(groups["docName"].Value.ToUpper(), out docName))
            {
                // Frame info present but DocName is malformed - invalid scenario.
                frames.PushDefault();
                return false;
            }
            Context? context = Contexts.ToEnum(groups["context"].Value);
            Context defaultContext = Frames.IsSecondaryDocName(docName) ? Context.REGULATIONS : Context.SECTIONS;
            if (!groups["context"].Success)
            {
                // Frame info has DocName but no Context - valid scenario.
                // Resort to default Context.
                frames.Push(docName, defaultContext);
                return true;
            }
            if (context == null)
            {
                // Frame info has a Context, but it is malformed - invalid scenario.
                // Resort to default Context.
                frames.Push(docName, defaultContext);
                return false;
            }
            // Frame info has valid DocName and Context
            frames.Push(docName, (Context) context);
            return true;
        }

        private static (int, int) CountStartAndEndQuotes(string text)
        {
            int start = Regex.Matches(text, StartQuotePattern()).Count;
            int end = Regex.Matches(text, EndQuotePattern()).Count;
            return (start, end);
        }

        private static (int, int) CountStartAndEndQuotes(IBlock block)
        {
            if (block is not WLine line)
                return (0, 0);
            return CountStartAndEndQuotes(line.TextContent);
        }

        private static (int, int) CountStartAndEndQuotes(IEnumerable<IBlock> blocks)
        {
            int start = 0;
            int end = 0;
            foreach (IBlock block in blocks)
            {
                (int addToStart, int addToEnd) = CountStartAndEndQuotes(block);
                start += addToStart;
                end += addToEnd;
            }
            return (start, end);
        }

        private bool IsStartOfQuotedStructure(IBlock block)
        {
            return block switch
            {
            WLine line => IsStartOfQuotedStructure(line),
            // possible BUG: there may be circumstances where the first line
            // is not necessarily the correct location to check, may be a
            // false positive.
            ILineable lineable => IsStartOfQuotedStructure(lineable.Lines.FirstOrDefault()),
            _ => false
            };
        }
        /*
         * Determines if a given block begins with a quoted structure.
         */
        private bool IsStartOfQuotedStructure(WLine line)
        {
            string text = Regex.Replace(line.NormalizedContent, @"\s+", string.Empty);
            if (!Regex.IsMatch(text, QuotedStructureStartPattern()))
                return false;

            var (left, right) = CountStartAndEndQuotes(text);
            // Handle start of multi-line quoted structures.
            // These feature more left quotes than right quotes, as the end quote will be on a later line.
            if (left > right)
                return true;
            // Handle single-paragraph quoted structures
            // Number of left and right quotes must match.
            // NOTE: I think there's a false positive:
            // “first quotes” some text “second quotes”
            // just another sign we need to re-work quoted structures
            else if (left == right && Regex.IsMatch(line.NormalizedContent, QuotedStructureEndPattern()))
                return true;
            return false;
        }

        private bool IsEndOfQuotedStructure(IDivision division)
        {
            QuoteDistance = 0;
            var lastLine = LastLine.GetLastLine(division)?.Contents;
            if (lastLine == null) return false;
            return IsEndOfQuotedStructure(IInline.ToString(lastLine), QuoteDistance);
        }

        private bool IsEndOfQuotedStructure(IList<IBlock> contents, ILine heading = null, IFormattedText number = null, bool headingPrecedesNumber = false)
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
            QuoteDistance = 0;
            var lastLine = LastLine.GetLastLine(contents)?.Contents;
            return IsEndOfQuotedStructure(IInline.ToString(lastLine), QuoteDistance);
        }

        private bool IsEndOfQuotedStructure(string text)
        {
            return IsEndOfQuotedStructure(text, 0);
        }

        private bool IsEndOfQuotedStructure(string text, int quoteDistance)
        {
            // a quoteDepth of 0 means we don't need to check for the end of a quoted structure because we never need to break out
            if (quoteDepth <= 0) return false;
            if (text == null)
                return false;
            bool isEndQuoteAtEnd = Regex.IsMatch(text, QuotedStructureEndPattern());
            if (!isEndQuoteAtEnd)
                return false;

            bool isStartQuoteAtStart = Regex.IsMatch(text, QuotedStructureStartPattern());
            (int left, int right) = CountStartAndEndQuotes(text);
            if (right > left && (right - left) <= quoteDistance)
                return false;

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

            // Handle the case where the start quote of the first quoted structure is NOT at the
            // start of a new line, but rather is at the end of the previous line.
            (int left, int right) = CountStartAndEndQuotes(line);
            bool isAtStartOfLine = (left == right + 1) && Regex.IsMatch(line.NormalizedContent, QuotedStructureStartPattern());
            if (left > right && !isAtStartOfLine)
            {
                bool isValidFrame = AddQuotedStructureFrame(line);
                BlockQuotedStructure qs = ParseQuotedStructure();
                qs.HasInvalidCode = !isValidFrame;
                if (qs != null)
                    quotedStructures.Add(qs);
                else
                    i = save;
                frames.Pop();
            }
            // Handle regular quoted structures
            while (i < Document.Body.Count && IsStartOfQuotedStructure(Current()))
            {
                save = i;
                bool isValidFrame = AddQuotedStructureFrame(Current());
                BlockQuotedStructure qs = ParseQuotedStructure();
                qs.HasInvalidCode = !isValidFrame;
                if (qs == null)
                {
                    i = save;
                    break;
                }
                quotedStructures.Add(qs);
                frames.Pop();
            }
            return quotedStructures;
        }

        private BlockQuotedStructure ParseQuotedStructure()
        {
            if (i == Document.Body.Count)
                return null;
            return Current() switch
            {
                WLine line => ParseAndMemoize(line, "QuotedStructure", ParseQuotedStructure),
                LdappTableBlock table => ParseQuotedStructure(table),
                WTable table => ParseQuotedStructure(table),
                _ => null
            };
        }

        // only runs when `IsStartOfQuotedStructure` is true, this is bad code but quoted structures need re-working anyway
        private BlockQuotedStructure ParseQuotedStructure(LdappTableBlock table)
        {
            i++;
            return new BlockQuotedStructure
            {
                Contents = [new WDummyDivision(table)],
                DocName = frames.CurrentDocName,
                Context = frames.CurrentContext
            };
        }

        private BlockQuotedStructure ParseQuotedStructure(WTable table)
        {
            i++;
            return new BlockQuotedStructure
            {
                Contents = [new WDummyDivision(table)],
                DocName = frames.CurrentDocName,
                Context = frames.CurrentContext
            };
        }

        private BlockQuotedStructure ParseQuotedStructure(WLine line) // why
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
            return new BlockQuotedStructure { Contents = contents, DocName = frames.CurrentDocName, Context = frames.CurrentContext };
        }

        #region Quote extraction
        #nullable enable
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
            qs.StartQuote = qs.Contents.FirstOrDefault() switch
            {
            HContainer hContainer => ExtractStartQuote(hContainer),
            WDummyDivision dummy => ExtractStartQuote(dummy),
            _ => null
            };
            return;
        }

        private static string? ExtractStartQuote(WDummyDivision dummy)
        {
            if (dummy.Contents.Count() > 1) return null;
            return dummy.Contents.FirstOrDefault() switch
            {
            // ILineable lineable => ExtractStartQuote(lineable.Lines.FirstOrDefault()),
            WTable table => ExtractStartQuote(table?.Rows?.FirstOrDefault()?.Cells?.FirstOrDefault()?.Contents?.FirstOrDefault() as WLine),
            _ => null,
            };

        }

        private static string? ExtractStartQuote(HContainer hContainer)
        {
            // First text item can be in the num, heading, intro, OR content.
            if (hContainer is null) return null;
            if (hContainer.HeadingPrecedesNumber && hContainer.Heading is WLine heading)
            {
                WLine enriched = EnrichFromBeginning.Enrich(heading, QuotedStructureStartPattern(), ExtractStartQuoteConstructor);
                WBookmark? bookmark = enriched.Contents.First(i => i is WBookmark) as WBookmark;
                heading.Contents = enriched.Contents.Where(i => i is not WBookmark);
                return bookmark?.Name;
            }
            else if (hContainer.Number is not null)
            {
                Match match = Regex.Match(hContainer.Number.Text, QuotedStructureStartPattern());
                if (!match.Success)
                    return null;
                int patternEndIndex = match.Index + match.Length;
                RunProperties? runProperties = hContainer.Number is WText wText ? wText.properties : null;
                hContainer.Number = new WText(hContainer.Number.Text[patternEndIndex..], runProperties);
                return match.Groups["startQuote"].Value;
            }
            else
            {
                IList<IBlock>? container = hContainer switch
                {
                    Leaf leaf => leaf.Contents,
                    Branch branch => branch.Intro,
                    _ => null
                };

                // This currently ONLY removes the start quote when the first block is a WLine
                if (container is null || container.Count == 0 || container.First() is null)
                    return null;
                if (container.First() is not WLine line)
                    return null;

                string? startQuote = ExtractStartQuote(line);
                if (startQuote is not null)
                {
                    container.RemoveAt(0);
                    container.Insert(0, line);
                }
                return startQuote;
            }
        }

        private static string? ExtractStartQuote(WLine? line)
        {
            if (line is null) return null;
            WLine enriched = EnrichFromBeginning.Enrich(line, QuotedStructureStartPattern(), ExtractStartQuoteConstructor);
            WBookmark? bookmark = enriched.Contents.First(i => i is WBookmark) as WBookmark;
            line.Contents = enriched.Contents.Where(i => i is not WBookmark);
            return bookmark?.Name;
        }

        // Removes any braced quoted structure info, and wraps the start quote in a WBookmark so it
        // can later be extracted and added to the startQuote attribute of the quoted structure.
        static IInline? ExtractStartQuoteConstructor(IEnumerable<IInline> inlines)
        {
            if (inlines == null || !inlines.Any())
                return null;
            string text = "";
            foreach (IInline inline in inlines)
                text += IInline.GetText(inline);
            string? startQuote = text.Split("}").LastOrDefault();
            // Todo: should probably use something other than WBookmark
            return new WBookmark { Name = startQuote };
        }

        #nullable disable
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
                    if (inline is not WText wText)
                        return null;
                    endText = wText.Text + endText;
                    inlineCount += 1;
                    match = Regex.Match(endText, EndPattern);
                    if (match.Success)
                        break;
                }
                if (match is null || !match.Success)
                    return null;

                EndQuote = match.Groups["endQuote"].Value;
                WText lastWText = line.Contents.Last() as WText;
                AppendText = new AppendText(match.Groups["followingText"].Value, lastWText.properties);
                WText replacement = new(endText[..match.Index], lastWText.properties);
                return WLine.Make(line, line.Contents.SkipLast(inlineCount).Append(replacement));
            }

        }

    }
    #endregion

}
