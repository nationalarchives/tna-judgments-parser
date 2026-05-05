using System.IO;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Xunit;

using DocxStyles = UK.Gov.Legislation.Judgments.DOCX.Styles;
using OoxmlStyles = DocumentFormat.OpenXml.Wordprocessing.Styles;

namespace UK.Gov.Legislation.DOCX.Test {

/// <summary>
/// Unit tests for UK.Gov.Legislation.Judgments.DOCX.Styles.ClassifyHeading. Covers the three
/// signal tiers (outlineLvl authoritative, Heading\d name authoritative,
/// visual bold + size) plus inheritance via the basedOn chain.
/// </summary>
public class TestClassifyHeading {

    private static (MemoryStream stream, MainDocumentPart main) BuildDoc(params Style[] styles) {
        var ms = new MemoryStream();
        var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document(new Body());
        var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
        var stylesEl = new OoxmlStyles();
        foreach (var s in styles) stylesEl.AppendChild(s);
        stylesPart.Styles = stylesEl;
        return (ms, main);
    }

    private static Style ParaStyle(string id, params OpenXmlElement[] children) {
        var s = new Style { Type = StyleValues.Paragraph, StyleId = id };
        s.AppendChild(new StyleName { Val = id });
        foreach (var c in children) s.AppendChild(c);
        return s;
    }

    [Fact]
    public void OutlineLvl_IsAuthoritative_AndConvertsToOneBased() {
        var s = ParaStyle("MyHeading",
            new StyleParagraphProperties(new OutlineLevel { Val = 0 }));
        var (ms, main) = BuildDoc(s);
        using (ms) {
            var c = DocxStyles.ClassifyHeading(main, "MyHeading");
            Assert.NotNull(c);
            Assert.Equal(1, c.Value.Depth);
            Assert.Equal(DocxStyles.HeadingSignal.Authoritative, c.Value.Signal);
        }
    }

    [Fact]
    public void OutlineLvl_DeepLevel_MapsTo6() {
        var s = ParaStyle("Sub6",
            new StyleParagraphProperties(new OutlineLevel { Val = 5 }));
        var (ms, main) = BuildDoc(s);
        using (ms) {
            var c = DocxStyles.ClassifyHeading(main, "Sub6");
            Assert.Equal(6, c.Value.Depth);
            Assert.Equal(DocxStyles.HeadingSignal.Authoritative, c.Value.Signal);
        }
    }

    [Fact]
    public void OutlineLvl_OutOfRange_FallsThrough() {
        // Word uses outlineLvl=9 for body text; should not be classified.
        var s = ParaStyle("Body",
            new StyleParagraphProperties(new OutlineLevel { Val = 9 }));
        var (ms, main) = BuildDoc(s);
        using (ms) {
            Assert.Null(DocxStyles.ClassifyHeading(main, "Body"));
        }
    }

    [Fact]
    public void HeadingNamePattern_IsAuthoritative() {
        var s = ParaStyle("Heading2");
        var (ms, main) = BuildDoc(s);
        using (ms) {
            var c = DocxStyles.ClassifyHeading(main, "Heading2");
            Assert.Equal(2, c.Value.Depth);
            Assert.Equal(DocxStyles.HeadingSignal.Authoritative, c.Value.Signal);
        }
    }

    [Fact]
    public void VisualClassification_BoldPlusLargeSize() {
        // 36 half-points = 18pt; with bold should be visual depth 1.
        var s = ParaStyle("BigBold",
            new StyleRunProperties(
                new Bold(),
                new FontSize { Val = "36" }));
        var (ms, main) = BuildDoc(s);
        using (ms) {
            var c = DocxStyles.ClassifyHeading(main, "BigBold");
            Assert.Equal(1, c.Value.Depth);
            Assert.Equal(DocxStyles.HeadingSignal.Visual, c.Value.Signal);
        }
    }

    [Fact]
    public void VisualClassification_BoldPlusMidSize_DepthTwo() {
        var s = ParaStyle("MidBold",
            new StyleRunProperties(
                new Bold(),
                new FontSize { Val = "30" }));
        var (ms, main) = BuildDoc(s);
        using (ms) {
            var c = DocxStyles.ClassifyHeading(main, "MidBold");
            Assert.Equal(2, c.Value.Depth);
            Assert.Equal(DocxStyles.HeadingSignal.Visual, c.Value.Signal);
        }
    }

    [Fact]
    public void VisualClassification_BelowSizeThreshold_NotHeading() {
        // 24 half-points = 12pt — below 26 threshold.
        var s = ParaStyle("BodyBold",
            new StyleRunProperties(
                new Bold(),
                new FontSize { Val = "24" }));
        var (ms, main) = BuildDoc(s);
        using (ms) {
            Assert.Null(DocxStyles.ClassifyHeading(main, "BodyBold"));
        }
    }

    [Fact]
    public void VisualClassification_NoBold_NotHeading() {
        var s = ParaStyle("BigPlain",
            new StyleRunProperties(new FontSize { Val = "36" }));
        var (ms, main) = BuildDoc(s);
        using (ms) {
            Assert.Null(DocxStyles.ClassifyHeading(main, "BigPlain"));
        }
    }

    [Fact]
    public void OutlineLvl_InheritedFromBasedOn() {
        var parent = ParaStyle("Parent",
            new StyleParagraphProperties(new OutlineLevel { Val = 1 }));
        var child = ParaStyle("Child", new BasedOn { Val = "Parent" });
        var (ms, main) = BuildDoc(parent, child);
        using (ms) {
            var c = DocxStyles.ClassifyHeading(main, "Child");
            Assert.Equal(2, c.Value.Depth);
            Assert.Equal(DocxStyles.HeadingSignal.Authoritative, c.Value.Signal);
        }
    }

    [Fact]
    public void OutlineLvl_PreferredOverHeadingNamePattern() {
        // "Heading7" name would be out of range (only 1-6 accepted), but
        // outlineLvl=2 wins. Confirms tier ordering.
        var s = ParaStyle("Heading7",
            new StyleParagraphProperties(new OutlineLevel { Val = 2 }));
        var (ms, main) = BuildDoc(s);
        using (ms) {
            var c = DocxStyles.ClassifyHeading(main, "Heading7");
            Assert.Equal(3, c.Value.Depth);
            Assert.Equal(DocxStyles.HeadingSignal.Authoritative, c.Value.Signal);
        }
    }

    [Fact]
    public void UnknownStyle_ReturnsNull() {
        var (ms, main) = BuildDoc();
        using (ms) {
            Assert.Null(DocxStyles.ClassifyHeading(main, "DoesNotExist"));
        }
    }

    [Fact]
    public void EmptyStyleId_ReturnsNull() {
        var (ms, main) = BuildDoc();
        using (ms) {
            Assert.Null(DocxStyles.ClassifyHeading(main, ""));
            Assert.Null(DocxStyles.ClassifyHeading(main, null));
        }
    }
}

}
