
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer ParsePara1(WLine line)
        {
            if (line is not WOldNumberedParagraph np)
                return null;
            string numText = IgnoreQuotedStructureStart(np.Number.Text, quoteDepth);
            if (!Para1.IsValidNumber(numText))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = HandleParagraphs(np);

            if (IsEndOfQuotedStructure(intro))
                return new Para1Leaf { Number = num, Contents = intro };

            List<IDivision> children = [];
            List<IBlock> wrapUp = [];

            int finalChildStart = i;
            while (i < Body.Count)
            {
                if (BreakFromProv1())
                    break;

                int save = i;

                // Para1 numbers: lowercase letters (a, b, c, ...)
                // Para2 numbers: lowercase roman numerals (i, ii, iii, ...)

                // Because roman numerals are a subset of letters, the Para1 parser would
                // normally consume all Para2 numbers. To handle this correctly, we explicitly
                // give the Para2 parser higher precedence when parsing the next division.
                IDivision next = ParseNextBodyDivision(
                    l => ParseAndMemoize(l, "Para2", ParsePara2)
                );

                // Special case: if the next number immediately follows the
                // previous Para1 number (e.g., h -> i, k -> l, u -> v, w -> x)
                // we treat it as a Para1 instead of Para2, despite being roman.
                bool nextIsPara1 = next is Para2
                    && IsSubsequentAlphabetic(num.Text, next.Number.Text)
                    && children.Count == 0;

                if (nextIsPara1 || !Para1.IsValidChild(next))
                {
                    i = save;
                    break;
                }

                children.Add(next);
                finalChildStart = save;

                if (IsEndOfQuotedStructure(next))
                    break;
            }
            wrapUp.AddRange(HandleWrapUp(children, finalChildStart));

            if (children.Count == 0)
                return new Para1Leaf { Number = num, Contents = intro };
            else
                return new Para1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

    }

}
