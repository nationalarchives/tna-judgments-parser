
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseSchProv1(WLine line)
        {
            if (!IsFlushLeft(line))
                return null;
            if (line is not WOldNumberedParagraph np)
                return null;
            if (!SchProv1.IsValidNumber(np.Number.Text))
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

            FixFirstSchProv2(intro, children);

            if (children.Count == 0)
                AddFollowingToIntroOrWrapUp(line, intro);

            while (i < Document.Body.Count)
            {
                if (!CurrentIsPossibleSchProv1Child(line))
                    break;
                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!SchProv1.IsValidChild(next))
                {
                    i = save;
                    break;
                }
                if (!NextChildIsAcceptable(children, next))
                {
                    i = save;
                    break;
                }
                children.Add(next);
            }

            if (children.Count == 0)
                return new SchProv1Leaf { Number = num, Contents = intro };

            List<IBlock> wrapUp = [];
            if (children.Last() is UnnumberedLeaf leaf)
            {
                children.RemoveAt(children.Count - 1);
                wrapUp = [.. leaf.Contents];
            }
            return new SchProv1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

        private bool CurrentIsPossibleSchProv1Child(WLine leader)
        {
            if (Current() is not WLine line)
                return true;
            if (!IsLeftAligned(line))
                return false;
            if (LineIsIndentedLessThan(line, leader))
                return false;
            return true;
        }

        private void FixFirstSchProv2(List<IBlock> intro, List<IDivision> children, WLine heading = null)
        {
            if (intro.Last() is not WLine last || last is WOldNumberedParagraph)
                return;

            (WText num1, WLine rest1) = FixFirstProv2Num(last);
            if (num1 is null)
                return;

            // Indentation seems to be bugged when parsing children of first SchProv2
            bool ignoreIndentation = true;
            List<IDivision> grandchildren = ParseSchProv2Children(last, ignoreIndentation);

            SchProv2 l;
            if (grandchildren.Count == 0)
            {
                List<IBlock> contents = [rest1];
                AddFollowingToContent(heading ?? last, contents);
                l = new SchProv2Leaf { Number = num1, Contents = contents };
            }
            else
            {
                List<IBlock> wrapUp = [];
                AddFollowingToIntroOrWrapUp(heading ?? last, wrapUp);
                l = new SchProv2Branch { Number = num1, Intro = [rest1], Children = grandchildren, WrapUp = wrapUp };
            }
            intro.RemoveAt(intro.Count - 1);
            children.Insert(0, l);
        }
    }

}