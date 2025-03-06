
using System;
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

        private bool PeekProv1(WLine line, string startQuote = null)
        {
            bool quoted = quoteDepth > 0;
            if (line is WOldNumberedParagraph)
                return false;  // could ParseBaseProv1(np);
            if (!IsFlushLeft(line) && !quoted)
                return false;
            if (i > Document.Body.Count - 2)
                return false;
            if (Document.Body[i + 1].Block is not WLine nextLine)
                return false;
            return PeekBareProv1(nextLine, startQuote);
        }

        private bool PeekBareProv1(WLine line, string startQuote = null)
        {
            bool quoted = quoteDepth > 0;
            if (!IsFlushLeft(line) && !quoted)
                return false;
            if (line is not WOldNumberedParagraph np)
                return false;
            string numText = (startQuote == null) ? np.Number.Text : np.Number.Text[1..];
            if (!Prov1.IsValidNumber(numText))
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

            HandleExtraParagraphs(np, intro);
            HandleQuotedStructures(intro);

            List<IDivision> children = [];
            List<IBlock> wrapUp = [];

            bool isEndOfQuotedStructure = FixFirstSubsection(intro, children, heading);
            if (isEndOfQuotedStructure)
            {
                if (children.Count == 0)
                    return new Prov1Leaf { Number = num, Contents = intro };

                return new Prov1Leaf { Number = num, Contents = intro };
            }

            int finalChildStart = i;
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1(np))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!Prov1.IsValidChild(next))
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
                return new Prov1Leaf { Number = num, Contents = intro };

            return new Prov1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

        private bool FixFirstSubsection(List<IBlock> intro, List<IDivision> children, WLine heading = null)
        {
            if (intro.First() is not WLine first || first is WOldNumberedParagraph)
                return false;

            (WText prov2Num, WLine prov2FirstLine) = FixFirstProv2Num(first);
            if (prov2Num is null)
                return false;

            intro.Remove(first);
            intro.Insert(0, prov2FirstLine);

            Prov2 prov2;
            bool isEndOfQuotedStructure = IsEndOfQuotedStructure(intro);
            if (isEndOfQuotedStructure)
            {
                List<IBlock> contents = new(intro);
                prov2 = new Prov2Leaf { Number = prov2Num, Contents = contents };
            }
            else
            {
                List<IBlock> prov2WrapUp = [];
                List<IDivision> prov2Children = ParseProv2Children(first, intro, prov2WrapUp);

                List<IBlock> contents = new(intro);
                if (prov2Children.Count == 0)
                    prov2 = new Prov2Leaf { Number = prov2Num, Contents = contents };
                else
                    prov2 = new Prov2Branch { Number = prov2Num, Intro = contents, Children = prov2Children, WrapUp = prov2WrapUp };
            }
            intro.Clear();
            children.Insert(0, prov2);
            return isEndOfQuotedStructure;
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
