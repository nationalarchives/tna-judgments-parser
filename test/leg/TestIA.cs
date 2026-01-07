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
        
        var actual = Helper.Parse(docx, filename + ".docx").Serialize();
        
        var expectedResourceName = $"test.leg.ia.original_filenames.{filename}.akn";
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        if (!assembly.GetManifestResourceNames().Contains(expectedResourceName)) {
            var doc = new XmlDocument();
            doc.LoadXml(actual);
            var validator = new Validator();
            var errors = validator.Validate(doc);
            Assert.Empty(errors);
            return;
        }
        
        var expected = DocumentHelpers.ReadXml(expectedResourceName);
        actual = RemoveSomeMetadata(actual);
        expected = RemoveSomeMetadata(expected);
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
            var akn = Helper.Parse(docx, filename + ".docx").Serialize();
            var outputPath = System.IO.Path.Combine(projectRoot, "test", "leg", "ia", "original filenames", $"{filename}.akn");
            System.IO.File.WriteAllText(outputPath, akn);
            System.Console.WriteLine($"Regenerated {filename}.akn");
        }
    }

    private static string xslt = @"<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' xmlns:uk='https://legislation.gov.uk/akn'>
  <xsl:template match='akn:FRBRdate/@date'/>
  <xsl:template match='uk:parser/text()'/>
  <xsl:template match='uk:hash/text()'/>
  <xsl:template match='uk:documentStage'/>
  <xsl:template match='uk:documentMainType'/>
  <xsl:template match='uk:department'/>
  <xsl:template match='uk:iaDate'/>
  <xsl:template match='uk:pdfDate'/>
  <xsl:template match='uk:legislationClass'/>
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

    [Theory]
    [MemberData(nameof(TestFiles))]
    public void ValidateParsedOutput(string filename) {
        var resourceName = $"test.leg.ia.original_filenames.{filename}.docx";
        var docx = DocumentHelpers.ReadDocx(resourceName);
        
        var akn = Helper.Parse(docx, filename + ".docx").Serialize();
        
        var doc = new XmlDocument();
        doc.LoadXml(akn);
        
        var validator = new Validator();
        var errors = validator.Validate(doc);
        
        if (errors.Count > 0) {
            var errorMessages = string.Join("\n", errors.Select(e => $"  - {e.Message}"));
            throw new Exception($"Validation failed for {filename} with {errors.Count} error(s):\n{errorMessages}");
        }
    }

}

}
