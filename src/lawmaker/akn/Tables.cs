
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder : AkN.Builder
    {

        private static ILogger logger = Logging.Factory.CreateLogger<UK.Gov.Legislation.Lawmaker.Builder>();

        public static readonly string HtmlNamespace = "http://www.w3.org/1999/xhtml";

        protected XmlElement CreateHtml(string name)
        {
            return doc.CreateElement("html", name, HtmlNamespace);
        }
        protected XmlElement CreateAndAppendHtml(string name, XmlNode parent)
        {
            XmlElement e = doc.CreateElement("html", name, HtmlNamespace);
            parent.AppendChild(e);
            return e;
        }

        override protected void Block(XmlElement parent, IBlock block)
        {
            if (block is LdappTableBlock tableBlock)
                AddTableBlock(parent, tableBlock);
            else
            {
                base.Block(parent, block);
            }
        }


        // in future we can combine this with base function so logic isn't duplicated
        override protected void AddTable(XmlElement parent, ITable model)
        {
            XmlElement tblock = CreateAndAppend("tblock", parent);
            XmlElement foreign = CreateAndAppend("foreign", tblock);
            XmlElement table = BuildTable(model, parent);
            foreign.AppendChild(table);

            tblock.SetAttribute("class", AknNamespace, "table");
        }

        private void AddTableBlock(XmlElement parent, LdappTableBlock tableBlock)
        {
            XmlElement tblock = CreateAndAppend("tblock", parent);
            if (!String.IsNullOrEmpty(tableBlock.TableNumber?.Number?.TextContent))
            {
                XmlElement num = CreateAndAppend("num", tblock);
                num.InnerText = tableBlock.TableNumber.Number.TextContent;
                AlignmentValues? numAlignment = tableBlock.TableNumber.Number.GetEffectiveAlignment();
                if (numAlignment is not null)
                {
                    num.SetAttribute("class", AknNamespace, numAlignment.Value.ToXmlClassValue());

                }
                var captions = tableBlock.TableNumber.Captions;
                if (captions != null && captions.Count > 0)
                {
                    // first caption is inserted as heading
                    XmlElement heading = CreateAndAppend("heading", tblock);
                    heading.InnerText = captions.First().TextContent;
                    AlignmentValues? headingAlignment = captions.First().GetEffectiveAlignment();
                    string headingAlignmentClass = headingAlignment?.ToXmlClassValue() ?? "left";
                    heading.SetAttribute("class", AknNamespace, headingAlignmentClass);

                    // others are inserted as subheading
                    foreach (WLine caption in captions.Skip(1))
                    {
                        XmlElement subheading = CreateAndAppend("subheading", tblock);
                        subheading.InnerText = caption.TextContent;
                        AlignmentValues? subheadingAlignment = caption.GetEffectiveAlignment();
                        string subheadingAlignmentClass = subheadingAlignment?.ToXmlClassValue() ?? "left";
                        subheading.SetAttribute("class", AknNamespace, subheadingAlignmentClass);
                    }
                    ;
                }
            }
            XmlElement foreign = CreateAndAppend("foreign", tblock);
            XmlElement table = BuildTable(tableBlock.Table, parent);
            foreign.AppendChild(table);
            tblock.SetAttribute("class", AknNamespace, "table");
        }

        private static bool IsCommencementHistory(XmlElement element) => element.GetAttribute("class") == "commencementHistory";

        private XmlElement BuildTable(ITable model, XmlElement parent)
        {
            XmlElement table = CreateElement("table");
            table.SetAttribute("xmlns", HtmlNamespace);
            table.SetAttribute("xmlns:akn", AknNamespace);
            string className = IsCommencementHistory(parent) ? "topAndBottom tablecenter width100" : "allBorders tableleft width100";
            table.SetAttribute("class", HtmlNamespace, className);
            table.SetAttribute("cols", model.ColumnWidthsIns.Count.ToString());
            
            IEnumerable<IEnumerable<IRow>> rowsGroupedByHeaders = GroupRowsByHeaders(model.Rows);

            foreach (IEnumerable<IRow> rows in rowsGroupedByHeaders)
            {
                AddRows(parent, table, rows.TakeWhile(row => row.IsImplicitHeader));
                AddRows(parent, table, rows.SkipWhile(row => row.IsImplicitHeader));
            }
            return table;
        }
        protected void AddRows(XmlElement parent, XmlElement table, IEnumerable<IRow> rows)
        {
            if (!rows.Any(_ => true))
            {
                return;
            }
            /* This keeps a grid of cells, with the dimensions the table would have
            /* if none of the cells were merged. Merged cells are repeated.
            /* The purpose is to find the correct cell above for vertically merged cells. */
            List<List<XmlElement>> allCellsWithRepeats = [];
            int iRow = 0;

            bool isCollectionOfHeaderRows = rows.All(row => row.IsImplicitHeader);
            XmlElement tbody = isCollectionOfHeaderRows ? CreateAndAppend("thead", table) : CreateAndAppend("tbody", table);
            if (isCollectionOfHeaderRows)
                tbody.SetAttribute("class", HtmlNamespace, IsCommencementHistory(parent) ? "italic left" : "bold centre");
            else
                tbody.SetAttribute("class", HtmlNamespace, "left");

            foreach (IRow row in rows)
            {
                // The merging is probably borked now
                List<XmlElement> thisRowOfCellsWithRepeats = [];
                allCellsWithRepeats.Add(thisRowOfCellsWithRepeats);

                XmlElement tr = CreateElement("tr");
                int iCell = 0;
                foreach (ICell cell in row.Cells)
                {
                    if (cell.VMerge == VerticalMerge.Continuation)
                    {
                        // the cell above for which this is a continuation
                        XmlElement above = allCellsWithRepeats[iRow - 1][iCell];
                        incrementRowspan(above);
                        this.blocks(above, cell.Contents);
                        int colspanAbove = getColspan(above);
                        for (int i = 0; i < colspanAbove; i++)
                            thisRowOfCellsWithRepeats.Add(above);
                        iCell += colspanAbove;
                        continue;
                    }
                    XmlElement td = CreateElement(isCollectionOfHeaderRows ? "th" : "td");

                    if (cell.ColSpan is not null)
                        td.SetAttribute("colspan", cell.ColSpan.ToString());
                    Dictionary<string, string> styles = cell.GetCSSStyles();
                    if (styles.Any())
                        td.SetAttribute("style", AkN.CSS.SerializeInline(styles));
                    tr.AppendChild(td);
                    AddBlocks(td, cell.Contents);

                    // all direct children of a td in Lawmaker gets the AKN as a default namespace
                    foreach (XmlElement element in td.ChildNodes.OfType<XmlElement>())
                    {
                        element.SetAttribute("xmlns", AknNamespace);
                    }

                    int colspan = cell.ColSpan ?? 1;
                    for (int i = 0; i < colspan; i++)
                        thisRowOfCellsWithRepeats.Add(td);
                    iCell += colspan;
                }
                if (tr.HasChildNodes)
                {   // some rows might contain nothing but merged cells
                    tbody.AppendChild(tr);
                }
                else
                {
                    // if row is not added, rowspans in row above may need to be adjusted, e.g., [2024] EWHC 2920 (KB)
                    List<XmlElement> above = allCellsWithRepeats[iRow - 1];
                    DecrementRowspans(above);
                }
                iRow += 1;
            }
        }

        // Group up header rows with their proceeding body rows
        // A list that looks like this:
        // [header header row row header row row header row]
        // will become this:
        // [[header header row row] [header row row] [header row]]
        private static IEnumerable<IEnumerable<IRow>> GroupRowsByHeaders(IEnumerable<IRow> rows)
        {
            List<(List<IRow>, List<IRow>)> headersWithRows =
                rows.Aggregate(new List<(List<IRow>, List<IRow>)> { ([], []) },
                (acc, row) => {
                (var currentHeaders, var currentRows) = acc.Last();
                if (row.IsImplicitHeader && currentRows.Count > 0)
                {
                    acc.Add(([row], []));
                } else if (row.IsImplicitHeader)
                {
                    currentHeaders.Add(row);
                } else
                {
                    currentRows.Add(row);
                }
                return acc;
            });
            return headersWithRows.Select(rows => {
                (var headerRows, var bodyRows) = rows;
                return headerRows.Concat(bodyRows);
            });
        }

    }
}
