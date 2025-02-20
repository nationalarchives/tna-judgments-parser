
using System.Collections.Generic;
using System.Xml;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder : AkN.Builder
    {

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
            XmlElement level;
            switch (name)
            {
                case "crossheading":
                case "schedules":
                    level = CreateAndAppend("hcontainer", parent);
                    level.SetAttribute("name", name);
                    break;
                case "schedule":
                    level = CreateAndAppend("hcontainer", parent);
                    level.SetAttribute("name", name);
                    break;
                default:
                    level = CreateAndAppend(name, parent);
                    break;
            }
            string eId = MakeDivisionId(hc);
            if (eId is not null)
            {
                level.SetAttribute("eId", eId);
            }
            if (hc.Class is not null)
            {
                // currently "class" and "style" attributes need to be in a non-empty namespace
                level.SetAttribute("class", UKNS, hc.Class);
            }

            XmlElement number;
            if (hc.HeadingPrecedesNumber)
            {
                AddHeading(level, hc.Heading);
                number = AddNumber(level, hc.Number);
            }
            else
            {
                number = AddNumber(level, hc.Number);
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
            else if (hc is Schedule schedule)
            {
                AddReferenceNote(number, schedule.ReferenceNote);
                AddDivisions(level, schedule.Contents);
            }
        }

        private XmlElement AddNumber(XmlElement parent, IFormattedText num)
        {
            if (num is null)
                return null;
            return AddAndWrapText(parent, "num", num);
        }

        private void AddReferenceNote(XmlElement number, IFormattedText referenceNote)
        {
            if (referenceNote is null)
                return;
            XmlElement authorialNote = CreateAndAppend("authorialNote", number);
            authorialNote.SetAttribute("class", UKNS, "referenceNote");
            AddAndWrapText(authorialNote, "p", referenceNote);
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
