
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder(Document bill) : AkN.Builder
    {

        override protected string UKNS => "https://www.legislation.gov.uk/namespaces/UK-AKN";

        public static XmlDocument Build(Document bill)
        {
            return new Builder(bill).Build();
        }

        private readonly Document bill = bill;

        private XmlDocument Build()
        {
            XmlElement akomaNtoso = CreateAndAppend("akomaNtoso", doc);
            akomaNtoso.SetAttribute("xmlns:ukl", UKNS);


            XmlElement main = CreateAndAppend("bill", akomaNtoso);
            main.SetAttribute("name", this.bill.Type.ToString().ToLower());

            string title = Metadata.Extract(bill).Title;
            MetadataBuilder.Add(main, title);

            AddCoverPage(main, bill.CoverPage);
            AddPreface(main, bill.Preface);
            AddPreamble(main, bill.Preamble);
            AddBody(main, bill.Body, bill.Schedules); // bill.Schedules will always be empty here as they are part of bill.Body
            AddConclusions(main, bill.Conclusions);

            return doc;
        }

        private void AddCoverPage(XmlElement bill, IList<IBlock> coverPage)
        {
            XmlElement e = CreateAndAppend("coverPage", bill);
            AddBlocks(e, coverPage);
        }

        private void AddPreface(XmlElement bill, IList<IBlock> prefaceElements)
        {
            if (prefaceElements.Count <= 0)
            {
                logger.LogWarning("The parsed Preface elements were empty!");
                return;
            }
            XmlElement preface = CreateAndAppend("preface", bill);
            preface.SetAttribute("eId", "preface");
            XmlElement longTitle = CreateAndAppend("longTitle", preface);

            List<string> standardPrefaceElements = ["A", "Bill", "To"];
            // We could make this a bit more robust by also allowing "A Bill to" text that isn't in separate IBlocks
            IEnumerable<XmlElement> elements = prefaceElements
                .Select(block => {
                    if (block is not WLine)
                    {
                        logger.LogWarning("Preface contains an element that isn't a WLine!");
                        return null;
                    }
                    WLine line = block as WLine;
                    XmlElement p = doc.CreateElement("p", ns);
                    string text = Regex.Replace(line.NormalizedContent, @"\s", "").ToLower();
                    switch (text) {
                    case "a":
                        p.SetAttribute("class", ns, "A");
                        p.InnerText = "A";
                        break;
                    case "bill":
                        p.SetAttribute("class", ns, "Bill");
                        p.InnerText = "bill";
                        break;
                    case "to":
                        p.SetAttribute("class", ns, "To");
                        p.InnerText = "to";
                        break;
                    default:
                        p.InnerText = line.NormalizedContent;
                        break;
                    }
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
            if (preamble.Count <= 0)
            {
                logger.LogWarning("The parsed Preamble elements were empty!");
                return;
            }
            XmlElement e = CreateAndAppend("preamble", bill);
            e.SetAttribute("eId", "preamble");
            XmlElement formula = CreateAndAppend("formula", e);
            formula.SetAttribute("name", "enactingText");

            if (Frames.IsSecondaryDocName(this.bill.Type))
            {
                AddBlocks(formula, preamble);
                return;
            }

            foreach (IBlock block in preamble)
            {
                XmlElement element = doc.CreateElement("p", ns);
                if (!(block is WLine line))
                    continue;
                element.InnerText = line.NormalizedContent;
                element = TransformPreambleText(element);
                formula.AppendChild(element);
            }
        }

        private XmlElement TransformPreambleText(XmlElement pElement)
        {
            const string ENACTING_PREFIX = "be it enacted";
            string preambleText = pElement.InnerText;
            if (!preambleText.ToLower().StartsWith(ENACTING_PREFIX))
            {
                logger.LogWarning("Enacting text is malformed and does not start with \'BE IT ENACTED\'");
                return pElement;
            }
            string enactingPrefix = preambleText[..ENACTING_PREFIX.Length];

            XmlElement dropCapB = doc.CreateElement("inline", ns);
            dropCapB.SetAttribute("name","dropCap");
            dropCapB.InnerText = enactingPrefix[..1];
            XmlElement smallCaps = doc.CreateElement("inline", ns);
            smallCaps.SetAttribute("name", "smallCaps");
            smallCaps.InnerText = enactingPrefix[1..ENACTING_PREFIX.Length].ToLower();
            XmlText text = doc.CreateTextNode(preambleText[ENACTING_PREFIX.Length..]);

            pElement.RemoveAll();
            pElement.AppendChild(dropCapB);
            pElement.AppendChild(smallCaps);
            pElement.AppendChild(text);
            return pElement;
        }

        private void AddConclusions(XmlElement main, IList<IDivision> conclusionElements)
        {
            if (conclusionElements.Count <= 0)
            {
                logger.LogWarning("The parsed Conclusions elements were empty!");
                return;
            }
            XmlElement conc = CreateAndAppend("conclusions", main);
            conc.SetAttribute("eId", "backCover");
            foreach (IDivision division in conclusionElements)
            {
                AddDivision(conc, division);
            }
        }

        private void AddBody(XmlElement main, IList<IDivision> divisions, IList<Schedule> schedules)
        {
            XmlElement body = CreateAndAppend("body", main);
            foreach (IDivision division in divisions)
            {
                AddDivision(body, division);
            }
        }

        /* */

        private void AddBlocks(XmlElement parent, IEnumerable<IBlock> blocks)
        {
            foreach (IBlock block in blocks)
            {
                if (block is IOldNumberedParagraph np)
                {
                    XmlElement container = doc.CreateElement("blockContainer", ns);
                    parent.AppendChild(container);
                    if (np.Number is not null)
                        AddAndWrapText(container, "num", np.Number);
                    this.p(container, np);
                }
                else if (block is ILine line)
                {
                    this.p(parent, line);
                }
                else if (block is Mod mod)
                {
                    AddMod(parent, mod);
                }
                else if (block is ITable table)
                {
                    AddTable(parent, table);
                }
                else if (block is LdappTableBlock tableBlock)
                {
                    AddTableBlock(parent, tableBlock);
                }
                else if (block is IQuotedStructure qs)
                {
                    AddQuotedStructure(parent, qs);
                }
                else if (block is IDivWrapper wrapper)
                {
                    AddDivision(parent, wrapper.Division);
                }
                else if (block is BlockList blockList)
                {
                    AddBlockList(parent, blockList);
                }
                else if (block is BlockListItem item)
                {
                    AddBlockListItem(parent, item);
                }
                else
                {
                    throw new Exception(block.GetType().ToString());
                }
            }
        }

        override protected void p(XmlElement parent, ILine line) {
        if (line is IUnknownLine) {
            // Putting this here for now, this should eventually be moved to a method IUnknownLine.Add(parent)
            // but for now we need access to the AddInline method
            XmlElement p = parent.OwnerDocument.CreateElement("p", parent.NamespaceURI);
            p.SetAttribute("class", parent.NamespaceURI, "unknownImport");
            foreach(IInline inline in line.Contents) {
                AddInline(p, inline);
            }
            parent.AppendChild(p);
        } else
            base.p(parent, line);
        }

        private int quoteDepth = 0;

        protected override void AddQuotedStructure(XmlElement parent, IQuotedStructure qs)
        {
            XmlElement e = CreateAndAppend("quotedStructure", parent);
            if (qs is BlockQuotedStructure qs2)
            {
                if (qs2.StartQuote is not null)
                    e.SetAttribute("startQuote", qs2.StartQuote);
                if (qs2.EndQuote is not null)
                    e.SetAttribute("endQuote", qs2.EndQuote);
                if (qs2.AppendText is not null)
                    AddAppendText(parent, qs2.AppendText);
                e.SetAttribute("indent", UKNS, "indent0");

                // These contexts modify parsing behaviour, but should NOT be reflected in the context attribute
                if (new[] { Context.REGULATIONS, Context.RULES, Context.ARTICLES }.Contains(qs2.Context))
                    qs2.Context = Context.SECTIONS;
                e.SetAttribute("context", UKNS, Contexts.ToBodyOrSch(qs2.Context));
                e.SetAttribute("docName", UKNS, qs2.DocName.ToString().ToLower());

                if (qs2.HasInvalidCode)
                    e.SetAttribute("class", UKNS, "unknownImport");
            }
            quoteDepth += 1;
            AddDivisions(e, qs.Contents);
            quoteDepth -= 1;
        }

        protected override void AddFootnote(XmlElement parent, IFootnote fn)
        {
            XmlElement authorialNote = doc.CreateElement("authorialNote", ns);
            parent.AppendChild(authorialNote);
            authorialNote.SetAttribute("class", ns, "footnote");
            authorialNote.SetAttribute("marker", fn.Marker);
            IEnumerable<IBlock> content = FootnoteEnricher.EnrichInside(fn.Content);
            blocks(authorialNote, content);
        }

        protected void AddBlockList(XmlElement parent, BlockList blockList)
        {
            XmlElement bl = CreateAndAppend("blockList", parent);
            if (blockList.Intro is not null)
            {
                XmlElement intro = CreateAndAppend("listIntroduction", bl);
                AddInlines(intro, blockList.Intro.Contents);
            }
            AddBlocks(bl, blockList.Children);
        }

        protected void AddBlockListItem(XmlElement parent, BlockListItem item)
        {
            XmlElement e = CreateAndAppend("item", parent);
            // Handle Word's weird bullet character
            if (item.Number is not null && item.Number is WText wText)
            {
                string newNum = new string(wText.Text.Select(c => ((uint)c == 61623) ? '\u2022' : c).ToArray());
                AddAndWrapText(e, "num", new WText(newNum, wText.properties));
            }
            AddBlocks(e, item.Contents);
        }

    }

}
