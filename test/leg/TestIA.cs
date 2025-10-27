using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Xsl;

using Xunit;

using CaseLaw = UK.Gov.NationalArchives.CaseLaw;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.ImpactAssessments.Test {

public class TestIA {

    public static readonly IEnumerable<object[]> Indices = GetTestIndices();

    private static IEnumerable<object[]> GetTestIndices() {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        
        var regex = new Regex(@"^test\.leg\.ia\.test(\d+)\.docx$");
        return resourceNames
            .Select(name => regex.Match(name))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value))
            .Where(i => {
                // Check if both DOCX and AKN resources exist
                var docxResource = $"test.leg.ia.test{i}.docx";
                var aknResource = $"test.leg.ia.test{i}.akn";
                return resourceNames.Contains(docxResource) && resourceNames.Contains(aknResource);
            })
            .OrderBy(i => i)
            .Select(i => new object[] { i });
    }

    private XslCompiledTransform Transform = new XslCompiledTransform();

    public TestIA() {
        using var stringReader = new StringReader(xslt);
        using var xsltReader = XmlReader.Create(stringReader);
        Transform.Load(xsltReader);
    }

    [Theory]
    [MemberData(nameof(Indices))]
    public void Test(int i) {
        var docx = CaseLaw.Tests.ReadDocx($"test.leg.ia.test{i}.docx");
        var actual = Helper.Parse(docx).Serialize();
        var expected = CaseLaw.Tests.ReadXml($"test.leg.ia.test{i}.akn");
        actual = RemoveSomeMetadata(actual);
        expected = RemoveSomeMetadata(expected);
        Assert.Equal(expected, actual);
    }

    [Fact(Skip = "Manual regeneration only")]
    public void RegenerateAllTestFiles() {
        // Navigate from bin/Debug/net8.0 back to project root
        var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."
        ));
        
        foreach (var testData in Indices) {
            int i = (int)testData[0];
            var docx = CaseLaw.Tests.ReadDocx($"test.leg.ia.test{i}.docx");
            var akn = Helper.Parse(docx).Serialize();
            var outputPath = System.IO.Path.Combine(projectRoot, "test", "leg", "ia", $"test{i}.akn");
            System.IO.File.WriteAllText(outputPath, akn);
            System.Console.WriteLine($"Regenerated test{i}.akn");
        }
    }

    private static string xslt = @"<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' xmlns:uk='https://legislation.gov.uk/akn'>
  <xsl:template match='akn:FRBRdate/@date'/>
  <xsl:template match='uk:parser/text()'/>
  <xsl:template match='uk:hash/text()'/>
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
    [MemberData(nameof(Indices))]
    public void ValidateAkn(int i) {
        var aknXml = CaseLaw.Tests.ReadXml($"test.leg.ia.test{i}.akn");
        var doc = new XmlDocument();
        doc.LoadXml(aknXml);
        
        var validator = new Validator();
        var errors = validator.Validate(doc);
        
        if (errors.Count > 0) {
            var errorMessages = string.Join("\n", errors.Select(e => $"  - {e.Message}"));
            throw new Exception($"Validation failed for test{i}.akn with {errors.Count} error(s):\n{errorMessages}");
        }
    }

}

}
