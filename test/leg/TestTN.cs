using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Xsl;

using test;

using Xunit;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.TranspositionNotes.Test {

public class TestTN {

    public static readonly IEnumerable<object[]> TestFiles = GetTestFiles();

    /// <summary>
    /// Gets test files with [prefix]tn_YYYYNNNN_en.docx naming pattern.
    /// Transposition Notes are not versioned.
    /// </summary>
    private static IEnumerable<object[]> GetTestFiles() {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        var regex = new Regex(@"^test\.leg\.tn\.(.+tn_\d+_en)\.docx$");
        return resourceNames
            .Select(name => regex.Match(name))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .OrderBy(name => name)
            .Select(name => new object[] { name });
    }

    private XslCompiledTransform Transform = new XslCompiledTransform();

    public TestTN() {
        using var stringReader = new StringReader(xslt);
        using var xsltReader = XmlReader.Create(stringReader);
        Transform.Load(xsltReader);
    }

    [Theory]
    [MemberData(nameof(TestFiles))]
    public void Test(string filename) {
        var resourceName = $"test.leg.tn.{filename}.docx";
        var docx = DocumentHelpers.ReadDocx(resourceName);

        var actual = Helper.Parse(docx, filename + ".docx").Serialize();

        var expectedResourceName = $"test.leg.tn.{filename}.akn";
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
            try {
                var resourceName = $"test.leg.tn.{filename}.docx";
                var docx = DocumentHelpers.ReadDocx(resourceName);
                var result = Helper.Parse(docx, filename + ".docx");
                var testFolder = System.IO.Path.Combine(projectRoot, "test", "leg", "tn");
                var outputPath = System.IO.Path.Combine(testFolder, $"{filename}.akn");
                System.IO.File.WriteAllText(outputPath, result.Serialize());
                try { result.SaveImages(testFolder); }
                catch (Exception ex) { System.Console.WriteLine($"  SaveImages failed for {filename}: {ex.Message}"); }
                System.Console.WriteLine($"Regenerated {filename}.akn");
            } catch (Exception ex) {
                System.Console.WriteLine($"FAILED to regenerate {filename}: {ex.GetType().Name}: {ex.Message}");
            }
        }
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

}

}
