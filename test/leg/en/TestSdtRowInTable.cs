using DocumentFormat.OpenXml.Packaging;

using Xunit;

using UK.Gov.NationalArchives.CaseLaw.Parse;

namespace test.leg.en;

/// <summary>
/// Verifies that documents containing SdtRow (structured document tags)
/// inside Word tables can be parsed without throwing an exception.
/// See: https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.sdtrow
/// </summary>
public class TestSdtRowInTable {

    [Fact]
    public void DocumentWithSdtRowInTable_ParsesSuccessfully() {
        // sdt-row-in-table.docx contains a table where rows are wrapped
        // in <w:sdt> content controls. Before the fix, this threw
        // System.Exception in WTable.ParseTableChild.
        var docx = DocumentHelpers.ReadDocx("test.leg.en.sdt-row-in-table.docx");

        using var stream = new System.IO.MemoryStream(docx);
        using var doc = WordprocessingDocument.Open(stream, false);

        var preParsed = new PreParser().Parse(doc);

        // If we get here without an exception, the SdtRow was handled
        Assert.NotNull(preParsed);
        Assert.NotEmpty(preParsed.Body);
    }

}
