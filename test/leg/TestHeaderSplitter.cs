using Xunit;

using test;

namespace UK.Gov.Legislation.Common.Test {

/// <summary>
/// Load-bearing assertion: EM-aggressive recovery (Start guess +
/// AfterRegulationTitle promotion) must live on <c>EMHeaderSplitter</c>
/// only, never on <see cref="BaseHeaderSplitter"/>. If someone moves it
/// back to the base, this test fails — pointing at the architectural
/// mistake rather than at the dozens of fixture diffs it produces.
/// </summary>
public class TestHeaderSplitter {

    [Theory]
    [InlineData("ukia_20250015_en")]
    [InlineData("ukia_20250009_en")]
    [InlineData("ukia_20250008_en")]
    public void ImpactAssessmentCoverSheetDoesNotProducePreface(string filename) {
        var docx = DocumentHelpers.ReadDocx($"test.leg.ia.original_filenames.{filename}.docx");
        var parsed = ImpactAssessments.Helper.Parse(
            docx, filename + ".docx",
            renderer: UK.Gov.Legislation.Test.LocalRendererHelper.GetOrNull());
        string xml = parsed.Serialize();
        Assert.DoesNotContain("<preface", xml);
    }

}

}
