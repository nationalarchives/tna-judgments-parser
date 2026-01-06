
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        /* 
         * There are 2 types of Prov1 element. 
         * 
         * Most jurisdictions (UK, SP, SC) use Prov1 elements with NUMBERED headings.
         * The heading line is REQUIRED. It begins with the Prov1 number.
         * The non-heading line is composed of an optional Prov2 number, and text content.
         * 
         * 1 Section heading                [A]
         * (1) Subsection content
         * 
         * 2 Section heading                [B]
         * Section content
         * 
         * But SI & NI use Prov1 elements with UNNUMBERED headings.
         * The heading line is actually OPTIONAL.
         * The non-heading line is composed of Prov1 number, optional Prov2 number, and text content.
         * 
         * Section heading                  [C]
         * 1.—(1) Subsection content
         * 
         * Section heading                  [D]
         * 2. Section content
         * 
         * 3.—(1) Subsection content        [E]
         * 
         * 4. Section content               [F]
         * 
         */

        /// <summary>
        /// Peeks at <paramref name="line"/> and its following sibling.
        /// Returns <c>true</c> if the two lines appear to form the start of a <c>Prov1</c> element.
        /// That is, <paramref name="line"/> is formatted like a <c>Prov1</c> heading, 
        /// and the following line is formatted like the start of a valid <c>Prov1</c> child. 
        /// </summary>
        /// <param name="line">.</param>
        /// <returns><c>true</c> if <paramref name="line"/> appears to be the start of a <c>Prov1</c>.</returns>
        private bool PeekProv1(WLine line)
        {
            // We consider the first two lines
            WLine headingLine = line;
            if (i > Body.Count - 2)
                return false;
            if (Body[i + 1] is not WLine secondLine)
                return false;

            if (!line.IsLeftAligned())
                return false;

            // Unquoted Prov1 elements should have zero indentation
            if (quoteDepth == 0 && !headingLine.IsFlushLeft())
                return false;

            if (frames.CurrentDocName.RequireNumberedProv1Headings())
            {
                // Heading line must begin with a valid Prov1 number 
                if (headingLine is not WOldNumberedParagraph np)
                    return false;
                if (!Prov1.IsValidNumber(GetNumString(np.Number), frames.CurrentDocName))
                    return false;
            }
            else
            {
                // Heading line must not be numbered
                if (headingLine is WOldNumberedParagraph)
                    return false;
            }

            // Both lines should have roughly the same indentation
            if (LineIsIndentedMoreThan(headingLine, secondLine, 0.2f))
                return false;

            // Heading OK, now peek the second line
            return PeekBareProv1(secondLine);
        }

        /// <summary>
        /// Peeks at <paramref name="line"/>.
        /// Returns <c>true</c> if it appears to be the start of a <c>Prov1</c> element, ignoring the heading.
        /// That is, if <paramref name="line"/> is formatted like the start of a valid <c>Prov1</c> child.
        /// </summary>
        /// <param name="line">.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="line"/> appears to be the start of a <c>Prov1</c> element, ignoring the heading.
        /// </returns>
        private bool PeekBareProv1(WLine line)
        {
            if (!line.IsLeftAligned())
                return false;

            if (!frames.CurrentDocName.RequireNumberedProv1Headings())
            {
                if (line is not WOldNumberedParagraph np)
                    return false;
                if (!Prov1.IsValidNumber(GetNumString(np.Number), frames.CurrentDocName))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Attempts to parse a <c>Prov1</c> element starting from the given <paramref name="line"/>.
        /// </summary>
        /// <param name="line">The line from which to begin parsing.</param>
        /// <returns>
        /// An <c>HContainer</c> representing the parsed <c>Prov1</c> element, if successful. 
        /// Otherwise <c>null</c>.
        /// </returns>
        private HContainer? ParseProv1(WLine line)
        {
            int save = i;
            WLine? headingLine;

            // Skip over and cache the heading for now (if present).
            if (!PeekProv1(line))
            {
                if (frames.CurrentDocName.RequireNumberedProv1Headings())
                    return null;
                // With UNNUMBERED Prov1 headings, it's possible to have no heading at all (see scenarios [E] & [F]).
                // So we re-peek without expecting a heading. But, we enforce that such a Prov1 must be numbered
                // sequentially from the previous Prov1.
                else if (!PeekBareProv1(line) || !IsNextProv1InSequence(line))
                    return null;
                headingLine = null;
            }
            else
            {
                headingLine = line;
                i += 1;
            }

            // Parse Prov1 contents/children
            IFormattedText? number = line is WOldNumberedParagraph np ? np.Number : null;
            HContainer parsedProv1 = ParseBareProv1(Current() as WLine, number);
            provisionRecords.Pop();
            if (parsedProv1 is null)
            {
                i = save;
                return null;
            }

            // Handle Prov1 heading
            if (headingLine is WOldNumberedParagraph numberedHeadingLine)
            {
                parsedProv1.Number = numberedHeadingLine.Number;
                parsedProv1.Heading = numberedHeadingLine;
            }
            else if (headingLine is not null)
            {
                parsedProv1.Heading = headingLine;
            }
            return parsedProv1;
        }

        /// <summary>
        /// Attempts to parse a <c>Prov1</c> element without the heading.
        /// </summary>
        /// <param name="line">The line from which to begin parsing.</param>
        /// <param name="number">An override for the <c>Prov1</c> element's number.</param>
        /// <returns>
        /// An <c>HContainer</c> representing the parsed <c>Prov1</c> element, if successful. 
        /// Otherwise <c>null</c>.
        /// </returns>
        private HContainer ParseBareProv1(WLine line, IFormattedText? number)
        {
            List<IBlock> intro = [];
            List<IDivision> children = [];
            List<IBlock> wrapUp = [];
            Prov1Name tagName = GetProv1Name();

            bool headingPrecedesNumber = !frames.CurrentDocName.RequireNumberedProv1Headings();
            if (headingPrecedesNumber)
            {
                // Must strip the Prov1 number from the beginning of the line (see scenarios [C] through [F])
                if (line is WOldNumberedParagraph np)
                    number = np.Number;
                WOldNumberedParagraph? fixedProv2Line = FixFirstProv2(line);
                if (fixedProv2Line is not null)
                    line = fixedProv2Line;
            }

            provisionRecords.Push(typeof(Prov1), number!, quoteDepth);

            // Attempt to parse the first child as a Prov2
            HContainer prov2 = ParseAndMemoize(line, "Prov2", ParseProv2);
            if (prov2 != null)
            {
                children.Add(prov2);
                if (IsEndOfQuotedStructure(prov2))
                    return new Prov1Branch { TagName = tagName, Number = number, Intro = intro, Children = children, WrapUp = wrapUp, HeadingPrecedesNumber = headingPrecedesNumber };
            }
            // If unsuccessful, parse as unstructured content
            else
            {
                i += 1;
                intro = HandleParagraphs(line);
                if (IsEndOfQuotedStructure(intro))
                    return new Prov1Leaf { TagName = tagName, Number = number, Contents = intro, HeadingPrecedesNumber = headingPrecedesNumber };
            }

            // Handle additional children
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

            if (children.Count == 0)
                return new Prov1Leaf { TagName = tagName, Number = number, Contents = intro, HeadingPrecedesNumber = headingPrecedesNumber };

            return new Prov1Branch { TagName = tagName, Number = number, Intro = intro, Children = children, WrapUp = wrapUp, HeadingPrecedesNumber = headingPrecedesNumber };
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

        // 
        private WOldNumberedParagraph? FixFirstProv2(WLine line)
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
