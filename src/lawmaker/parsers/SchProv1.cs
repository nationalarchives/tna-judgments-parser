
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseSchProv1(WLine line)
        {
            bool quoted = quoteDepth > 0;
            if (!IsFlushLeft(line) && !quoted)
                return null;
            if (line is not WOldNumberedParagraph np)
                return null;
            string numText = IgnoreStartQuote(np.Number.Text, quoteDepth);
            if (!SchProv1.IsValidNumber(numText))
                return null;

            int save = i;
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
            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            i += 1;
            if (i == Document.Body.Count)
                return new SchProv1Leaf { Number = num, Contents = intro };

            HandleExtraParagraphs(np, intro);
            HandleQuotedStructures(intro);

            List<IDivision> children = [];
            List<IBlock> wrapUp = [];

            bool isEndOfQuotedStructure = FixFirstSchProv2(intro, children);
            if (isEndOfQuotedStructure)
            {
                if (children.Count == 0)
                    return new SchProv1Leaf { Number = num, Contents = intro };

                return new SchProv1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
            }

            int finalChildStart = i;
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1(line))
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

            if (children.Count == 0)
                return new SchProv1Leaf { Number = num, Contents = intro };
            
            return new SchProv1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

        private bool FixFirstSchProv2(List<IBlock> intro, List<IDivision> children, WLine heading = null)
        {
            if (intro.First() is not WLine first || first is WOldNumberedParagraph)
                return false;

            (WText schProv2Num, WLine schProv2FirstLine) = FixFirstProv2Num(first);
            if (schProv2Num is null)
                return false;

            intro.Remove(first);
            intro.Insert(0, schProv2FirstLine);


            SchProv2 schProv2;
            bool isEndOfQuotedStructure = IsEndOfQuotedStructure(intro);
            if (isEndOfQuotedStructure)
            {
                List<IBlock> contents = new(intro);
                schProv2 = new SchProv2Leaf { Number = schProv2Num, Contents = contents };
            }
            else
            {
                List<IBlock> schProv2WrapUp = [];
                List<IDivision> schProv2Children = ParseSchProv2Children(first, intro, schProv2WrapUp);

                List<IBlock> contents = new(intro);
                if (schProv2Children.Count == 0)
                    schProv2 = new SchProv2Leaf { Number = schProv2Num, Contents = contents };
                else
                    schProv2 = new SchProv2Branch { Number = schProv2Num, Intro = contents, Children = schProv2Children, WrapUp = schProv2WrapUp };

            }
            intro.Clear();
            children.Insert(0, schProv2);
            return isEndOfQuotedStructure;
        }
    }

}