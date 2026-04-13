using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

using Xunit;

using test;

namespace UK.Gov.Legislation.ImpactAssessments.Test {

/// <summary>
/// Golden-snapshot tests for the akn2html.xsl output on parsed IA fixtures.
/// Each fixture has an <c>{name}.html</c> embedded resource stored alongside
/// its <c>{name}.akn</c>; the test regenerates HTML from the AKN and compares.
///
/// These tests need Oxygen-bundled Saxon on the machine (via OXYGEN_HOME or
/// the default install path) because akn2html.xsl is XSLT 2.0. They skip
/// cleanly when HtmlBuilder is unavailable so CI without Oxygen still runs.
/// </summary>
public class TestIAHtml {

    private const string ResourcePrefix = "test.leg.ia.original_filenames.";

    public static readonly IEnumerable<object[]> TestFiles = GetTestFiles();

    private static IEnumerable<object[]> GetTestFiles() {
        var assembly = Assembly.GetExecutingAssembly();
        var regex = new Regex(@"^test\.leg\.ia\.original_filenames\.(ukia_\d+_en)\.akn$");
        return assembly.GetManifestResourceNames()
            .Select(name => regex.Match(name))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .OrderBy(name => name)
            .Select(name => new object[] { name });
    }

    [Theory]
    [MemberData(nameof(TestFiles))]
    public void HtmlSnapshot(string filename) {
        if (!HtmlBuilder.IsAvailable())
            Assert.Skip("HtmlBuilder unavailable (Oxygen/Saxon not installed). Set OXYGEN_HOME to enable.");

        var assembly = Assembly.GetExecutingAssembly();
        string expectedResource = $"{ResourcePrefix}{filename}.html";
        if (!assembly.GetManifestResourceNames().Contains(expectedResource))
            Assert.Skip($"Golden snapshot {filename}.html not present. Run TestIAHtml.RegenerateAllHtml to create it.");

        string aknText = DocumentHelpers.ReadXml($"{ResourcePrefix}{filename}.akn");
        var akn = new XmlDocument();
        akn.LoadXml(aknText);

        string actual = Normalize(HtmlBuilder.Build(akn));
        string expected = Normalize(DocumentHelpers.ReadXml(expectedResource));

        Assert.Equal(expected, actual);
    }

    [Fact(Skip = "Manual regeneration only - remove Skip attribute to run")]
    public void RegenerateAllHtml() {
        if (!HtmlBuilder.IsAvailable())
            Assert.Skip("HtmlBuilder unavailable - install Oxygen or set OXYGEN_HOME.");

        string projectRoot = Path.GetFullPath(Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
        string outputDir = Path.Combine(projectRoot, "test", "leg", "ia", "original filenames");

        foreach (var testData in TestFiles) {
            string filename = (string)testData[0];
            string aknText = DocumentHelpers.ReadXml($"{ResourcePrefix}{filename}.akn");
            var akn = new XmlDocument();
            akn.LoadXml(aknText);

            string html = HtmlBuilder.Build(akn);
            string outputPath = Path.Combine(outputDir, $"{filename}.html");
            File.WriteAllText(outputPath, html);
            System.Console.WriteLine($"Regenerated {filename}.html");
        }
    }

    /// <summary>
    /// Normalise line endings so snapshots are stable across OS/git settings.
    /// </summary>
    private static string Normalize(string html) => html.Replace("\r\n", "\n").TrimEnd();

}

}
