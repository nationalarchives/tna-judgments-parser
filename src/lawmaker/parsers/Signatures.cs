
using System;
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
            // bool foundRightLinesStart = false;
            // bool foundRightLinesEnd = false;

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
                    
                if (!currentLine.Contents.Any())
                    break;
                if (currentLine.Contents.OfType<WText>().First() is not WText lineText)
                    break;
                string styleName = lineText.Style;
                if (contents.Count > 0 && ContainsNonSigneeOrSignatory(contents)
                    && styleName is not null
                    && styleName.StartsWith("Sig", StringComparison.InvariantCultureIgnoreCase)
                    && styleName.EndsWith("Signatory", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Reached start of new signature block
                    break;
                }
                int save = i;

                // Ensure we encounter a single contiguous block of right-positioned text.
                // if (ContentHasTabbedText(currentLine))
                // {
                //     if (!foundRightLinesStart)
                //         foundRightLinesStart = true;
                //     else if (foundRightLinesEnd)
                //     {
                //         i = save;
                //         break;
                //     }
                // }
                // else if (foundRightLinesStart && !foundRightLinesEnd)
                //     foundRightLinesEnd = true;


                // Add each line of the signature to the contents.
                // A single line will often have both left and right-positioned text,
                // in which case we must split the line in two.
                List<IInline> inlines = [];
                // if (currentLine.Contents.Any(inline => inline is WTab))
                // {
                    // bool isAfterTab = false;
                    foreach (IInline inline in currentLine.Contents)
                    {
                        if (!(inline is WTab))
                        {
                            inlines.Add(inline);
                            continue;
                        }
                        // Encountered a tab
                        // isAfterTab = true;
                        AddLineToContents(contents, new WLine(currentLine, inlines));
                        inlines.Clear();
                    }
                    AddLineToContents(contents, new WLine(currentLine, inlines));
                // }
                // else
                //     AddLineToContents(contents, new WLine(currentLine, inlines), false);

                i += 1;
            }

            if (contents.Count == 0)
                return null;
            else
                return new SignatureBlock { Contents = contents };
        }

        private static bool ContainsNonSigneeOrSignatory(List<IBlock> contents)
        {
            for (int i = 0; i < contents.Count; i++)
            {
                WSignatureBlock sigBlock = contents[i] as WSignatureBlock;
                WText sigContent =  sigBlock?.Content.First() as WText;
                // Only signature styles Sig_signatory and Sig_signee contain "sign"
                if (sigContent is not null && !sigContent.Style.Contains("sign", StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Adds the given line to the list of contents. 
        /// Depending on the style, it will add a new WSignatureBlock or WLine
        /// </summary>
        private static void AddLineToContents(List<IBlock> contents, WLine line)
        {
            if (!line.Contents.Any())
                return;
            if (line.Contents.First() is not WText lineText)
                return;
            string styleName = lineText.Style;
            string name = null;
            // Using StartsWith and EndsWith because sometimes the casing can be different and there might be underscores in between
            // So this handles styles like "Sig_Signee" and "sigsignee"
            if (styleName is not null && styleName.StartsWith("Sig", StringComparison.InvariantCultureIgnoreCase))
            {
                if (styleName.EndsWith("Signee", StringComparison.InvariantCultureIgnoreCase)
                    || (styleName.EndsWith("Block", StringComparison.InvariantCultureIgnoreCase) && line.IsAllItalicized()))
                    name = "signature";     // Sig_signee
                else if (styleName.EndsWith("Title", StringComparison.InvariantCultureIgnoreCase))
                    name = "role";          // Sig_title
                else if (styleName.EndsWith("Add", StringComparison.InvariantCultureIgnoreCase))
                    name = "location";      // Sig_add
                else if (styleName.EndsWith("Date", StringComparison.InvariantCultureIgnoreCase))
                    name = "date";          // Sig_date
            }
            else if (styleName is not null && styleName.StartsWith("Leg", StringComparison.InvariantCultureIgnoreCase)
                && styleName.EndsWith("Seal", StringComparison.InvariantCultureIgnoreCase))
                name = "seal";              // LegSeal
            
            if (name is not null)
                contents.Add(new WSignatureBlock() { Name = name, Content = line.Contents });
            else
                // Style not recognised so it will be added a //p
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
