using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer ParseSchProv1(WLine line)
        {
            if (!PeekSchProv1(line))
                return null;

            int save = i;
            WOldNumberedParagraph np = line as WOldNumberedParagraph;
            HContainer next = Parse(line, np);
            if (next is null)
            {
                i = save;
                return null;
            }
            return next;
        }

        private HContainer Parse(WLine line, WOldNumberedParagraph np)
        {
            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = [];
            List<IDivision> children = [];
            List<IBlock> wrapUp = [];

            provisionRecords.Push(typeof(SchProv1), num, quoteDepth);

            WOldNumberedParagraph firstProv2Line = FixFirstProv2(np);
            bool hasProv2Child = (firstProv2Line != null);
            if (hasProv2Child)
            {
                i -= 1;
                HContainer schProv2 = ParseAndMemoize(firstProv2Line, "SchProv2", ParseSchProv2);
                if (schProv2 == null)
                    return new SchProv1Leaf { Number = num, Contents = intro };
                children.Add(schProv2);
                if (IsEndOfQuotedStructure(schProv2))
                    return new SchProv1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
            }
            else
            {
                intro = HandleParagraphs(np);
                if (IsEndOfQuotedStructure(intro))
                    return new SchProv1Leaf { Number = num, Contents = intro };
            }

            int finalChildStart = i;
            while (i < Body.Count)
            {
                if (BreakFromProv1())
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!SchProv1.IsValidChild(next))
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
                return new SchProv1Leaf { Number = num, Contents = intro };

            return new SchProv1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

        private bool PeekSchProv1(WLine line)
        {
            bool quoted = quoteDepth > 0;
            if (!line.IsFlushLeft() && !quoted)
                return false;
            if (line is not WOldNumberedParagraph np)
                return false;
            if (!SchProv1.IsValidNumber(GetNumString(np.Number), frames.CurrentDocName))
                return false;
            return true;
        }

    }

}
