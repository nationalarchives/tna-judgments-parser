
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
            if (!PeekProv1(line))
                return null;

            int save = i;
            i += 1;
            WOldNumberedParagraph np = Current() as WOldNumberedParagraph;
            HContainer next = ParseBareProv1(np, line);
            if (next is null)
            {
                i = save;
                return null;
            }

            next.Heading = line;
            return next;
        }

        private bool PeekProv1(WLine line)
        {
            if (line is WOldNumberedParagraph)
                return false;  // could ParseBaseProv1(np);
            if (!IsFlushLeft(line))
                return false;
            if (i > Document.Body.Count - 2)
                return false;
            if (Document.Body[i + 1].Block is not WLine nextLine)
                return false;
            return PeekBareProv1(nextLine);
        }

        private bool PeekBareProv1(WLine line)
        {
            if (!IsFlushLeft(line))
                return false;
            if (line is not WOldNumberedParagraph np)
                return false;
            if (!Prov1.IsValidNumber(np.Number.Text))
                return false;
            return true;
        }

        // matches only a numbered section without a heading
        private HContainer ParseBareProv1(WOldNumberedParagraph np, WLine heading = null)
        {
            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            i += 1;

            if (i == Document.Body.Count)
                return new Prov1Leaf { Number = num, Contents = intro };

            List<IDivision> children = [];

            FixFirstSubsection(intro, children, heading);

            int finalChildStartLine = i;
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1())
                    break;
                int save = i;
                IBlock childStartLine = Current();
                IDivision next = ParseNextBodyDivision();
                if (next is UnknownLevel || IsExtraIntroLine(next, childStartLine, np, children.Count))
                {
                    intro.Add(childStartLine);
                    continue;
                }
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
                finalChildStartLine = save;
            }
            List<IBlock> wrapUp = HandleWrapUp(children, finalChildStartLine);

            if (children.Count == 0)
                return new Prov1Leaf { Number = num, Contents = intro };

            return new Prov1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

        private void FixFirstSubsection(List<IBlock> intro, List<IDivision> children, WLine heading = null)
        {
            if (intro.Last() is not WLine last || last is WOldNumberedParagraph)
                return;

            (WText num1, WLine rest1) = FixFirstProv2Num(last);
            if (num1 is null)
                return;

            intro.Remove(last);
            intro.Add(rest1);

            List<IBlock> prov2WrapUp = [];
            List<IDivision> prov2Children = ParseProv2Children(last, intro, prov2WrapUp);

            Prov2 l;
            if (prov2Children.Count == 0)
            {
                List<IBlock> contents = new(intro);
                AddFollowingToContent(heading ?? last, contents);
                l = new Prov2Leaf { Number = num1, Contents = contents };
            }
            else
            {
                List<IBlock> contents = new(intro);
                l = new Prov2Branch { Number = num1, Intro = contents, Children = prov2Children, WrapUp = prov2WrapUp };
            }
            intro.Clear();
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
