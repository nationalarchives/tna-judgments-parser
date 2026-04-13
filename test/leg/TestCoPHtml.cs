using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

using Xunit;

using test;

namespace UK.Gov.Legislation.CodesOfPractice.Test {

/// <summary>
/// Golden-snapshot tests for akn2html.xsl output on parsed CoP fixtures.
/// Mirrors TestIAHtml / TestEMHtml / TestENHtml / TestTNHtml.
/// </summary>
public class TestCoPHtml {

    private const string ResourcePrefix = "test.leg.cop.";

    public static readonly IEnumerable<object[]> TestFiles = GetTestFiles();

    private static IEnumerable<object[]> GetTestFiles() {
        var assembly = Assembly.GetExecutingAssembly();
        var regex = new Regex(@"^test\.leg\.cop\.(.+cop_[0-9]+_en(?:_\d+)?)\.akn$");
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
            Assert.Skip($"Golden snapshot {filename}.html not present. Run TestCoPHtml.RegenerateAllHtml to create it.");

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
        string outputDir = Path.Combine(projectRoot, "test", "leg", "cop");

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

    private static string Normalize(string html) => html.Replace("\r\n", "\n").TrimEnd();

}

}
