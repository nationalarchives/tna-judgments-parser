
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.DOCX;
using UK.Gov.Legislation.Judgments.Parse;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder(Document bill, LanguageService languageService) : AkN.Builder
    {

        override protected string UKNS => "https://www.legislation.gov.uk/namespaces/UK-AKN";

        public static XmlDocument Build(Document bill, LanguageService languageService)
        {
            return new Builder(bill, languageService).Build();
        }

        private readonly Document bill = bill;

        private XmlDocument Build()
        {
            XmlElement akomaNtoso = CreateAndAppend("akomaNtoso", doc);
            akomaNtoso.SetAttribute("xmlns:ukl", UKNS);


            XmlElement main = CreateAndAppend("bill", akomaNtoso);
            main.SetAttribute("name", this.bill.Type.ToString().ToLower());

            AddCoverPage(main, bill.CoverPage);
            AddPreface(main, bill.Preface);
            AddPreamble(main, bill.Preamble);
            AddBody(main, bill.Body, bill.Schedules); // bill.Schedules will always be empty here as they are part of bill.Body
            AddConclusions(main, bill.Conclusions);

            Metadata.ExtractTitle(bill, logger, bill.Metadata);
            main.PrependChild(bill.Metadata.Build(bill).ToXmlNode(main.OwnerDocument));

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
            if (prefaceElements.All(e => e is IBuildable<XNode>))
            {
                foreach (XmlNode node in prefaceElements
                    .OfType<IBuildable<XNode>>()
                    .Select(e => e.Build(this.bill).ToXmlNode(bill.OwnerDocument)))
                {
                    bill.AppendChild(node);
                }
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

            if (this.bill.Type.IsSecondaryDocName())
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

        private void AddConclusions(XmlElement main, IList<BlockContainer> conclusionElements)
        {
            if (conclusionElements.Count <= 0)
            {
                logger.LogWarning("The parsed Conclusions elements were empty!");
                return;
            }
            XmlElement conc = CreateAndAppend("conclusions", main);
            conc.SetAttribute("eId", "backCover");
            foreach (BlockContainer blockContainer in conclusionElements)
            {
                AddBlockContainer(conc, blockContainer);
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
                if (block is WOldNumberedParagraph np)
                {
                    // In Lawmaker, by default, all numbered paragraphs should be marked up
                    // as regular p elements
                    List<IInline> inlines = [np.Number];
                    if (np.Contents.Count() > 0)
                        inlines.AddRange([new WText(" ", null), .. np.Contents]);
                    this.p(parent, new WLine(np, inlines));
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
                else if (block is ISignatureBlock sigBlock)
                {
                    AddSigBlock(parent, sigBlock);
                }
                else if (block is BlockContainer blockContainer)
                {
                    AddBlockContainer(parent, blockContainer);
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
                base.p(parent, TrimLine(line));
        }

        /// <summary>
        /// A wrapper for the <c>Block</c> method which strips leading and trailing white space from <paramref name="line"/>,
        /// before inserting it as an XML element with tagname <paramref name="name"/> as a child of <paramref name="parent"/>.
        /// </summary>
        /// <param name="parent">The parent element</param>
        /// <param name="line">The line to be added as a child element</param>
        /// <param name="name">The tag name of the inserted child element</param>
        /// <returns>The resulting child XML element</returns>
        protected override XmlElement Block(XmlElement parent, ILine line, string name)
        {
            ILine stripped = TrimLine(line);
            return base.Block(parent, stripped, name);
        }

        /// <summary>
        /// Trims any whitespace from the beginning or end of a line.
        /// </summary>
        /// <param name="line">The line to trim</param>
        /// <returns>The trimmed line</returns>
        private ILine TrimLine(ILine line)
        {
            if (line is not WLine wLine)
                return line;
            if (line.Contents.Count() == 0)
                return line;

            List<IInline> trimmedInlines = [];

            // Remove starting and ending inlines which are entirely white space
            IEnumerable<IInline> newContents = line.Contents
                .SkipWhile(IInline.IsEmpty).Reverse()
                .SkipWhile(IInline.IsEmpty).Reverse();

            // Trim start of first inline
            IInline first = newContents.First();
            if (first is WText text)
            {
                // regex selects any leading whitespace and removes it
                string fixedText = Regex.Replace(text.Text, @"^\s*", "");
                if (newContents.Count() == 1)
                {
                    // if there is only one WLine also removes trailing whitespace
                    fixedText = Regex.Replace(fixedText, @"\s*$", "");
                }
                WText fixedInline = new WText(fixedText, text.properties);
                trimmedInlines.Add(fixedInline);
            }
            else
                trimmedInlines.Add(first);

            // Add middle inlines
            if (newContents.Count() >= 3)
                trimmedInlines.AddRange(newContents.Skip(1).SkipLast(1));

            // Trim end of last inline
            if (newContents.Count() >= 2)
            {
                IInline last = newContents.Last();
                if (last is WText text2)
                {
                    // regex selects any trailing whitespace and removes it
                    string fixedText = Regex.Replace(text2.Text, @"\s*$", "");
                    WText fixedInline = new WText(fixedText, text2.properties);
                    trimmedInlines.Add(fixedInline);
                }
                else
                    trimmedInlines.Add(last);
            }
            return new WLine(line as WLine, trimmedInlines);
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
            XmlElement itemElement = CreateAndAppend("item", parent);
            if (item.Number is not null)
            {
                // Handle Word's weird bullet character.
                string newNum = new string(item.Number.Text.Select(c => ((uint)c == 61623) ? '\u2022' : c).ToArray());
                if (item.Number is WText wText)
                    AddAndWrapText(itemElement, "num", new WText(newNum, wText.properties));
                else if (item.Number is WNumText WNumText)
                    AddAndWrapText(itemElement, "num", new WNumText(WNumText, newNum));
                else
                    AddAndWrapText(itemElement, "num", new WText(newNum, null));
            }
            if (item.Children.Count() > 0)
            {
                /* Handle nested BlockListItem children.
                 * In which case we must wrap them in a BlockList element:
                 *
                 * <item>                           <item>
                 *     <num>(1)</num>                   <num>(1)</num>
                 *     <p>Text1</p>                     <blockList>
                 *     <item>                               <listIntroduction>Text1</listIntroduction>
                 *         <num>(a)</num>      --->         <item>
                 *         <p>Text2</p>                         <num>(a)</num>
                 *     </item>                                  <p>Text2</p>
                 *     ...                                  </item>
                 * </item>                                  ...
                 *                                      </blockList>
                 *                                  </item>
                 */
                XmlElement blockListElement = CreateAndAppend("blockList", itemElement);
                // Handle listIntroduction
                XmlElement listIntroductionElement = CreateAndAppend("listIntroduction", blockListElement);
                foreach (IBlock block in item.Intro)
                {
                    if (block is WLine line)
                        AddInlines(listIntroductionElement, line.Contents);
                    else
                        AddBlocks(listIntroductionElement, [block]);
                }
                // Handle nested blockList children
                AddBlocks(blockListElement, item.Children);
            }
            else
                AddBlocks(itemElement, item.Intro);
        }

        protected void AddSigBlock(XmlElement parent, ISignatureBlock sig)
        {
            XmlElement block = CreateAndAppend("block", parent);
            block.SetAttribute("name", sig.Name);

            string dateString = null;
            if (sig.Name.Equals("date"))
            {
                // Only dates of the format "d MMMM yyyy" with or without an ordinal suffix will parse successfully
                // e.g. "17th June 2025" and "9 October 2021"
                // Any other format will result in the date attribute being set to "9999-01-01"
                string text = (sig.Content.First() as WText).Text;

                // Remove ordinal suffix from date if there is one
                Match match = Regex.Match(text, @"(\d+)(st|nd|rd|th)");
                if (match.Success)
                    // Extract the numeric day and remove the suffix from the original string
                    text = text.Replace(match.Value, match.Groups[1].Value);

                foreach (CultureInfo culture in languageService.Cultures)
                {
                    // this DateTime parsing should really be done in the parsing stage, not the building stage
                    bool parsedDate = DateTime.TryParseExact(text, "d MMMM yyyy", culture, DateTimeStyles.None, out DateTime dateTime);
                    if (parsedDate)
                    {
                        dateString = dateTime.ToString("yyyy-MM-dd");
                        break;
                    }
                    // Date was not parsed so set to dummy value
                    else
                    {
                        dateString = "9999-01-01";
                    }
                }
            }
            if (dateString is not null)
            {
                XmlElement date = CreateAndAppend("date", block);
                date.SetAttribute("date", dateString);
                AddInlines(date, sig.Content);
            }
            else
                AddInlines(block, sig.Content);
        }

    }

}
