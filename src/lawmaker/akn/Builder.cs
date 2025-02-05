
using System.Collections.Generic;
using System.Xml;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder(Bill bill) : AkN.Builder
    {

        override protected string UKNS => "https://www.legislation.gov.uk/namespaces/UK-AKN";

        public static XmlDocument Build(Bill bill)
        {
            return new Builder(bill).Build();
        }

        private readonly Bill bill = bill;

        private XmlDocument Build()
        {
            XmlElement akomaNtoso = CreateAndAppend("akomaNtoso", doc);

            XmlElement main = CreateAndAppend("bill", akomaNtoso);
            main.SetAttribute("name", this.bill.Type);

            string title = "";
            Metadata.Add(main, title);

            AddCoverPage(main, bill.CoverPage);
            AddPreface(main, bill.Preface);
            AddPreamble(main, bill.Preamble);
            AddBody(main, bill.Body, bill.Schedules);

            return doc;
        }

        private void AddCoverPage(XmlElement bill, IList<IBlock> coverPage)
        {
            XmlElement e = CreateAndAppend("coverPage", bill);
            AddBlocks(e, coverPage);
        }

        private void AddPreface(XmlElement bill, IList<IBlock> preface)
        {
            XmlElement e = CreateAndAppend("preface", bill);
            e.SetAttribute("eId", "preface");
            AddBlocks(e, preface);
        }

        private void AddPreamble(XmlElement bill, IList<IBlock> preamble)
        {
            XmlElement e = CreateAndAppend("preamble", bill);
            e.SetAttribute("eId", "preamble");
            XmlElement formula = CreateAndAppend("formula", e);
            formula.SetAttribute("name", "enactingText");
            AddBlocks(formula, preamble);
        }

        private void AddBody(XmlElement main, IList<IDivision> divisions, IList<Schedule> schedules)
        {
            XmlElement body = CreateAndAppend("body", main);
            foreach (IDivision division in divisions)
            {
                AddDivision(body, division);
            }
            // add schedules
        }

        /* */

        private void AddBlocks(XmlElement parent, IEnumerable<IBlock> blocks)
        {
            base.blocks(parent, blocks);
        }

        protected override void AddQuotedStructure(XmlElement parent, IQuotedStructure qs)
        {
            XmlElement p = CreateAndAppend("p", parent);
            XmlElement mod = CreateAndAppend("mod", p);
            XmlElement e = CreateAndAppend("quotedStructure", mod);
            if (qs is QuotedStructure qs2)
            {
                if (qs2.StartQuote is not null)
                    e.SetAttribute("startQuote", qs2.StartQuote);
                if (qs2.EndQuote is not null)
                    e.SetAttribute("endQuote", qs2.EndQuote);
            }
            AddDivisions(e, qs.Contents);
        }

    }

}
