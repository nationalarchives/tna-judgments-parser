#nullable enable

using System;
using System.IO;
using System.Runtime.CompilerServices;

using Xunit;

namespace test;

/// <summary>
/// Helpers for the fixture-regeneration utilities (the RegenerateAllTestFiles / RegenerateAllHtml
/// "tests"), which rewrite the golden .akn/.html fixtures on disk rather than asserting anything.
/// </summary>
public static class TestFileUpdateHelpers
{
    /// <summary>
    /// Skips the calling regenerator unless UPDATE_XML=true. Without this guard a normal
    /// <c>dotnet test</c> run rewrites every fixture, which churns git and lets the comparison tests
    /// pass tautologically against output the parser has just produced. Mirrors the judgment
    /// <see cref="UpdateXmlFiles"/> utility.
    /// </summary>
    public static void SkipUnlessUpdatingFixtures([CallerMemberName] string caller = "")
    {
        var enabled = bool.TryParse(Environment.GetEnvironmentVariable("UPDATE_XML"), out var update) && update;
        Assert.SkipUnless(enabled,
            $"Not a test. To run it: dotnet test test/test.csproj --filter \"FullyQualifiedName~{caller}\" -e UPDATE_XML=\"true\"");
    }

    // Strips the non-deterministic metadata a leg .akn embeds (FRBRdate dates, ukm:Parser version,
    // uk:hash). Deliberately minimal: it strips strictly less than the per-type comparison XSLTs, so
    // the only-write-if-changed check never suppresses a real change, only date/version/hash noise.
    // (.html embeds none of this, so its regenerators need no equivalent.)
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

    /// <summary>
    /// Writes the regenerated leg .akn only when it differs from the file on disk after stripping
    /// non-deterministic metadata, so a deliberate regeneration produces a minimal, meaningful diff.
    /// Returns whether the file was written.
    /// </summary>
    public static bool WriteLegAknFixtureIfChanged(string path, string newXml)
    {
        if (File.Exists(path)
            && string.Equals(
                DocumentHelpers.RemoveNonDeterministicMetadata(newXml, LegNonDeterministicMetadataXslt),
                DocumentHelpers.RemoveNonDeterministicMetadata(File.ReadAllText(path), LegNonDeterministicMetadataXslt),
                StringComparison.Ordinal))
        {
            return false;
        }

        File.WriteAllText(path, newXml);
        return true;
    }
}
