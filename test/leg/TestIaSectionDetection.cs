using System.Xml;

using Xunit;

namespace UK.Gov.Legislation.ImpactAssessments.Test {

/// <summary>
/// Unit tests for the IA section-detection heuristics
/// (<see cref="Helper.IsSectionHeaderParagraph"/> and
/// <see cref="Helper.IsSectionHeaderLevel"/>).
///
/// Each test constructs a minimal AKN fragment that captures one structural
/// shape and asserts the heuristic accepts/rejects it. Locks in the
/// LEG-150 colour-preservation pattern (where bold is wrapped in
/// <c>&lt;span style="color:..."&gt;</c>) as a positive case, since the
/// pre-LEG-151 direct-child query silently rejected those and dropped 279
/// sections across 28 of 32 IA fixtures.
/// </summary>
public class TestIaSectionDetection {

    private const string Akn = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";

    private static (XmlNode root, XmlNamespaceManager ns) Parse(string xml) {
        string wrapped = $"<x xmlns=\"{Akn}\">{xml}</x>";
        var doc = new XmlDocument();
        doc.LoadXml(wrapped);
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("akn", Akn);
        return (doc.DocumentElement.FirstChild, ns);
    }

    // --- IsSectionHeaderLevel ----------------------------------------------

    [Fact]
    public void Level_DirectBold_IsHeader() {
        // Pre-LEG-150 shape (still expected to work)
        var (level, ns) = Parse("<level><content><p><b>Evidence base</b></p></content></level>");
        Assert.True(Helper.IsSectionHeaderLevel(level, ns, out string heading));
        Assert.Equal("Evidence base", heading);
    }

    [Fact]
    public void Level_SpanWrappedBold_IsHeader() {
        // Post-LEG-150 colour-preservation shape — the regression case.
        var (level, ns) = Parse(
            "<level><content><p><span style=\"color:#000000\"><b>Evidence base</b></span></p></content></level>");
        Assert.True(Helper.IsSectionHeaderLevel(level, ns, out string heading));
        Assert.Equal("Evidence base", heading);
    }

    [Fact]
    public void Level_NoContent_IsNotHeader() {
        var (level, ns) = Parse("<level />");
        Assert.False(Helper.IsSectionHeaderLevel(level, ns, out string _));
    }

    [Fact]
    public void Level_MultipleParagraphs_IsNotHeader() {
        var (level, ns) = Parse("<level><content><p><b>One</b></p><p>Two</p></content></level>");
        Assert.False(Helper.IsSectionHeaderLevel(level, ns, out string _));
    }

    [Fact]
    public void Level_NoBold_IsNotHeader() {
        var (level, ns) = Parse("<level><content><p>Plain text</p></content></level>");
        Assert.False(Helper.IsSectionHeaderLevel(level, ns, out string _));
    }

    [Fact]
    public void Level_BoldTooShort_IsNotHeader() {
        var (level, ns) = Parse("<level><content><p><b>Hi</b></p></content></level>");
        Assert.False(Helper.IsSectionHeaderLevel(level, ns, out string _));
    }

    [Fact]
    public void Level_BoldTooLong_IsNotHeader() {
        string longText = new string('x', 200);
        var (level, ns) = Parse($"<level><content><p><b>{longText}</b></p></content></level>");
        Assert.False(Helper.IsSectionHeaderLevel(level, ns, out string _));
    }

    [Fact]
    public void Level_BoldIsLessThanHalf_IsNotHeader() {
        // Bold text 5 chars, paragraph 30+ chars total — bold is not predominant.
        var (level, ns) = Parse(
            "<level><content><p><b>Hello</b> and a long stretch of regular prose</p></content></level>");
        Assert.False(Helper.IsSectionHeaderLevel(level, ns, out string _));
    }

    // --- IsSectionHeaderParagraph ------------------------------------------

    [Fact]
    public void Paragraph_NumberedDirectBold_IsHeader() {
        var (para, ns) = Parse(
            "<paragraph><num><b>1.</b></num><content><p><b>Introduction</b></p></content></paragraph>");
        Assert.True(Helper.IsSectionHeaderParagraph(para, ns, out string heading));
        Assert.Equal("Introduction", heading);
    }

    [Fact]
    public void Paragraph_NumberedSpanWrappedBold_IsHeader() {
        // Both <num> and <p> have <b> wrapped in <span style="color:..."> — the
        // exact LEG-150 regression pattern.
        var (para, ns) = Parse(
            "<paragraph>" +
            "<num><span style=\"color:#000000\"><b>1.</b></span></num>" +
            "<content><p><span style=\"color:#000000\"><b>Introduction</b></span></p></content>" +
            "</paragraph>");
        Assert.True(Helper.IsSectionHeaderParagraph(para, ns, out string heading));
        Assert.Equal("Introduction", heading);
    }

    [Fact]
    public void Paragraph_LetteredNum_IsHeader() {
        var (para, ns) = Parse(
            "<paragraph><num><b>A.</b></num><content><p><b>First chapter</b></p></content></paragraph>");
        Assert.True(Helper.IsSectionHeaderParagraph(para, ns, out string heading));
        Assert.Equal("First chapter", heading);
    }

    [Fact]
    public void Paragraph_NumNotNumberedOrLettered_IsNotHeader() {
        // Non-conforming num text ("1.1", "(a)", etc.).
        var (para, ns) = Parse(
            "<paragraph><num><b>1.1</b></num><content><p><b>Sub-section</b></p></content></paragraph>");
        Assert.False(Helper.IsSectionHeaderParagraph(para, ns, out string _));
    }

    [Fact]
    public void Paragraph_NoNum_IsNotHeader() {
        var (para, ns) = Parse(
            "<paragraph><content><p><b>Heading without number</b></p></content></paragraph>");
        Assert.False(Helper.IsSectionHeaderParagraph(para, ns, out string _));
    }
}

}
