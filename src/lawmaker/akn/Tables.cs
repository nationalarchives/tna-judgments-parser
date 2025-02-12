
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder : AkN.Builder
    {

        private static readonly string HtmlNamespace = "http://www.w3.org/1999/xhtml";

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

        // in future we can combine this with base function so logic isn't duplicated
        override protected void AddTable(XmlElement parent, ITable model)
        {
            XmlElement tblock = CreateAndAppend("tblock", parent);
            XmlElement foreign = CreateAndAppend("foreign", tblock);
            XmlElement table = CreateAndAppendHtml("table", foreign);

            List<float> widths = model.ColumnWidthsIns;
            XmlElement colgroup = CreateAndAppendHtml("colgroup", table);
            foreach (var width in widths)
            {
                XmlElement col = CreateAndAppendHtml("col", colgroup);
                // currently "class" and "style" attributes need to be in a non-empty namespace
                col.SetAttribute("style", UKNS, "width: " + CSS.ConvertSize(width, "in"));
            }

            /* This keeps a grid of cells, with the dimensions the table would have
            /* if none of the cells were merged. Merged cells are repeated.
            /* The purpose is to find the correct cell above for vertically merged cells. */
            List<List<XmlElement>> allCellsWithRepeats = [];

            List<List<ICell>> rows = [.. model.Rows.Select(r => r.Cells.ToList())]; // enrichers are lazy
            int iRow = 0;
            foreach (List<ICell> row in rows)
            {

                List<XmlElement> thisRowOfCellsWithRepeats = [];
                allCellsWithRepeats.Add(thisRowOfCellsWithRepeats);

                bool rowIsHeader = model.Rows.ElementAt(iRow).IsHeader;
                XmlElement tr = CreateHtml("tr");
                int iCell = 0;
                foreach (ICell cell in row)
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
                    XmlElement td = CreateHtml(rowIsHeader ? "th" : "td");
                    if (cell.ColSpan is not null)
                        td.SetAttribute("colspan", cell.ColSpan.ToString());
                    Dictionary<string, string> styles = cell.GetCSSStyles();
                    if (styles.Any())
                        td.SetAttribute("style", AkN.CSS.SerializeInline(styles));
                    tr.AppendChild(td);
                    this.blocks(td, cell.Contents);

                    int colspan = cell.ColSpan ?? 1;
                    for (int i = 0; i < colspan; i++)
                        thisRowOfCellsWithRepeats.Add(td);
                    iCell += colspan;
                }
                if (tr.HasChildNodes)
                {   // some rows might contain nothing but merged cells
                    table.AppendChild(tr);
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

    }

}
