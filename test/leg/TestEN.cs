using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Xsl;

using Xunit;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

using test;

namespace UK.Gov.Legislation.ExplanatoryNotes.Test {

public class TestEN {

    public static readonly IEnumerable<object[]> TestFiles = GetTestFiles();

    private static IEnumerable<object[]> GetTestFiles() {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        var regex = new Regex(@"^test\.leg\.en\.([^.]+)\.docx$");
        return resourceNames
            .Select(name => regex.Match(name))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .OrderBy(name => name)
            .Select(name => new object[] { name });
    }

    private XslCompiledTransform Transform = new XslCompiledTransform();

    public TestEN() {
        using var stringReader = new StringReader(xslt);
        using var xsltReader = XmlReader.Create(stringReader);
        Transform.Load(xsltReader);
    }

    [Theory]
    [MemberData(nameof(TestFiles))]
    public void Test(string filename) {
        var resourceName = $"test.leg.en.{filename}.docx";
        var docx = DocumentHelpers.ReadDocx(resourceName);

        var parsed = Helper.Parse(docx, filename + ".docx");
        DocumentHelpers.AssertValidMainAkn(parsed.Document);

        // Expected .akn is keyed by the canonical CSV filename, not the on-disk one.
        var normalizedName = UK.Gov.Legislation.Common.ENLegislationMapping.NormalizeFilename(filename);
        var expectedResourceName = $"test.leg.en.{normalizedName}.akn";
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        if (!assembly.GetManifestResourceNames().Contains(expectedResourceName))
            return;

        var actual = RemoveSomeMetadata(parsed.Serialize());
        var expected = RemoveSomeMetadata(DocumentHelpers.ReadXml(expectedResourceName));
        Assert.Equal(expected, actual);
    }

    private static string xslt = @"<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' xmlns:uk='https://legislation.gov.uk/akn' xmlns:ukm='http://www.legislation.gov.uk/namespaces/metadata'>
  <xsl:template match='akn:FRBRdate/@date'/>
  <xsl:template match='ukm:Parser'/>
  <xsl:template match='uk:hash/text()'/>
  <xsl:template match='ukm:DocumentMainType'/>
  <xsl:template match='ukm:Department'/>
  <xsl:template match='ukm:Date'/>
  <xsl:template match='ukm:Year'/>
  <xsl:template match='ukm:LegislationClass'/>
  <xsl:template match='@*|node()'>
    <xsl:copy>
      <xsl:apply-templates select='@*|node()'/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>";

    public string RemoveSomeMetadata(string akn) {
        using XmlReader reader = XmlReader.Create(new StringReader(akn));
        using StringWriter sWriter = new StringWriter();
        using XmlWriter xWriter = XmlWriter.Create(sWriter);
        Transform.Transform(reader, xWriter);
        return sWriter.ToString();
    }

    [Fact(Skip = "Manual regeneration only - remove Skip attribute to run")]
    public void RegenerateAllTestFiles() {
        var projectRoot = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."
        ));

        var failures = new List<string>();
        foreach (var testData in TestFiles) {
            string filename = (string)testData[0];
            try {
                var resourceName = $"test.leg.en.{filename}.docx";
                var docx = DocumentHelpers.ReadDocx(resourceName);
                var result = Helper.Parse(docx, filename + ".docx");
                var testFolder = Path.Combine(projectRoot, "test", "leg", "en");
                var normalizedName = UK.Gov.Legislation.Common.ENLegislationMapping.NormalizeFilename(filename);
                var outputPath = Path.Combine(testFolder, $"{normalizedName}.akn");
                File.WriteAllText(outputPath, result.Serialize());
                result.SaveImages(testFolder);
                Console.WriteLine($"Regenerated {normalizedName}.akn (from {filename}.docx)");
            } catch (Exception ex) {
                failures.Add($"{filename}: {ex.Message}");
                Console.WriteLine($"Skipped {filename}: {ex.Message}");
            }
        }
        if (failures.Count > 0) {
            Console.WriteLine($"\n{failures.Count} file(s) failed to regenerate:");
            foreach (var f in failures) Console.WriteLine($"  - {f}");
        }
    }

}

}
