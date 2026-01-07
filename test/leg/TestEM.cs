
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Xsl;

using test;

using Xunit;

namespace UK.Gov.Legislation.ExplanatoryMemoranda.Test {

public class TestEM {

    private static readonly int N = 9;

    public static readonly IEnumerable<object[]> Indices = Enumerable.Range(1, N)
        .Select(i => new object[] { i });

    private XslCompiledTransform Transform = new XslCompiledTransform();

    public TestEM() {
        using var stringReader = new StringReader(xslt);
        using var xsltReader = XmlReader.Create(stringReader);
        Transform.Load(xsltReader);
    }

    [Theory]
    [MemberData(nameof(Indices))]
    public void Test(int i) {
        var docx = DocumentHelpers.ReadDocx($"test.leg.em.test{i}.docx");
        var actual = Helper.Parse(docx).Serialize();
        var expected = DocumentHelpers.ReadXml($"test.leg.em.test{i}.akn");
        actual = RemoveSomeMetadata(actual);
        expected = RemoveSomeMetadata(expected);
        Assert.Equal(expected, actual);
    }

    private static string xslt = @"<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' xmlns:uk='https://legislation.gov.uk/akn'>
  <xsl:template match='akn:FRBRdate/@date'/>
  <xsl:template match='uk:parser/text()'/>
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

    [Fact]
    public void RegenerateAllEMTestFiles() {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var assemblyDir = System.IO.Path.GetDirectoryName(assembly.Location);
        var projectRoot = assemblyDir;
        
        // Look for either .sln file or .git directory to find project root
        while (projectRoot != null && 
               !System.IO.File.Exists(System.IO.Path.Combine(projectRoot, "tna-judgments-parser.sln")) &&
               !System.IO.Directory.Exists(System.IO.Path.Combine(projectRoot, ".git"))) {
            projectRoot = System.IO.Directory.GetParent(projectRoot)?.FullName;
        }
        
        if (projectRoot == null) {
            throw new System.Exception("Could not find project root");
        }
        
        foreach (var testData in Indices) {
            int i = (int)testData[0];
            var docx = DocumentHelpers.ReadDocx($"test.leg.em.test{i}.docx");
            var akn = Helper.Parse(docx).Serialize();
            var outputPath = System.IO.Path.Combine(projectRoot, "test", "leg", "em", $"test{i}.akn");
            System.IO.File.WriteAllText(outputPath, akn);
            System.Console.WriteLine($"Regenerated test{i}.akn");
        }
    }

}

}
