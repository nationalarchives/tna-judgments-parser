#nullable enable

using System.IO;
using System.Linq;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Lawmaker.Api;

using Xunit;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Lawmaker;

/// <summary>
/// A table whose italic first row is treated as a header, with a cell merged down into the
/// second row, failed to build because the two rows were built in separate passes (LCO-5192).
/// </summary>
public class TestTableHeaderMerge
{

    private static readonly LegislationClassifier Classifier = new(DocName.UKSI, "order", "made");
    private static readonly LanguageService Languages = new(["en"]);

    [Fact]
    public void Parse_HeaderCellMergedIntoNextRow_BuildsBothRowsInTheHeader()
    {
        var docx = CreateDocx(new Table(
            new TableGrid(new GridColumn { Width = "2000" }, new GridColumn { Width = "2000" }, new GridColumn { Width = "2000" }),
            new TableRow(
                Cell("Development area node point", italic: true, new W.VerticalMerge { Val = MergedCellValues.Restart }),
                Cell("WGS84 UTM Zone 31N", italic: true, new GridSpan { Val = 2 })),
            new TableRow(
                Cell("", italic: true, new W.VerticalMerge()),
                Cell("Easting", italic: true),
                Cell("Northing", italic: true)),
            new TableRow(
                Cell("1"),
                Cell("401818"),
                Cell("5992480"))));

        Response response = Helper.Parse(docx, Classifier, Languages);

        response.Error.ShouldBeNull();
        var xml = new XmlDocument();
        xml.LoadXml(response.Xml);
        var ns = new XmlNamespaceManager(xml.NameTable);
        ns.AddNamespace("h", "http://www.w3.org/1999/xhtml");

        var headerRows = xml.SelectNodes("//h:table/h:thead/h:tr", ns)!;
        headerRows.Count.ShouldBe(2);
        var merged = (XmlElement)headerRows[0]!.SelectSingleNode("h:th", ns)!;
        merged.InnerText.Trim().ShouldBe("Development area node point");
        merged.GetAttribute("rowspan").ShouldBe("2");
        headerRows[1]!.SelectNodes("h:th", ns)!.Cast<XmlNode>().Select(th => th.InnerText.Trim()).ShouldBe(["Easting", "Northing"]);

        var bodyRows = xml.SelectNodes("//h:table/h:tbody/h:tr", ns)!;
        bodyRows.Count.ShouldBe(1);
        bodyRows[0]!.SelectNodes("h:td", ns)!.Cast<XmlNode>().Select(td => td.InnerText.Trim()).ShouldBe(["1", "401818", "5992480"]);
    }

    private static TableCell Cell(string text, bool italic = false, params DocumentFormat.OpenXml.OpenXmlElement[] properties)
    {
        var runProperties = italic ? new RunProperties(new Italic()) : new RunProperties();
        return new TableCell(
            new TableCellProperties(properties),
            new Paragraph(new Run(runProperties, new Text(text))));
    }

    private static byte[] CreateDocx(Table table)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            MainDocumentPart main = doc.AddMainDocumentPart();
            main.Document = new W.Document(new Body(table, new SectionProperties()));
            main.AddNewPart<StyleDefinitionsPart>().Styles = new Styles(new DocDefaults(new RunPropertiesDefault(
                new RunPropertiesBaseStyle(new RunFonts { Ascii = "Times New Roman" }, new FontSize { Val = "24" }))));
        }
        return stream.ToArray();
    }

}
