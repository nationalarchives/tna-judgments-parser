
#nullable enable

using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer? ParseProv2(WLine line)
        {
            WLine? heading = ConsumeProv2Heading(line);

            IBlock current = heading is null ? line : Body[i];
            if (current is not WOldNumberedParagraph np)
                return null;
            string numText = IgnoreQuotedStructureStart(np.Number.Text, quoteDepth);
            if (!Prov2.IsValidNumber(numText))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = HandleParagraphs(np);

            Prov2Name tagName = GetProv2Name();

            if (IsEndOfQuotedStructure(intro))
                return new Prov2Leaf { TagName = tagName, Number = num, Contents = intro };

            List<IBlock> wrapUp = [];
            List<IDivision> children = ParseProv2Children(line, intro, wrapUp);

            if (children.Count == 0)
            {
                return new Prov2Leaf { TagName = tagName, Number = num, Contents = intro };
            }
            return new Prov2Branch { TagName = tagName, Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

        /// <summary>
        /// Attempts to consume an italicised unnumbered Prov2 heading at the current position.
        /// </summary>
        /// <remarks>
        /// If these conditions are met, the parser advances the cursor and returns the heading line.
        /// Otherwise, no state is changed and null is returned.
        /// </remarks>
        private WLine? ConsumeProv2Heading(WLine line)
        {
            if (line is WOldNumberedParagraph)
                return null;
            if (!line.IsAllItalicized())
                return null;
            if (IsAtEnd())
                return null;

            i += 1;
            return line;
        }

        internal List<IDivision> ParseProv2Children(WLine leader, List<IBlock> intro, List<IBlock> wrapUp)
        {
            List<IDivision> children = [];
            int finalChildStart = i;
            while (i < Body.Count)
            {
                if (BreakFromProv1())
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!Prov2.IsValidChild(next))
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
            return children;
        }

        private Prov2Name GetProv2Name()
        {
            return frames.IsSecondaryDocName() ? Prov2Name.paragraph : Prov2Name.subsection;
        }

    }

}
