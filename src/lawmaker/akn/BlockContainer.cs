using System.Xml;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder : AkN.Builder
    {

        protected void AddBlockContainer(XmlElement parent, BlockContainer bc)
        {
            string name = bc.Name ?? "blockContainer";
            XmlElement blockContainerElement = CreateAndAppend(name, parent);

            if (bc.Class is not null)
            {
                // currently "class" and "style" attributes need to be in a non-empty namespace
                blockContainerElement.SetAttribute("class", UKNS, bc.Class);
            }

            AddHeading(blockContainerElement, bc.Heading);

            AddSubheading(blockContainerElement, bc.Subheading);

            AddBlocks(blockContainerElement, bc.Blocks);
        }
        
        private void AddSubheading(XmlElement parent, ILine subheading)
        {
            if (subheading is null)
                return;
            Block(parent, subheading, "subheading");
        }

    }

}
