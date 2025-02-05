
using System.Collections.Generic;
using System.Xml;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder : AkN.Builder
    {

        protected override string MakeDivisionId(IDivision div)
        {
            return null;
        }

        protected override void AddDivision(XmlElement parent, IDivision div)
        {
            if (div is HContainer hc)
            {
                AddHContainer(parent, hc);
            }
            else
            {
                base.AddDivision(parent, div);
            }
        }

        protected void AddHContainer(XmlElement parent, HContainer hc)
        {
            string name = hc.Name ?? "level";
            XmlElement level = CreateAndAppend(name, parent);
            string eId = MakeDivisionId(hc);
            if (eId is not null)
            {
                level.SetAttribute("eId", eId);
            }
            if (hc.Class is not null)
            {
                // this is a bit of a hack, because the system already uses the "class" attribute for Word style
                // the Simplifer class corrects this, setting the "class" attribute and removing the "title"
                level.SetAttribute("title", hc.Class);
            }

            if (hc.HeadingPrecedesNumber)
            {
                AddHeading(level, hc.Heading);
                AddNumber(level, hc.Number);
            }
            else
            {
                AddNumber(level, hc.Number);
                AddHeading(level, hc.Heading);
            }

            if (hc is IBranch branch)
            {
                AddIntro(level, branch);
                AddDivisions(level, branch.Children);
                AddWrapUp(level, branch);
            }
            else if (hc is ILeaf leaf)
            {
                AddContent(level, leaf.Contents);
            }
        }

        private void AddNumber(XmlElement parent, IFormattedText num)
        {
            if (num is null)
                return;
            AddAndWrapText(parent, "num", num);
        }

        private void AddHeading(XmlElement parent, ILine heading)
        {
            if (heading is null)
                return;
            Block(parent, heading, "heading");
        }

        private void AddContent(XmlElement parent, IEnumerable<IBlock> blocks)
        {
            XmlElement content = CreateAndAppend("content", parent);
            AddBlocks(content, blocks);
        }

    }

}
