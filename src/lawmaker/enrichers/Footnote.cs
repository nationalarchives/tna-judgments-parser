
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{
    /*
      Footnotes in Lawmaker Statutory Instruments are drafted with two redundant sets of parentheses— 
      one around the footnote reference, and one inside the footnote itself (at the beginning).
      This enricher serves to strip these parentheses.
    */
    class FootnoteEnricher : LineEnricher
    {

        /*
         * Strip redundant parentheses from AROUND the footnote 
         * (i.e. from the end of the previous line, and/or from the start of the next line). 
         */
        internal override WLine EnrichLine(WLine raw)
        {
            List<IInline> contents = raw.Contents.ToList();
            List<IInline> enrichedInlines = [];

            for (int i = 0; i < contents.Count(); i++)
            {
                IInline prev = i > 0 ? contents[i - 1] : null;
                IInline current = contents[i];
                IInline next = i < contents.Count - 1 ? contents[i + 1] : null;

                if (current is WText text && text.Text.StartsWith(")") && prev is WFootnote)
                    current = new WText(text.Text[1..], text.properties);
                if (current is WText text2 && text2.Text.EndsWith("(") && next is WFootnote)
                    current = new WText(text2.Text[..^1], text2.properties);

                enrichedInlines.Add(current);
            }
            WLine newLine = new WLine(raw, enrichedInlines);
            if (raw is WUnknownLine)
                return new WUnknownLine(newLine);
            return newLine;
        }

        // Strip redundant parentheses from INSIDE the footnote (at the beginning).
        public static IEnumerable<IBlock> EnrichInside(IEnumerable<IBlock> content)
        {
            if (content.First() is WLine firstLine && firstLine.TextContent.StartsWith("()"))
            {
                IEnumerable<IInline> modifiedContents = firstLine.Contents.SkipWhile(inline => {
                    return !(inline is WText wText) || Regex.IsMatch(wText.Text, @"^\s*(\(|\))\s*$");
                });
                return content.Skip(1).Prepend(new WLine(firstLine, modifiedContents));
            }

            return content;
        }

    }

}
