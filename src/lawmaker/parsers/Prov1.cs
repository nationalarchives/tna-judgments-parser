
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
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
            bool quoted = quoteDepth > 0;
            if (line is WOldNumberedParagraph)
                return false;  // could ParseBaseProv1(np);
            if (!IsFlushLeft(line) && !quoted)
                return false;
            if (i > Document.Body.Count - 2)
                return false;
            if (Document.Body[i + 1].Block is not WLine nextLine)
                return false;
            // The heading and first line should have the same indentation
            if (LineIsIndentedMoreThan(line, nextLine, 0.2f))
                return false;
            return PeekBareProv1(nextLine);
        }

        private bool PeekBareProv1(WLine line)
        {
            bool quoted = quoteDepth > 0;
            if (!IsFlushLeft(line) && !quoted)
                return false;
            if (line is not WOldNumberedParagraph np)
                return false;
            string numText = IgnoreQuotedStructureStart(np.Number.Text, quoteDepth);
            if (!Prov1.IsValidNumber(numText))
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
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1())
                    break;

                int save = i;
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
                children.Add(next);
                finalChildStart = save;

                if (IsEndOfQuotedStructure(next))
                    break;
            }
            wrapUp.AddRange(HandleWrapUp(children, finalChildStart));

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
            if (line.Contents.FirstOrDefault() is WText t && Prov2.IsFirstProv2Start(t.Text.TrimStart()))
            {
                string text = t.Text.TrimStart(); // Sometimes there's a leading space
                WText num = new("(1)", t.properties);
                WText remainder = new(text.TrimStart()[5..], t.properties);
                return new(num, line.Contents.Skip(1).Prepend(remainder), line);
            }
            else if (line.Contents.FirstOrDefault() is WText t1 && line.Contents.Skip(1).FirstOrDefault() is WText t2)
            {
                string combined = t1.Text + t2.Text;
                if (!Prov2.IsFirstProv2Start(combined.TrimStart()))
                    return null;

                WText num = new("(1)", t1.Text.Length > 2 ? t1.properties : t2.properties);
                WText remainder = new(combined[5..], t2.properties);
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
                Context.RULES => Prov1Name.rule,
                Context.ARTICLES => Prov1Name.article,
                _ => Prov1Name.regulation
            };

        }

    }

}
