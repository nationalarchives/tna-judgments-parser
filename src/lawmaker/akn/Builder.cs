
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using DocumentFormat.OpenXml.Vml;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
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
            akomaNtoso.SetAttribute("xmlns:ukl", UKNS);


            XmlElement main = CreateAndAppend("bill", akomaNtoso);
            main.SetAttribute("name", this.bill.Type);

            string title = Metadata.Extract(bill).Title;
            MetadataBuilder.Add(main, title);

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

        private void AddPreface(XmlElement bill, IList<IBlock> prefaceElements)
        {
            XmlElement preface = CreateAndAppend("preface", bill);
            preface.SetAttribute("eId", "preface");
            XmlElement longTitle = CreateAndAppend("longTitle", preface);

            List<string> standardPrefaceElements = ["A", "Bill", "To"];

            IEnumerable<XmlElement> elements = prefaceElements
                .Select(block => {
                    if (block is not WLine)
                    {
                        logger.LogWarning("Preface contains an element that isn't a WLine!");
                        return null;
                    }
                    WLine line = block as WLine;
                    XmlElement p = doc.CreateElement("p", ns);
                    switch (line.NormalizedContent.ToLower()) {
                    case "a":
                        p.SetAttribute("class", ns, "A");
                        break;
                    case "bill":
                        p.SetAttribute("class", ns, "Bill");
                        break;
                    case "to":
                        p.SetAttribute("class", ns, "To");
                        break;
                    }
                    p.InnerText = line.NormalizedContent;
                    return p;
                })
                .Where(b => b is not null);
                IEnumerable<XmlElement> longTitleText =
                elements
                .TakeWhile(e => standardPrefaceElements.Contains(e.GetAttribute("class", ns)))
                .Append(
                    elements
                    .SkipWhile(e => standardPrefaceElements.Contains(e.GetAttribute("class", ns)))
                    .Aggregate((XmlElement acc, XmlElement element) => {
                        acc.InnerText = acc.InnerText + " " + element.InnerText;
                        return acc;
                }));
            // LNI-224: For now we can assume any text content in the preface is the longTitle and
            // should just be in one <p> tag
            foreach (XmlElement element in longTitleText) {
                longTitle.AppendChild(element);
            }
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

        private int quoteDepth = 0;

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
                if (qs2.AppendText is not null)
                {
                    XmlElement at = CreateAndAppend("inline", mod);
                    at.SetAttribute("name", "AppendText");
                    AddOrWrapText(at, qs2.AppendText);
                }
            }
            quoteDepth += 1;
            AddDivisions(e, qs.Contents);
            quoteDepth -= 1;
        }

    }

}
