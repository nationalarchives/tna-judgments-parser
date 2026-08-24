using System.Linq;
using System.Xml;

using UK.Gov.Legislation.Common;

using Xunit;

using AknBuilder = UK.Gov.Legislation.Judgments.AkomaNtoso.Builder;
using DocumentHelpers = test.DocumentHelpers;

namespace UK.Gov.Legislation.Common.Test;


/// <summary>
/// The provenance marker's route to the published AKN.
/// </summary>
/// <remarks>
/// The leg builder writes <c>uk:class="flush-with-parent"</c> and relies on the
/// simplifier promoting a namespaced class to a plain one
/// (<c>Simplify.cs:158-171</c>). That is a narrow path deliberately chosen over
/// exempting <c>subparagraph</c> from stripping altogether: these tests exist to
/// hold it narrow, so a Word style class or an inline style on a subparagraph
/// keeps being stripped as it always was.
/// </remarks>
public class TestFlushWithParentMarker
{

    private const string UKNS = "https://legislation.gov.uk/akn";

    /// <summary>A one-subparagraph document, with whatever attributes the caller wants on it.</summary>
    private static XmlDocument Subparagraph(params (string name, string ns, string value)[] attributes)
    {
        XmlDocument doc = new();
        var root = doc.CreateElement("akomaNtoso", AknBuilder.ns);
        doc.AppendChild(root);
        var sub = doc.CreateElement("subparagraph", AknBuilder.ns);
        root.AppendChild(sub);
        foreach (var (name, ns, value) in attributes)
        {
            if (ns is null)
                sub.SetAttribute(name, value);
            else
                sub.SetAttribute(name, ns, value);
        }
        return doc;
    }

    private static XmlElement OnlySubparagraph(XmlDocument doc) =>
        doc.DocumentElement.GetElementsByTagName("subparagraph", AknBuilder.ns)
                           .Cast<XmlElement>().Single();

    [Fact]
    public void TheMarkerSurvivesSimplificationAsAPlainClass()
    {
        var doc = Subparagraph(("class", UKNS, "flush-with-parent"));
        LegSimplifier.Simplify(doc);

        var sub = OnlySubparagraph(doc);
        Assert.Equal("flush-with-parent", sub.GetAttribute("class"));
        // promoted, not duplicated — the namespaced original must not survive too
        Assert.Equal("", sub.GetAttribute("class", UKNS));
    }

    [Fact]
    public void AnUnrelatedClassOnASubparagraphIsStillStripped()
    {
        // Pre-simplification a plain @class is a Word style name. Nothing writes
        // one on a subparagraph today, but the marker's route must not become a
        // standing exemption that would let one through tomorrow.
        var doc = Subparagraph(("class", null, "EMLevel1Paragraph"));
        LegSimplifier.Simplify(doc);

        Assert.Equal("", OnlySubparagraph(doc).GetAttribute("class"));
    }

    [Fact]
    public void AnInlineStyleOnASubparagraphIsStillStripped()
    {
        var doc = Subparagraph(("style", null, "margin-left:2in"));
        LegSimplifier.Simplify(doc);

        Assert.Equal("", OnlySubparagraph(doc).GetAttribute("style"));
    }

    [Fact]
    public void AWordStyleClassIsStrippedWhileTheMarkerSurvivesOnTheSameElement()
    {
        var doc = Subparagraph(
            ("class", null, "EMLevel1Paragraph"),
            ("class", UKNS, "flush-with-parent"));
        LegSimplifier.Simplify(doc);

        Assert.Equal("flush-with-parent", OnlySubparagraph(doc).GetAttribute("class"));
    }

    [Fact]
    public void TheMarkerReachesTheAknThroughTheWholeParsePath()
    {
        // Builder -> simplifier -> serialised AKN, on a real document. The unit
        // tests above exercise the promotion in isolation; this one proves the
        // leg builder actually takes that route.
        var docx = DocumentHelpers.ReadDocx("test.leg.cop.uksicop_20180470_en.docx");
        var xml = CodesOfPractice.Helper.Parse(docx, "uksicop_20180470_en.docx").Document;

        var marked = xml.GetElementsByTagName("subparagraph", AknBuilder.ns)
                        .Cast<XmlElement>()
                        .Count(e => e.GetAttribute("class") == "flush-with-parent");
        Assert.Equal(14, marked);
        Assert.DoesNotContain("uk:class", xml.OuterXml);
    }

    [Theory]
    // 14 marked, all numbered.
    [InlineData("test.leg.cop.uksicop_20180470_en.docx", "cop", 14)]
    // 57 marked, four of them the numberless subparagraphs I7 absorbs — the
    // shape most likely to trip a schema that expects a num.
    [InlineData("test.leg.em.original_filenames.uksiem_20240868_en_001.docx", "em", 57)]
    public void TheMarkerIsValidAgainstCanonicalAkn(string resource, string type, int expectedMarked)
    {
        // The subschema is a local narrowing we control and had to widen to
        // admit @class; that says nothing about interoperability. This asserts
        // the published AKN is still valid against the OASIS Akoma Ntoso 3.0
        // schema, which is the contract anyone downstream reads.
        var docx = DocumentHelpers.ReadDocx(resource);
        var name = resource.Split('.')[^2];
        var xml = type == "cop"
            ? CodesOfPractice.Helper.Parse(docx, name + ".docx").Document
            : ExplanatoryMemoranda.Helper.Parse(docx, name + ".docx").Document;

        var marked = xml.GetElementsByTagName("subparagraph", AknBuilder.ns)
                        .Cast<XmlElement>()
                        .Count(e => e.GetAttribute("class") == "flush-with-parent");
        Assert.Equal(expectedMarked, marked);

        Assert.Empty(Validator.Shared.ValidateAgainstMainAkn(xml));
    }

}
