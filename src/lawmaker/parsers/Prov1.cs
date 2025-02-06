
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
            while (i < Document.Body.Count) {
                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (next is not Prov2) {
                    i = save;
                    break;
                }
                children.Add(next);
            }

            FixFirstSubsection(intro, children);

            if (children.Count == 0)
                return new Prov1Leaf { Number = num, Contents = intro };
            else
                return new Prov1Branch { Number = num, Intro = intro, Children = children };

        }

        private void FixFirstSubsection(List<IBlock> intro, List<IDivision> children) {
            if (children.Count == 0)
                return;
            if (intro.Last() is not WLine last || last is WOldNumberedParagraph)
                return;
            if (children.First() is not Prov2 sub2 || sub2.Number?.Text != "(2)")
                return;
            if (last.Contents.FirstOrDefault() is WText t && t.Text.StartsWith("—(1) ")) {
                WText num1 = new("(1)", t.properties);
                WText x = new(t.Text[5..], t.properties);
                WLine rest1 = WLine.Make(last, last.Contents.Skip(1).Prepend(x));
                Prov2Leaf l = new() { Number = num1, Contents = [ rest1 ] };
                intro.RemoveAt(intro.Count - 1);
                children.Insert(0, l);
            } else if (last.Contents.FirstOrDefault() is WText t1 && last.Contents.Skip(1).FirstOrDefault() is WText t2) {
                string combined = t1.Text + t2.Text;
                if (combined.StartsWith("—(1) ")) {
                    WText num1 = new("(1)", t1.Text.Length > 2 ? t1.properties : t2.properties);
                    WText x = new(combined[5..], t2.properties);
                    WLine rest1 = WLine.Make(last, last.Contents.Skip(2).Prepend(x));
                    Prov2Leaf l = new() { Number = num1, Contents = [ rest1 ] };
                    intro.RemoveAt(intro.Count - 1);
                    children.Insert(0, l);
                }
            }
        }

    }

}
