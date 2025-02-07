
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        // matches only a heading above numbered section
        private HContainer ParseProv1(WLine line)
        {
            if (line is WOldNumberedParagraph)
                return null;  // could return ParseBaseProv1(np);
            if (!IsFlushLeft(line))
                return null;
            if (i == Document.Body.Count)
                return null;
            if (Document.Body[i+1].Block is not WOldNumberedParagraph np)
                return null;

            int save = i;
            i += 1;
            HContainer next = ParseBareProv1(np);
            if (next is null) {
                i = save;
                return null;
            }

            next.Heading = line;
            return next;
        }

        // matches only a numbered section without a heading
        private HContainer ParseBareProv1(WLine line)
        {
            if (!IsFlushLeft(line))
                return null;
            if (line is not WOldNumberedParagraph np)
                return null;
            if (!Prov1.IsSectionNumber(np.Number.Text))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = [ WLine.RemoveNumber(np) ];

            if (i == Document.Body.Count)
                return new Prov1Leaf { Number = num, Contents = intro };

            List<IDivision> children = [];

            FixFirstSubsection(intro, children);

            while (i < Document.Body.Count) {
                if (!CurrentIsPossibleProv1Child(line))
                    break;
                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (next is not Prov2 && next is not Para1) {
                    i = save;
                    break;
                }
                if (!NextChildIsAcceptable(children, next)) {
                    i = save;
                    break;
                }
                children.Add(next);
            }

            if (children.Count == 0)
                return new Prov1Leaf { Number = num, Contents = intro };
            else
                return new Prov1Branch { Number = num, Intro = intro, Children = children };

        }

        private bool CurrentIsPossibleProv1Child(WLine leader) {
            if (Current() is not WLine line)
                return true;
            if (!IsLeftAligned(line))
                return false;
            if (LineIsIndentedLessThan(line, leader))
                return false;
            return true;
        }

        private void FixFirstSubsection(List<IBlock> intro, List<IDivision> children) {
            if (intro.Last() is not WLine last || last is WOldNumberedParagraph)
                return;
            WText num1;
            WLine rest1;
            if (last.Contents.FirstOrDefault() is WText t && t.Text.StartsWith("—(1) ")) {
                num1 = new("(1)", t.properties);
                WText x = new(t.Text[5..], t.properties);
                rest1 = WLine.Make(last, last.Contents.Skip(1).Prepend(x));
                List<IDivision> grandchildren = ParseProv2Children(last);
                Prov2 l;
                if (grandchildren.Count == 0)
                    l = new Prov2Leaf { Number = num1, Contents = [ rest1 ] };
                else
                    l = new Prov2Branch { Number = num1, Intro = [ rest1 ], Children = grandchildren};
                intro.RemoveAt(intro.Count - 1);
                children.Insert(0, l);
            } else if (last.Contents.FirstOrDefault() is WText t1 && last.Contents.Skip(1).FirstOrDefault() is WText t2) {
                string combined = t1.Text + t2.Text;
                if (combined.StartsWith("—(1) ")) {
                    num1 = new("(1)", t1.Text.Length > 2 ? t1.properties : t2.properties);
                    WText x = new(combined[5..], t2.properties);
                    rest1 = WLine.Make(last, last.Contents.Skip(2).Prepend(x));
                    List<IDivision> grandchildren = ParseProv2Children(last);
                    Prov2 l;
                    if (grandchildren.Count == 0)
                        l = new Prov2Leaf { Number = num1, Contents = [ rest1 ] };
                    else
                        l = new Prov2Branch { Number = num1, Intro = [ rest1 ], Children = grandchildren};
                    intro.RemoveAt(intro.Count - 1);
                    children.Insert(0, l);
                }
            }
        }

    }

}
