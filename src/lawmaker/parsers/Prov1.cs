
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Vml;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer ParseProv1(WLine line)
        {
            int save = i;
            ILine heading = null;

            // A Prov1 element may or may not have a heading
            // If it does, we cache and skip over the heading for now
            if (PeekProv1(line))
            {
                heading = line;
                i += 1;
            }
            // A heading-less Prov1 must numbered in sequence
            else if (!PeekBareProv1(line) || !IsNextProv1InSequence(line))
            {
                return null;
            }

            WOldNumberedParagraph np = Current() as WOldNumberedParagraph;
            HContainer next = ParseBareProv1(np, line);
            if (next is null)
            {
                i = save;
                return null;
            }

            if (heading is not null)
                next.Heading = heading;
            return next;
        }

        private bool PeekProv1(WLine line)
        {
            bool quoted = quoteDepth > 0;
            if (line is WOldNumberedParagraph)
                return false;  // could ParseBaseProv1(np);
            if (!line.IsFlushLeft() && !quoted)
                return false;
            if (i > Body.Count - 2)
                return false;
            if (Body[i + 1] is not WLine nextLine)
                return false;
            // The heading and first line should have the same indentation
            if (LineIsIndentedMoreThan(line, nextLine, 0.2f))
                return false;
            return PeekBareProv1(nextLine);
        }

        private bool PeekBareProv1(WLine line)
        {
            bool quoted = quoteDepth > 0;
            if (!line.IsLeftAligned())
                return false;
            if (line is not WOldNumberedParagraph np)
                return false;
            if (!Prov1.IsValidNumber(GetNumString(np.Number)))
                return false;
            return true;
        }

        // matches only a numbered section without a heading
        private HContainer ParseBareProv1(WOldNumberedParagraph np, WLine heading = null)
        {
            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = [];
            List<IDivision> children = [];
            List<IBlock> wrapUp = [];

            Prov1Name tagName = GetProv1Name();

            provisionRecords.Push(typeof(Prov1), num, quoteDepth);

            WOldNumberedParagraph firstProv2Line = FixFirstProv2(np);
            bool hasProv2Child = (firstProv2Line != null);
            if (hasProv2Child)
            {
                i -= 1;
                HContainer prov2 = ParseAndMemoize(firstProv2Line, "Prov2", ParseProv2);
                if (prov2 == null)
                    return new Prov1Leaf { TagName = tagName, Number = num, Contents = intro };
                children.Add(prov2);
                if (IsEndOfQuotedStructure(prov2))
                    return new Prov1Branch { TagName = tagName, Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
            }
            else
            {
                intro = HandleParagraphs(np);
                if (IsEndOfQuotedStructure(intro))
                    return new Prov1Leaf { TagName = tagName, Number = num, Contents = intro };
            }

            int finalChildStart = i;
            while (i < Body.Count)
            {
                if (BreakFromProv1())
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

            provisionRecords.Pop();

            if (children.Count == 0)
                return new Prov1Leaf { TagName = tagName, Number = num, Contents = intro };

            return new Prov1Branch { TagName = tagName, Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
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

        private WOldNumberedParagraph FixFirstProv2(WLine line)
        {
            if (line.Contents.FirstOrDefault() is WText t && Prov2.IsFirstProv2Start(t.Text))
            {
                string text = t.Text.TrimStart(); // Sometimes there's a leading space
                WText num = new("(1)", t.properties);
                WText remainder = new(text.Substring(Math.Min(5, text.Length)), t.properties);
                return new(num, line.Contents.Skip(1).Prepend(remainder), line);
            }
            else if (line.Contents.FirstOrDefault() is WText t1 && line.Contents.Skip(1).FirstOrDefault() is WText t2)
            {
                string combined = t1.Text.TrimStart() + t2.Text;
                if (!Prov2.IsFirstProv2Start(combined))
                    return null;

                WText num = new("(1)", t1.Text.Length > 2 ? t1.properties : t2.properties);
                WText remainder = new(combined.Substring(Math.Min(5, combined.Length)), t2.properties);
                return new(num, line.Contents.Skip(2).Prepend(remainder), line);
            }
            return null;
        }

        private Prov1Name GetProv1Name()
        {
            if (!frames.IsSecondaryDocName())
                return Prov1Name.section;
            // NISI should always contain articles, regardless of what the user specifies
            if (frames.CurrentDocName == DocName.NISI)
                return Prov1Name.article;
            return frames.CurrentContext switch
            {
                Context.REGULATIONS => Prov1Name.regulation,
                Context.RULES => Prov1Name.rule,
                Context.ARTICLES => Prov1Name.article,
                _ => Prov1Name.article
            };

        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="line"/> is numbered sequentially after the previous Prov1 element.
        /// </summary>
        /// <remarks>
        /// We enforce that Prov1 elements without headings must be numbered sequentially in order to prevent
        /// numbered list items and other similarly formatted provisions from being mischaracterised.
        /// </remarks>
        private bool IsNextProv1InSequence(WLine line)
        {
            // If there is no existing sequence (i.e. no first num) just return true.
            IFormattedText currentNumber = provisionRecords.CurrentNumber(quoteDepth);
            if (currentNumber is null)
                return true;

            string firstNum = GetNumString(currentNumber);
            string secondNum = GetNumString(line);
            return IsSubsequentNum(firstNum, secondNum);
        }

    }

}
