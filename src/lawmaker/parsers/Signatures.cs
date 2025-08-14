
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
            int rightAlignedContentStart = -1;
            int rightAlignedContentEnd = -1;

            List<IBlock> contents = [];

            while (i < Document.Body.Count)
            {
                if (BreakFromProv1())
                    break;
                if (Current() is not WLine currentLine)
                    break;
                // Signature shouldn't have any nums
                if (currentLine is WOldNumberedParagraph np)
                    break;

                int save = i;

                // TODO: add comment
                if (ContentHasTabbedText(currentLine))
                {
                    // A signature must contain a single concurrent set of right-positioned paragraphs

                    if (rightAlignedContentStart < 0)
                        rightAlignedContentStart = i;
                    else if (rightAlignedContentEnd > 0)
                    {
                        i = save;
                        break;
                    }
                }
                else 
                {
                    if (rightAlignedContentStart > 0 && rightAlignedContentEnd < 0)
                        rightAlignedContentEnd = i;
                }

                List<IInline> inlines = [];
                if (currentLine.Contents.Any(inline => inline is WTab))
                {
                    bool isAfterTab = false;
                    foreach (IInline inline in currentLine.Contents)
                    {
                        if (!(inline is WTab))
                            inlines.Add(inline);
                        else if (inlines.Count > 0)
                        {
                            AddInlines(currentLine, contents, inlines, isAfterTab);
                            inlines.Clear();
                            isAfterTab = true;
                        }
                        else
                            isAfterTab = true;
                    }
                    if (inlines.Count > 0)
                        AddInlines(currentLine, contents, inlines, isAfterTab);
                }
                else
                    contents.Add(currentLine);

                i += 1;
            }

            if (contents.Count == 0)
                return null;
            else
                return new SignatureBlock { Contents = contents };
        }


        private void AddInlines(WLine line, List<IBlock> contents, List<IInline> inlines, bool isAfterTab)
        {
            WLine newLine = new WLine(line, inlines);
            if (isAfterTab && newLine.IsAllItalicized())
                contents.Add(new WSignatureName() { Content = inlines });
            else
                contents.Add(newLine);
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
