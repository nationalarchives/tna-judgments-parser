
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

            if (frames.CurrentDocName.RequireNumberedProv1Heading())
            {
                // Heading line must begin with a valid Prov1 number 
                if (headingLine is not WOldNumberedParagraph np)
                {
                    // If the line is not a WOldNumberedParagraph check if it is all bold and the text before the first space is a valid prov1 number
                    // This check is required because the logic for parsing a line as a WOldNumberedParagraph doesn't work for numbered lines which only contain space(s) between the num and heading
                    string numString = headingLine.NormalizedContent.Split(' ', 2).First();
                    string numCleaned = IgnoreQuotedStructureStart(numString, quoteDepth);
                    if (headingLine.IsAllBold() && Prov1.IsValidNumber(numCleaned, frames.CurrentDocName))
                    {
                        // Recreate the current line as a WOldNumberedParagraph
                        List<IInline> contents = headingLine.Contents.ToList();
                        WText? firstContent = contents.First() as WText;
                        if (firstContent is not null)
                        {
                            // Remove num from content of line
                            string firstContentWithoutNum = firstContent.Text.Substring(numString.Length).TrimStart();
                            WText newFirstContent = new WText(firstContentWithoutNum, firstContent.properties);
                            contents = contents.Skip(1).ToList();
                            contents.Insert(0, newFirstContent);
                            
                            // Assign the new WOldNumberedParagraph to the various variables
                            headingLine = new WOldNumberedParagraph(new WText(numString, null), new WLine(headingLine, contents));
                            np = (WOldNumberedParagraph) headingLine;
                            Body[i] = headingLine;
                        }
                        else
                            return false;
                    }
                    else
                        return false;
                }
                
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

            if (!frames.CurrentDocName.RequireNumberedProv1Heading())
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
                if (frames.CurrentDocName.RequireNumberedProv1Heading())
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
                headingLine = Current() as WLine;
                i += 1;
            }

            // Parse Prov1 contents/children
            HContainer parsedProv1 = ParseBareProv1(Current() as WLine, headingLine);
            provisionRecords.Pop();
            if (parsedProv1 is null)
            {
                i = save;
                return null;
            }
            return parsedProv1;
        }

        /// <summary>
        /// Attempts to parse a <c>Prov1</c> element without the heading.
        /// </summary>
        /// <param name="line">The line from which to begin parsing.</param>
        /// <param name="headingLine">The immediately preceding heading line, if present.</param>
        /// <returns>
        /// An <c>HContainer</c> representing the parsed <c>Prov1</c> element, if successful. 
        /// Otherwise <c>null</c>.
        /// </returns>
        private HContainer ParseBareProv1(WLine line, WLine? headingLine)
        {
            Prov1Name tagName = GetProv1Name();
            ILine? heading = headingLine;
            IFormattedText? number = null;
            List<IBlock> intro = [];
            List<IDivision> children = [];
            List<IBlock> wrapUp = [];
                
            bool headingPrecedesNumber = !frames.CurrentDocName.RequireNumberedProv1Heading();
            if (headingPrecedesNumber && headingLine is not WOldNumberedParagraph)
            {
                // Scenarios [C] through [F].
                // Must strip the Prov1 number from the beginning of Line.
                number = ((WOldNumberedParagraph)line).Number;
                WOldNumberedParagraph? fixedProv2Line = FixFirstProv2(line);
                if (fixedProv2Line is not null)
                    line = fixedProv2Line;
            }
            else
            {
                // Scenarios [A] and [B].
                // Prov1 number comes from HeadingLine.
                number =  ((WOldNumberedParagraph)headingLine!).Number;
            }
            
            provisionRecords.Push(typeof(Prov1), number, quoteDepth);

            // Attempt to parse the first child as a Prov2
            HContainer? prov2 = ParseAndMemoize(line, "Prov2", ParseProv2);
            if (prov2 != null)
            {
                children.Add(prov2);
                if (IsEndOfQuotedStructure(prov2))
                    return new Prov1Branch 
                    { 
                        TagName = tagName, 
                        Number = number,
                        Heading = heading,
                        Intro = intro, 
                        Children = children, 
                        WrapUp = wrapUp, 
                        HeadingPrecedesNumber = headingPrecedesNumber 
                    };
            }
            // If unsuccessful, parse as unstructured content
            else
            {
                i += 1;
                intro = HandleParagraphs(line);
                if (IsEndOfQuotedStructure(intro))
                    return new Prov1Leaf 
                    { 
                        TagName = tagName, 
                        Number = number, 
                        Heading = heading,
                        Contents = intro, 
                        HeadingPrecedesNumber = headingPrecedesNumber 
                    };
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
                return new Prov1Leaf 
                { 
                    TagName = tagName, 
                    Number = number, 
                    Heading = heading,
                    Contents = intro, 
                    HeadingPrecedesNumber = headingPrecedesNumber 
                };

            return new Prov1Branch 
            { 
                TagName = tagName, 
                Number = number, 
                Heading = heading,
                Intro = intro, 
                Children = children, 
                WrapUp = wrapUp, 
                HeadingPrecedesNumber = headingPrecedesNumber 
            };
        }

        /// <summary>
        /// Strips the Prov1 number from a joint Prov1-Prov2 line.
        /// </summary>
        /// <param name="line">
        /// The joint Prov1-Prov2 line.
        /// </param>
        /// <returns>
        /// A <see cref="WOldNumberedParagraph"/> representing a corrected Prov2 line,
        /// if a Prov1 number was successfully removecd. Otherwise, <c>null</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The first Prov2 under a Prov1 is special in that it begins on the same line
        /// as the Prov1 number (e.g., <c>1.-(1)</c>, where <c>1</c> is the Prov1 number and
        /// <c>(1)</c> is the first Prov2 number).
        /// </para>
        /// <para>
        /// This method strips the Prov1 number from the beginning of the joint Prov1-Prov2 line,
        /// returning the line as a standalone Prov2.
        /// </para>
        /// </remarks>
        private WOldNumberedParagraph? FixFirstProv2(WLine line)
        {
            List<IInline> inlines = [.. line.Contents.Where(inline => inline is not WTab)];
            if (inlines.FirstOrDefault() is WText t && Prov2.IsFirstProv2Start(t.Text))
            {
                // The start of the Prov2 is within a single IInline element.
                string text = t.Text.TrimStart(); // Sometimes there's a leading space
                WText num = new("(1)", t.properties);
                WText remainder = new(text.Substring(Math.Min(5, text.Length)), t.properties);
                return new(num, inlines.Skip(1).Prepend(remainder), line);
            }
            else if (inlines.FirstOrDefault() is WText t1 && inlines.Skip(1).FirstOrDefault() is WText t2)
            {
                // The start of the Prov2 is split across multiple IInline elements.
                string combined = t1.Text.TrimStart() + t2.Text;
                if (!Prov2.IsFirstProv2Start(combined))
                    return null;

                WText num = new("(1)", t1.Text.Length > 2 ? t1.properties : t2.properties);
                WText remainder = new(combined.Substring(Math.Min(5, combined.Length)), t2.properties);
                return new(num, inlines.Skip(2).Prepend(remainder), line);
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
            return IsSubsequentAlphanumeric(firstNum, secondNum);
        }

    }

}
