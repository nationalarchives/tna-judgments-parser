using System;

using Xunit;

using test;

namespace UK.Gov.Legislation.ImpactAssessments.Test {

/// <summary>
/// Regression guard for the numbering crashes (LEG-160), using the real documents that
/// failed the first IA batch:
///   ukia_20170142 threw "unsupported numbering format: NumberFormatValues { }" in
///     Numbering2.FormatN (an empty/unknown level format), and
///   ukia_20130127 hit a NullReferenceException in Numbering2.TransformLevelNumber on a
///     level with no resolvable format.
/// Numbering resolution must now degrade to decimal rather than failing the document.
///
/// Asserted as "Numbering2 is no longer the crash point" rather than a full clean parse:
/// ukia_20130127 also carries the LEG-159 image-pipeline crash, which is fixed on its
/// own branch off main and so still surfaces here. The two fixes meet on integration.
/// </summary>
public class TestNumberingDegrade {

    [Theory]
    [InlineData("ukia_20170142_en")]  // empty/unknown numbering format -> FormatN
    [InlineData("ukia_20130127_en")]  // unresolvable level -> TransformLevelNumber NRE
    public void MalformedNumberingNoLongerCrashesInNumbering(string filename) {
        var docx = DocumentHelpers.ReadDocx($"test.leg.ia.{filename}.docx");
        var ex = Record.Exception(() => Helper.Parse(docx, filename + ".docx",
            renderer: UK.Gov.Legislation.Test.LocalRendererHelper.GetOrNull()));
        Assert.True(ex is null || !ex.ToString().Contains("Numbering2", StringComparison.Ordinal),
            $"numbering still crashed: {ex}");
    }
}

}
