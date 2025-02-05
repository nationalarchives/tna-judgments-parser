
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseProv1(WLine line)
        {
            if (!IsFlushLeft(line))
                return null;

            IFormattedText num;
            ILine heading;
            if (line is WOldNumberedParagraph np)
            {
                if (!Prov1.IsSectionNumber(np.Number.Text))
                    return null;
                num = np.Number;
                heading = WLine.RemoveNumber(np);
            }
            else
            {
                num = null;
                heading = line;
            }
            i += 1;

            if (i == Document.Body.Count)
                return new Prov1Leaf { Number = num, Contents = [heading] };

            if (line is not WOldNumberedParagraph)
            {
                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (next is not Prov1)
                {
                    i = save;
                    return new Prov1Leaf { Number = num, Contents = [heading] };
                }
                if (next is Prov1Branch branch)
                {
                    if (branch.Number is null || branch.Heading is not null)
                    {
                        i = save;
                        return new Prov1Leaf { Number = num, Contents = [heading] };
                    }
                    else
                    {
                        branch.Heading = heading;
                        return branch;
                    }
                }
                if (next is Prov1Leaf leaf)
                {
                    if (leaf.Number is null || leaf.Heading is not null)
                    {
                        i = save;
                        return new Prov1Leaf { Number = num, Contents = [heading] };
                    }
                    else
                    {
                        leaf.Heading = heading;
                        return leaf;
                    }
                }
                throw new System.Exception();
            }

            // look for children
            List<IBlock> intro = [ heading ];
            heading = null;
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

            if (children.Count == 0) {
                return new Prov1Leaf { Number = num, Contents = intro };
            } else {
                return new Prov1Branch { Number = num, Intro = intro, Children = children };
            }

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
            }
            if (last.Contents.FirstOrDefault() is WText t1 && last.Contents.Skip(1).FirstOrDefault() is WText t2) {
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
