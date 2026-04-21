using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Xsl;

using Xunit;

using test;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.ImpactAssessments.Test {

public class TestIA {

    public static readonly IEnumerable<object[]> TestFiles = GetTestFiles();

    /// <summary>
    /// Gets test files with ukia_YYYYNNNN_en.docx naming pattern
    /// </summary>
    private static IEnumerable<object[]> GetTestFiles() {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        
        // Match pattern: test.leg.ia.original_filenames.ukia_20250001_en.docx
        var regex = new Regex(@"^test\.leg\.ia\.original_filenames\.(ukia_\d+_en)\.docx$");
        return resourceNames
            .Select(name => regex.Match(name))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value) // e.g., ukia_20250001_en
            .OrderBy(name => name)
            .Select(name => new object[] { name });
    }

    private XslCompiledTransform Transform = new XslCompiledTransform();

    public TestIA() {
        using var stringReader = new StringReader(xslt);
        using var xsltReader = XmlReader.Create(stringReader);
        Transform.Load(xsltReader);
    }

    [Theory]
    [MemberData(nameof(TestFiles))]
    public void Test(string filename) {
        var resourceName = $"test.leg.ia.original_filenames.{filename}.docx";
        var docx = DocumentHelpers.ReadDocx(resourceName);

        var parsed = Helper.Parse(docx, filename + ".docx", renderer: UK.Gov.Legislation.Test.LocalRendererHelper.GetOrNull());
        DocumentHelpers.AssertValidMainAkn(parsed.Document);

        var expectedResourceName = $"test.leg.ia.original_filenames.{filename}.akn";
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        if (!assembly.GetManifestResourceNames().Contains(expectedResourceName))
            return;

        var actual = RemoveSomeMetadata(parsed.Serialize());
        var expected = RemoveSomeMetadata(DocumentHelpers.ReadXml(expectedResourceName));
        Assert.Equal(expected, actual);
    }

    [Fact(Skip = "Manual regeneration only - remove Skip attribute to run")]
    public void RegenerateAllTestFiles() {
        var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."
        ));
        
        foreach (var testData in TestFiles) {
            string filename = (string)testData[0];
            var resourceName = $"test.leg.ia.original_filenames.{filename}.docx";
            var docx = DocumentHelpers.ReadDocx(resourceName);
            var result = Helper.Parse(docx, filename + ".docx", renderer: UK.Gov.Legislation.Test.LocalRendererHelper.GetOrNull());
            var testFolder = System.IO.Path.Combine(projectRoot, "test", "leg", "ia", "original filenames");
            var outputPath = System.IO.Path.Combine(testFolder, $"{filename}.akn");
            System.IO.File.WriteAllText(outputPath, result.Serialize());
            result.SaveImages(testFolder);
            System.Console.WriteLine($"Regenerated {filename}.akn");
        }
    }

    private static string xslt = @"<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' xmlns:uk='https://legislation.gov.uk/akn' xmlns:ukm='http://www.legislation.gov.uk/namespaces/metadata'>
  <xsl:template match='akn:FRBRdate/@date'/>
  <xsl:template match='ukm:Parser'/>
  <xsl:template match='uk:hash/text()'/>
  <xsl:template match='ukm:DocumentStage'/>
  <xsl:template match='ukm:DocumentMainType'/>
  <xsl:template match='ukm:Department'/>
  <xsl:template match='ukm:Date'/>
  <xsl:template match='ukm:Year'/>
  <xsl:template match='ukm:Number'/>
  <xsl:template match='ukm:PdfDate'/>
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

}

}
