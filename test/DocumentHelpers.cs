#nullable enable

using System;
using System.IO;
using System.Xml;
using System.Xml.Xsl;

namespace test;

public static class DocumentHelpers
{

    public static byte[] ReadDocx(int i)
    {
        return ReadDocx($"test.judgments.test{i}.docx");
    }

    public static byte[] ReadDocx(int i, string name)
    {
        return ReadDocx($"test.judgments.test{i}-{name}.docx");
    }

    public static byte[] ReadDocx(string resource)
    {
        using var stream = GetManifestResourceStream(resource);
        using StreamReader reader = new(stream);
        MemoryStream ms = new();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static Stream GetManifestResourceStream(string resource)
    {
        Stream? stream = null;
        try
        {
            var assembly = typeof(DocumentHelpers).Assembly;
            stream = assembly.GetManifestResourceStream(resource);
            return stream ?? throw new InvalidOperationException(
                $"Resource {resource} was not found in the assembly. Has it been included as an embedded resource?");
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    public static string ReadXml(int i)
    {
        return ReadXml($"test.judgments.test{i}.xml");
    }

    public static string ReadXml(string resource)
    {
        using var stream = GetManifestResourceStream(resource);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    public static string RemoveNonDeterministicMetadata(string akn, string? xslt = null)
    {
        const string defaultXslt = @"<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' xmlns:uk='https://caselaw.nationalarchives.gov.uk/akn'>
  <xsl:template match='akn:FRBRManifestation/akn:FRBRdate/@date'/>
  <xsl:template match='uk:parser/text()'/>
  <xsl:template match='@*|node()'>
    <xsl:copy>
      <xsl:apply-templates select='@*|node()'/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>";
        var transform = GetTransformer(xslt ?? defaultXslt);

        using var reader = XmlReader.Create(new StringReader(akn));
        using StringWriter sWriter = new();
        using var xWriter = XmlWriter.Create(sWriter);

        transform.Transform(reader, xWriter);
        return sWriter.ToString();
    }

    private static XslCompiledTransform GetTransformer(string xslt)
    {
        using StringReader stringReader = new(xslt);
        using var xsltReader = XmlReader.Create(stringReader);

        XslCompiledTransform transform = new();
        transform.Load(xsltReader);

        return transform;
    }
}
