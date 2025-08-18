
using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Bibliography;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private BlockList ParseBlockList(WLine line)
        {
            WLine introLine = (line is WOldNumberedParagraph np) ? WLine.RemoveNumber(np) : line;
            if (introLine == null)
                return null;

            List <IBlock> children = [];
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1())
                    break;

                int save = i;
                BlockListItem next = ParseBlockListItem(Current());
                if (next == null)
                {
                    i = save;
                    break;
                }
                children.Add(next);

                if (IsEndOfQuotedStructure([next]))
                    break;
            }

            if (children.Count == 0)
                return null;
            return new BlockList { Intro = introLine, Children = children };
        }

        private BlockListItem ParseBlockListItem(IBlock block)
        {
            if (!(block is WLine line))
                return null;

            int finalChildStart = i;
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1())
                    break;
            }
            return null;
        }

    }

}
