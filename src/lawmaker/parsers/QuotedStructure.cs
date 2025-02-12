
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

        private QuotedStructure ParseQuotedStructure()
        {
            if (i == Document.Body.Count)
                return null;
            IBlock block = Document.Body[i].Block;
            if (block is not WLine line)
                return null;
            return ParseQuotedStructure(line);
        }

        private QuotedStructure ParseQuotedStructure(WLine line)
        {
            if (line is not WOldNumberedParagraph np)
                return null;
            if (!np.Number.Text.StartsWith('“'))
                return null;
            WText newNum = new WText(np.Number.Text.TrimStart('“'), null);  // not ideal
            np = new WOldNumberedParagraph(newNum, np);

            quoteDepth += 1;

            int save = i;
            var p2 = ParseProv2(np);
            if (p2 is null)
            {
                quoteDepth -= 1;
                i = save;
                return null;
            }

            List<IDivision> contents = [p2];
            while (i < Document.Body.Count)
            {
                if (Document.Body[i].Block is not WLine next)
                    break;
                save = i;
                p2 = ParseProv2(next);
                if (p2 is null)
                {
                    i = save;
                    break;
                }
                if (LineIsIndentedLessThan(next, line))
                {
                    i = save;
                    break;
                }
                contents.Add(p2);
            }
            quoteDepth -= 1;
            return new QuotedStructure { Contents = contents, StartQuote = "“" };
        }

        // extract end quote marks and appended text

        internal static void ExtractAllEndQuotesAndAppendTexts(IList<IDivision> body)
        {
            Util.WithEachBlock.Do(body, ExtractEndQuotesAndAppendTexts);
        }

        private static void ExtractEndQuotesAndAppendTexts(IBlock block)
        {
            if (block is not QuotedStructure qs)
                return;
            var f = new ExtractAndReplace();
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
            internal WText AppendText { get; private set; } = null;

            private static readonly string pattern = @"”([\.;])?$";

            public WLine Invoke(WLine line)
            {
                if (line.Contents.LastOrDefault() is not WText last)
                    return null;
                Match match = Regex.Match(last.Text, pattern);
                if (match.Success) {
                    EndQuote = "”";
                    AppendText = new WText(match.Groups[1].Value, last.properties);
                    WText replacement = new(last.Text[..match.Index], last.properties);
                    return WLine.Make(line, line.Contents.SkipLast(1).Append(replacement));
                }
                return null;
            }

        }

    }

}
