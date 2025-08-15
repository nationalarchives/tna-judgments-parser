
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer ParseSignatures(WLine line)
        {
            if (!frames.IsSecondaryDocName())
                return null;
            // Signatures shouldn't have any nums
            if (line is WOldNumberedParagraph np)
                return null;

            List<IDivision> children = [];

            while (i < Document.Body.Count)
            {
                if (BreakFromProv1())
                    break;

                int save = i;
                if (Current() is not WLine currentLine)
                    return null;

                IDivision next = ParseSignature(currentLine);
                if (next == null)
                {
                    i = save;
                    break;
                }
                children.Add(next);
            }

            if (children.Count == 0)
                return null;
            else
                return new SignaturesBranch { Children = children };
        }

        private HContainer ParseSignature(WLine line)
        {
            // A signature must contain a single concurrent set of right-positioned paragraphs.
            // It may optionally begin and/or end with left-positioned paragraphs.
            // In SIs, this right-positioning is achieved with Tab Stops, as opposed to Alignment.
            bool foundRightLinesStart = false;
            bool foundRightLinesEnd = false;

            List<IBlock> contents = [];

            while (i < Document.Body.Count)
            {
                if (BreakFromProv1())
                    break;
                if (Current() is not WLine currentLine)
                    break;
                // Signature lines are not numbered
                if (currentLine is WOldNumberedParagraph np)
                    break;

                int save = i;

                // Ensure we encounter a single contiguous block of right-positioned text.
                if (ContentHasTabbedText(currentLine))
                {
                    if (!foundRightLinesStart)
                        foundRightLinesStart = true;
                    else if (foundRightLinesEnd)
                    {
                        i = save;
                        break;
                    }
                }
                else if (foundRightLinesStart && !foundRightLinesEnd)
                    foundRightLinesEnd = true;


                // Add each line of the signature to the contents.
                // A single line will often have both left and right-positioned text,
                // in which case we must split the line in two.
                List<IInline> inlines = [];
                if (currentLine.Contents.Any(inline => inline is WTab))
                {
                    bool isAfterTab = false;
                    foreach (IInline inline in currentLine.Contents)
                    {
                        if (!(inline is WTab))
                        {
                            inlines.Add(inline);
                            continue;
                        }
                        // Encountered a tab
                        isAfterTab = true;
                        AddLineToContents(contents, new WLine(currentLine, inlines), isAfterTab);
                        inlines.Clear();
                    }
                    AddLineToContents(contents, new WLine(currentLine, inlines), isAfterTab);
                }
                else
                    contents.Add(currentLine);

                i += 1;
            }

            if (contents.Count == 0 || !foundRightLinesStart)
                return null;
            else
                return new SignatureBlock { Contents = contents };
        }

        /// <summary>
        /// Adds the given line to the list of contents. 
        /// If the line resembles a Signature Name, it will be identified as such. 
        /// </summary>
        private static void AddLineToContents(List<IBlock> contents, WLine line, bool isAfterTab)
        {
            if (line.Contents.Count() == 0)
                return;
            if (isAfterTab && line.IsAllItalicized())
                contents.Add(new WSignatureName() { Content = line.Contents });
            else
                contents.Add(line);
        }

    }

    internal interface Signatures
    {

        public static bool IsValidChild(IDivision child)
        {
            if (child is SignatureBlock)
                return true;
            return false;
        }

    }

    internal class SignaturesBranch : Branch, Signatures
    {
        public override string Name { get; internal init; } = "signatures";

        public override string Class => null;

    }

    internal class SignatureBlock : Leaf
    {
        public override string Name { get; internal init; } = "signatureBlock";

        public override string Class => null;

    }

}
