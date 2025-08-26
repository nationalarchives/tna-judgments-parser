
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
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
                else
                {
                    throw new Exception(block.GetType().ToString());
                }
            }
        }

        protected override XmlElement AddAndWrapText(XmlElement parent, string name, IFormattedText model)
        
        {
            // remove leading and trailing whitespace from name.
            return base.AddAndWrapText(parent, name.Trim(), model);
        }

        override protected void p(XmlElement parent, ILine line) {
            if (line is IUnknownLine)
            {
                // Putting this here for now, this should eventually be moved to a method IUnknownLine.Add(parent)
                // but for now we need access to the AddInline method
                XmlElement p = parent.OwnerDocument.CreateElement("p", parent.NamespaceURI);
                p.SetAttribute("class", parent.NamespaceURI, "unknownImport");
                foreach (IInline inline in line.Contents)
                {
                    AddInline(p, inline);
                }
                parent.AppendChild(p);
            }
            else
            {
                base.p(parent, StripLeadingAndTrailingWhitespace(line));

            }
        }

        private ILine StripLeadingAndTrailingWhitespace(ILine line)
        {
            if (line is not WLine wLine)
                return line;

            List<IInline> fixedInlines = [];

            // Fix start of first inline
            IInline first = line.Contents.First();
            if (first is WText text)
            {
                // regex selects any leading whitespace and removes it
                string fixedText = Regex.Replace(text.Text, @"^\s*", "");
                if (line.Contents.Count() <= 1)
                {
                    // if there is only one WLine also removes trailing whitespace
                    fixedText = Regex.Replace(fixedText, @"\s*$", "");
                }
                WText fixedInline = new WText(fixedText, text.properties);
                fixedInlines.Add(fixedInline);
            }
            else
                fixedInlines.Add(first);

            // Add middle inlines
            if (line.Contents.Count() >= 3)
                fixedInlines.AddRange(line.Contents.Skip(1).SkipLast(1));

            // Fix end of last inline
            if (line.Contents.Count() >= 2)
            {
                IInline last = line.Contents.Last();
                if (last is WText text2)
                {
                    // regex selects any trailing whitespace and removes it
                    string fixedText = Regex.Replace(text2.Text, @"\s*$", "");
                    WText fixedInline = new WText(fixedText, text2.properties);
                    fixedInlines.Add(fixedInline);
                }
                else
                    fixedInlines.Add(last);
            }
            return new WLine(line as WLine, fixedInlines);
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


    }

}
