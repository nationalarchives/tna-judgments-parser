using System.Xml;

using test;

using Xunit;

namespace UK.Gov.Legislation.Test;


/// <summary>
/// Pins the wiring between <see cref="LegVersion.Current"/> and the
/// <c>Name="legislation"</c> entry of <c>&lt;ukm:Parser&gt;</c>.
/// </summary>
/// <remarks>
/// The per-type fixture comparators (TestEM, TestCoP, TestEN, TestIA,
/// TestOD, TestTN) all strip <c>ukm:Parser</c> before comparing, so a
/// green fixture suite says nothing about what version the parser
/// actually emits — or whether it emits one at all. Nothing else in the
/// test suite reads <c>LegVersion</c>.
///
/// The assertion is deliberately against the constant rather than a
/// literal: a test pinning "1.1.0" would have to be edited in lockstep
/// with every bump, and a test you must update to keep green does not
/// verify much. What can actually break is the wiring — the constant
/// not reaching the output — and that is what this covers.
/// </remarks>
public class TestLegParserVersion
{

    private const string UKM_NS = "http://www.legislation.gov.uk/namespaces/metadata";

    private static XmlDocument ParseFixture()
    {
        const string name = "uksiem_20140198_en";
        var docx = DocumentHelpers.ReadDocx($"test.leg.em.original_filenames.{name}.docx");
        var parsed = ExplanatoryMemoranda.Helper.Parse(
            docx, name + ".docx", renderer: LocalRendererHelper.GetOrNull());
        return parsed.Document;
    }

    [Fact]
    public void EmitsLegVersionAsTheLegislationParserEntry()
    {
        var akn = ParseFixture();
        var nsmgr = new XmlNamespaceManager(akn.NameTable);
        nsmgr.AddNamespace("ukm", UKM_NS);

        var leg = akn.SelectSingleNode("//ukm:Parser[@Name='legislation']", nsmgr) as XmlElement;

        Assert.NotNull(leg);
        Assert.Equal(LegVersion.Current, leg.GetAttribute("Value"));
    }

    [Fact]
    public void EmitsBothParserEntries()
    {
        var akn = ParseFixture();
        var nsmgr = new XmlNamespaceManager(akn.NameTable);
        nsmgr.AddNamespace("ukm", UKM_NS);

        var entries = akn.SelectNodes("//ukm:Parser", nsmgr);

        Assert.Equal(2, entries.Count);
        var core = akn.SelectSingleNode("//ukm:Parser[@Name='core']", nsmgr) as XmlElement;
        Assert.NotNull(core);
        Assert.False(string.IsNullOrEmpty(core.GetAttribute("Value")));
    }

}
