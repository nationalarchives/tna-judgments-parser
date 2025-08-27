
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
            if (line is WOldNumberedParagraph)
                return null;

            List<IDivision> children = [];

            while (i < Body.Count)
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
            // A signature must have Signature styling at the paragraph or text level
            // In SIs, this right-positioning is achieved with Tab Stops, as opposed to Alignment.
            List<IBlock> contents = [];

            while (i < Body.Count)
            {
                if (BreakFromProv1())
                    break;
                if (Current() is not WLine currentLine)
                    break;
                // Signature lines are not numbered
                if (currentLine is WOldNumberedParagraph)
                    break;
                if (!currentLine.Contents.Any())
                    break;
                // The line needs to have at least one content of the type WText
                if (currentLine.Contents.OfType<WText>().First() is not WText lineText)
                    break;
                string styleName = lineText.Style;
                // The line needs to have Signature style formatting at the parargraph or text level
                if (!(StartsWithSig(currentLine.Style) || (styleName is not null && StartsWithSig(styleName))))
                    break;
                // If the current line has styling Sig_Signatory or Sig_Signee
                // and any content has styling that isn't Sig_Signatory or Sig_Signee, it must be a new signature block
                if (contents.Count > 0 && ContainsNonSigneeOrSignatory(contents)
                    && styleName is not null && StartsWithSig(styleName) && (EndsWith(styleName, "Signatory") || EndsWith(styleName, "Signee")))
                {
                    // Reached start of new signature block
                    break;
                }

                // Add each line of the signature to the contents.
                // A single line will often have both left and right-positioned text,
                // in which case we must split the line in two.
                List<IInline> inlines = [];
                foreach (IInline inline in currentLine.Contents)
                {
                    if (inline is not WTab)
                    {
                        inlines.Add(inline);
                        continue;
                    }
                    AddLineToContents(contents, new WLine(currentLine, inlines));
                    inlines.Clear();
                }
                AddLineToContents(contents, new WLine(currentLine, inlines));

                i += 1;
            }

            if (contents.Count == 0)
                return null;
            else
                return new SignatureBlock { Contents = contents };
        }

        /// <summary>
        /// Returns true if an item in the provided list has styling that isn't null, Sig_signatory or Sig_signee
        /// Adds the given line to the list of contents.
        /// If the line resembles a Signature Name, it will be identified as such.
        /// </summary>
        private static bool ContainsNonSigneeOrSignatory(List<IBlock> contents)
        {
            for (int i = 0; i < contents.Count; i++)
            {
                // Content could either be a WSignatureBlock or WLine
                WSignatureBlock sigBlock = contents[i] as WSignatureBlock;
                WLine sigLine = contents[i] as WLine;
                if (sigBlock?.Content.First() is not WText sigContent)
                    sigContent = sigLine.Contents.First() as WText;

                string sigStyle = sigContent?.Style;
                // Only signature styles Sig_signatory and Sig_signee contain "sign"
                if (sigStyle is not null && !sigStyle.Contains("sign", StringComparison.InvariantCultureIgnoreCase))
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
            string paraStyleName = line.Style; // paragraph level formatting
            string styleName = lineText.Style; // text/character level formatting
            string name = null;
            // Using StartsWith and EndsWith because sometimes the casing can be different and there might be underscores in between
            // So this handles styles like "Sig_Signee" and "sigsignee"
            if (paraStyleName is not null && StartsWithSig(paraStyleName) && EndsWith(paraStyleName, "Block") && line.IsAllItalicized())
                name = "signature";     // Sig_signee
            else if (styleName is not null && StartsWithSig(styleName))
            {
                if (EndsWith(styleName, "Signee"))
                    name = "signature";     // Sig_signee
                else if (EndsWith(styleName, "Title"))
                    name = "role";          // Sig_title
                else if (EndsWith(styleName, "Add"))
                    name = "location";      // Sig_add
                else if (EndsWith(styleName, "Date"))
                    name = "date";          // Sig_date
            }
            else if (styleName is not null && StartsWith(styleName, "Leg")
                && EndsWith(styleName, "Seal"))
                name = "seal";              // LegSeal

            if (name is not null)
                contents.Add(new WSignatureBlock() { Name = name, Content = line.Contents });
            else
                // Style not recognised so it will be added as //p
                contents.Add(line);
        }

        /// <summary>
        /// Checks if the given string begins with "Sig", case insentitive
        /// </summary>
        private static bool StartsWithSig(String str)
        {
            return str.StartsWith("Sig", StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Checks if the given string starts with the provided value, case insentitive
        /// </summary>
        private static bool StartsWith(String str, String value)
        {
            return str.StartsWith(value, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Checks if the given string ends with the provided value, case insentitive
        /// </summary>
        private static bool EndsWith(String str, String value)
        {
            return str.EndsWith(value, StringComparison.InvariantCultureIgnoreCase);
        }
    }

    internal interface ISignatures
    {

        public static bool IsValidChild(IDivision child)
        {
            if (child is SignatureBlock)
                return true;
            return false;
        }

    }

    internal class SignaturesBranch : Branch, ISignatures
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
