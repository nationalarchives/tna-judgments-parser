
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseSchProv1(WLine line, string startQuote)
        {
            bool quoted = quoteDepth > 0;
            if (!IsFlushLeft(line) && !quoted)
                return null;
            if (line is not WOldNumberedParagraph np)
                return null;
            string numText = (startQuote == null) ? np.Number.Text : np.Number.Text[1..];
            if (!SchProv1.IsValidNumber(numText))
                return null;

            int save = i;
            i += 1;
            HContainer next = Parse(line, np);
            if (next is null)
            {
                i = save;
                return null;
            }

            return next;
        }

        private HContainer Parse(WLine line, WOldNumberedParagraph np)
        {
            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            if (i == Document.Body.Count)
                return new SchProv1Leaf { Number = num, Contents = intro };

            List<IDivision> children = [];
            List<IBlock> wrapUp = [];

            FixFirstSchProv2(intro, children);

            while (i < Document.Body.Count)
            {
                int save = i;
                BlockQuotedStructure qs = ParseQuotedStructure(children.Count);
                if (qs != null)
                {
                    intro.Add(qs);
                    continue;
                }
                i = save;

                if (BreakFromProv1(line))
                    break;

                IBlock childStartLine = Current();
                IDivision next = ParseNextBodyDivision();
                if (IsExtraIntroLine(next, childStartLine, np, children.Count))
                {
                    intro.Add(childStartLine);
                    continue;
                }
                if (!SchProv1.IsValidChild(next))
                {
                    i = save;
                    break;
                }
                if (!HasValidIndentForChild(childStartLine, line))
                {
                    List<IBlock> addToWrapUp = HandleWrapUp2(next, children.Count);
                    if (addToWrapUp.Count > 0)
                        wrapUp.AddRange(addToWrapUp);
                    else
                        i = save;
                    break;
                }
                children.Add(next);
            }

            if (children.Count == 0)
                return new SchProv1Leaf { Number = num, Contents = intro };
            else
                return new SchProv1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

        private void FixFirstSchProv2(List<IBlock> intro, List<IDivision> children, WLine heading = null)
        {
            if (intro.Last() is not WLine last || last is WOldNumberedParagraph)
                return;

            (WText num1, WLine rest1) = FixFirstProv2Num(last);
            if (num1 is null)
                return;

            intro.Remove(last);
            intro.Add(rest1);

            List<IBlock> prov2WrapUp = [];
            List<IDivision> prov2Children = ParseSchProv2Children(last, intro, prov2WrapUp);

            SchProv2 l;
            if (prov2Children.Count == 0)
            {
                List<IBlock> contents = new(intro);
                AddFollowingToContent(heading ?? last, contents);
                l = new SchProv2Leaf { Number = num1, Contents = contents };
            }
            else
            {
                List<IBlock> contents = new(intro);
                l = new SchProv2Branch { Number = num1, Intro = contents, Children = prov2Children, WrapUp = prov2WrapUp };
            }
            intro.Clear();
            children.Insert(0, l);
        }
    }

}