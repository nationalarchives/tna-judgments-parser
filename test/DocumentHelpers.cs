#nullable enable

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Xsl;

using Xunit;

namespace test;

public static class DocumentHelpers
{
    /// <summary>
    /// Assert that the parser's output is valid against the full OASIS Akoma Ntoso 3.0
    /// schema. Used by every leg test theory to enforce the AKN-validity invariant on
    /// every fixture (not just new ones lacking an expected .akn).
    /// </summary>
    public static void AssertValidMainAkn(XmlDocument akn)
    {
        Assert.Empty(UK.Gov.Legislation.Validator.Shared.ValidateAgainstMainAkn(akn));
    }

    // Guards the fixture regenerators (RegenerateAllTestFiles / RegenerateAllHtml): they rewrite the
    // golden fixtures on disk, so they only run when UPDATE_XML=true. Otherwise a normal test run
    // rewrites every fixture and the comparison tests pass tautologically against their own output.
    public static void SkipUnlessUpdatingFixtures([CallerMemberName] string caller = "")
    {
        var enabled = bool.TryParse(Environment.GetEnvironmentVariable("UPDATE_XML"), out var update) && update;
        Assert.SkipUnless(enabled,
            $"Not a test. To run it: dotnet test test/test.csproj --filter \"FullyQualifiedName~{caller}\" -e UPDATE_XML=\"true\"");
    }

    // Strips non-deterministic metadata from a leg .akn (FRBRdate dates, ukm:Parser version, uk:hash)
    // so a regeneration only diffs on real changes. (.html embeds none of this, so needs no equivalent.)
    private const string LegNonDeterministicMetadataXslt = @"<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' xmlns:ukm='http://www.legislation.gov.uk/namespaces/metadata' xmlns:uk='https://legislation.gov.uk/akn'>
  <xsl:template match='akn:FRBRdate/@date'/>
  <xsl:template match='ukm:Parser'/>
  <xsl:template match='uk:hash/text()'/>
  <xsl:template match='@*|node()'>
    <xsl:copy>
      <xsl:apply-templates select='@*|node()'/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>";

    // Writes the regenerated .akn only when it differs from disk after stripping non-deterministic
    // metadata, keeping deliberate regenerations to meaningful diffs. Returns whether it wrote.
    public static bool WriteLegAknFixtureIfChanged(string path, string newXml)
    {
        if (File.Exists(path)
            && string.Equals(
                RemoveNonDeterministicMetadata(newXml, LegNonDeterministicMetadataXslt),
                RemoveNonDeterministicMetadata(File.ReadAllText(path), LegNonDeterministicMetadataXslt),
                StringComparison.Ordinal))
        {
            return false;
        }

        File.WriteAllText(path, newXml);
        return true;
    }

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
        return GetEmbeddedResourceAsBytes(resource);
    }
    
    public static byte[] GetEmbeddedResourceAsBytes(string resource)
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
        return ReadEmbeddedResourceAsString(resource);
    }

    public static string ReadEmbeddedResourceAsString(string resource)
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
