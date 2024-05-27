
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Xsl;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.NationalArchives.CaseLaw {

public class Tests {

    private XslCompiledTransform Transform = new XslCompiledTransform();

    public Tests() {
        using var stringReader = new StringReader(xslt);
        using var xsltReader = XmlReader.Create(stringReader);
        Transform.Load(xsltReader);
    }

    static readonly int total = 83;

    public static readonly IEnumerable<object[]> indices =
        Enumerable.Concat(
            Enumerable.Range(1, 10),
        Enumerable.Concat(
            Enumerable.Range(12, 16),
            Enumerable.Range(29, total - 29 + 1)
        )
    ).Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(indices))]
    public void Test(int i) {
        var docx = ReadDocx(i);
        var actual = Api.Parser.Parse(new Api.Request(){ Content = docx }).Xml;
        var expected = ReadXml(i);
        // Assert.NotEqual(expected, actual);
        actual = RemoveSomeMetadata(actual);
        expected = RemoveSomeMetadata(expected);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(11,"order")]
    public void TestWithAttachment(int i, string name) {
        var main = ReadDocx(i, "main");
        var attach = ReadDocx(i, name);
        Api.AttachmentType type;
        switch (name) {
            case "order":
                type = Api.AttachmentType.Order;
                break;
            default:
                throw new System.Exception();
        }
        List<Api.Attachment> attachments = new List<Api.Attachment>(1) { new Api.Attachment() { Content = attach, Type = type } };
        var actual = Api.Parser.Parse(new Api.Request(){ Content = main, Attachments = attachments }).Xml;
        var expected = ReadXml(i);
        // Assert.NotEqual(expected, actual);
        actual = RemoveSomeMetadata(actual);
        expected = RemoveSomeMetadata(expected);
        Assert.Equal(expected, actual);
    }

    public static byte[] ReadDocx(int i) {
        var resource = $"test.judgments.test{i}.docx";
        return ReadDocx(resource);
    }
    public static byte[] ReadDocx(int i, string name) {
        var resource = $"test.judgments.test{i}-{name}.docx";
        return ReadDocx(resource);
    }
    internal static byte[] ReadDocx(string resource) {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream(resource);
        MemoryStream ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public static string ReadXml(int i) {
        var resource = $"test.judgments.test{i}.xml";
        return ReadXml(resource);
    }
    internal static string ReadXml(string resource) {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream(resource);
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string xslt = @"<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' xmlns:uk='https://caselaw.nationalarchives.gov.uk/akn'>
  <xsl:template match='akn:FRBRManifestation/akn:FRBRdate/@date'/>
  <xsl:template match='uk:parser/text()'/>
  <xsl:template match='@*|node()'>
    <xsl:copy>
      <xsl:apply-templates select='@*|node()'/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>";
//  <xsl:template match='@style'/>

    public string RemoveSomeMetadata(string akn) {
        // akn = new System.Text.RegularExpressions.Regex("text-indent ?:[-0-9a-z\\.]+ ?").Replace(akn, "");
        using XmlReader reader = XmlReader.Create(new StringReader(akn));
        using StringWriter sWriter = new StringWriter();
        using XmlWriter xWriter = XmlWriter.Create(sWriter);
        Transform.Transform(reader, xWriter);
        return sWriter.ToString();
    }

    private string Serialize(XmlDocument judgment) {
        using MemoryStream memStrm = new MemoryStream();
        AkN.Serializer.Serialize(judgment, memStrm);
        return System.Text.Encoding.UTF8.GetString(memStrm.ToArray());
    }

    [Theory]
    [InlineData(28)]
    public void TestWithImages(int i) {
        var docx = ReadDocx(i);
        Api.Response response = Api.Parser.Parse(new Api.Request(){ Content = docx });
        var actualXml = response.Xml;
        var expectedXml = ReadXml(i);
        actualXml = RemoveSomeMetadata(actualXml);
        expectedXml = RemoveSomeMetadata(expectedXml);
        Assert.Equal(expectedXml, actualXml);
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var actual in response.Images) {
            using Stream stream = assembly.GetManifestResourceStream($"test.judgments.test{ i }-{ actual.Name }");
            using MemoryStream ms = new MemoryStream();
            stream.CopyTo(ms);
            byte[] expected = ms.ToArray();
            Assert.Equal(expected, actual.Content);
        }
    }

}

}
