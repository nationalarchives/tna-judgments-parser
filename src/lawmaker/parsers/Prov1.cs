
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        // matches only a heading above numbered section
        private HContainer ParseProv1(WLine line)
        {
            if (line is WOldNumberedParagraph)
                return null;  // could return ParseBaseProv1(np);
            if (!IsFlushLeft(line))
                return null;
            if (i > Document.Body.Count - 2)
                return null;
            if (Document.Body[i + 1].Block is not WOldNumberedParagraph np)
                return null;

            int save = i;
            i += 1;
            HContainer next = ParseBareProv1(np, line);
            if (next is null)
            {
                i = save;
                return null;
            }

            next.Heading = line;
            return next;
        }

        // matches only a numbered section without a heading
        private HContainer ParseBareProv1(WLine line, WLine heading = null)
        {
            if (!IsFlushLeft(line))
                return null;
            if (line is not WOldNumberedParagraph np)
                return null;
            if (!Prov1.IsValidNumber(np.Number.Text))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            if (i == Document.Body.Count)
                return new Prov1Leaf { Number = num, Contents = intro };

            List<IDivision> children = [];

            FixFirstSubsection(intro, children, heading);

            if (children.Count == 0)
                AddFollowingToIntroOrWrapUp(heading ?? line, intro);

            while (i < Document.Body.Count)
            {
                if (!CurrentIsPossibleProv1Child(line))
                    break;
                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!Prov1.IsValidChild(next))
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
                return new Prov1Leaf { Number = num, Contents = intro };

            List<IBlock> wrapUp = [];
            AddFollowingToIntroOrWrapUp(heading ?? line, wrapUp);
            return new Prov1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };

        }

        private bool CurrentIsPossibleProv1Child(WLine leader)
        {
            if (Current() is not WLine line)
                return true;
            if (!IsLeftAligned(line))
                return false;
            if (LineIsIndentedLessThan(line, leader))
                return false;
            return true;
        }

        private void FixFirstSubsection(List<IBlock> intro, List<IDivision> children, WLine heading = null)
        {
            if (intro.Last() is not WLine last || last is WOldNumberedParagraph)
                return;

            (WText num1, WLine rest1) = FixFirstProv2Num(last);
            if (num1 is null)
                return;

            List<IDivision> grandchildren = ParseProv2Children(last);
            Prov2 l;
            if (grandchildren.Count == 0)
            {
                List<IBlock> contents = [rest1];
                AddFollowingToContent(heading ?? last, contents);
                l = new Prov2Leaf { Number = num1, Contents = contents };
            }
            else
            {
                List<IBlock> wrapUp = [];
                AddFollowingToIntroOrWrapUp(heading ?? last, wrapUp);
                l = new Prov2Branch { Number = num1, Intro = [rest1], Children = grandchildren, WrapUp = wrapUp };
            }
            intro.RemoveAt(intro.Count - 1);
            children.Insert(0, l);
        }

        private (WText, WLine) FixFirstProv2Num(WLine line)
        {
            WText num = null;
            WLine rest = null;
            if (line.Contents.FirstOrDefault() is WText t && t.Text.StartsWith("—(1) "))
            {
                num = new("(1)", t.properties);
                WText x = new(t.Text[5..], t.properties);
                rest = WLine.Make(line, line.Contents.Skip(1).Prepend(x));
            }
            else if (line.Contents.FirstOrDefault() is WText t1 && line.Contents.Skip(1).FirstOrDefault() is WText t2)
            {
                string combined = t1.Text + t2.Text;
                if (!combined.StartsWith("—(1) "))
                    return (null, null);
                num = new("(1)", t1.Text.Length > 2 ? t1.properties : t2.properties);
                WText x = new(combined[5..], t2.properties);
                rest = WLine.Make(line, line.Contents.Skip(2).Prepend(x));
            }
            return (num, rest);
        }

    }

}
