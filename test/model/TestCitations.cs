#nullable enable

using System;

using Shouldly;

using UK.Gov.Legislation.Judgments;

using Xunit;

namespace test.model;

public class TestCitations
{
    [Theory]
    [InlineData("[2020] UKSC 1", "[2020] UKSC 1")]
    [InlineData("[2020] uksc 01", "[2020] UKSC 1")]
    [InlineData("[2020] UKPC 5", "[2020] UKPC 5")]
    [InlineData("  [2020]   UKSC   1  ", "[2020] UKSC 1")]
    public void Normalize_UkscAndUkpc_ReturnsCanonicalForm(string cite, string expected)
    {
        Citations.Normalize(cite).ShouldBe(expected);
    }

    [Theory]
    [InlineData("[2020] EWCA CIV 5", "[2020] EWCA Civ 5")]
    [InlineData("[2020] EWCA crim 007", "[2020] EWCA Crim 7")]
    [InlineData("[2020[ EWCA Civ 7", "[2020] EWCA Civ 7")]
    [InlineData("[2020] EWCA 5 (Civ)", "[2020] EWCA Civ 5")]
    [InlineData("[2020] EWCA 5 Crim", "[2020] EWCA Crim 5")]
    public void Normalize_Ewca_ReturnsCanonicalForm(string cite, string expected)
    {
        Citations.Normalize(cite).ShouldBe(expected);
    }

    [Theory]
    [InlineData("[2020] EWHC 5 (Admin)", "[2020] EWHC 5 (Admin)")]
    [InlineData("2020 EWCH 05 Ch", "[2020] EWHC 5 (Ch)")]
    [InlineData("[2020] EHWC 5 (Comm)", "[2020] EWHC 5 (Comm)")]
    [InlineData("[2020] EWHC 5 (KB)", "[2020] EWHC 5 (KB)")]
    [InlineData("[2020] EWHC 05 (TCC)", "[2020] EWHC 5 (TCC)")]
    public void Normalize_Ewhc_ReturnsCanonicalForm(string cite, string expected)
    {
        Citations.Normalize(cite).ShouldBe(expected);
    }

    [Theory]
    [InlineData("[2020] EWFC 5", "[2020] EWFC 5")]
    [InlineData("[2020] ewcop 007", "[2020] EWCOP 7")]
    [InlineData("[2020] EWFC 5 (B)", "[2020] EWFC 5 (B)")]
    [InlineData("[2020] EWCOP 5 (T1)", "[2020] EWCOP 5 (T1)")]
    [InlineData("[2020] EWCC 5", "[2020] EWCC 5")]
    [InlineData("[2020] EWCR 5", "[2020] EWCR 5")]
    public void Normalize_FamilyAndCourtOfProtectionCourts_ReturnsCanonicalForm(string cite, string expected)
    {
        Citations.Normalize(cite).ShouldBe(expected);
    }

    [Theory]
    [InlineData("[2020] UKUT 5 (IAC)", "[2020] UKUT 5 (IAC)")]
    [InlineData("2020] UKUT 05(LC)", "[2020] UKUT 5 (LC)")]
    [InlineData("[2020] UKAIT 5", "[2020] UKAIT 5")]
    [InlineData("2020 UKAIT 5", "[2020] UKAIT 5")]
    [InlineData("[2020] EAT 5", "[2020] EAT 5")]
    [InlineData("2020 EAT 5", "[2020] EAT 5")]
    [InlineData("[2020] UKFTT 5 (TC)", "[2020] UKFTT 5 (TC)")]
    [InlineData("[2020] UKIPTrib 5", "[2020] UKIPTrib 5")]
    public void Normalize_TribunalCourts_ReturnsCanonicalForm(string cite, string expected)
    {
        Citations.Normalize(cite).ShouldBe(expected);
    }

    [Theory]
    [InlineData("not a citation")]
    [InlineData("[2020] MADEUP 5")]
    [InlineData("")]
    public void Normalize_UnrecognisedFormat_ReturnsNull(string cite)
    {
        Citations.Normalize(cite).ShouldBeNull();
    }

    [Fact]
    public void Normalize_NullCite_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Citations.Normalize(null));
    }

    [Fact]
    public void Normalize_NumberIsAllZeros_ReturnsNull()
    {
        // the leading-zero strip leaves an empty string, so the match is treated as non-authoritative
        Citations.Normalize("[2020] UKSC 0").ShouldBeNull();
    }

    [Theory]
    [InlineData("[2020] UKSC 1", "uksc/2020/1")]
    [InlineData("[2020] UKPC 5", "ukpc/2020/5")]
    [InlineData("[2020] EWCA Civ 1", "ewca/civ/2020/1")]
    [InlineData("[2020] EWCA Crim 1", "ewca/crim/2020/1")]
    [InlineData("[2020] EWHC 5 (Admin)", "ewhc/admin/2020/5")]
    [InlineData("[2020] EWFC 5", "ewfc/2020/5")]
    [InlineData("[2020] EWCOP 5", "ewcop/2020/5")]
    [InlineData("[2020] EWFC 5 (B)", "ewfc/b/2020/5")]
    [InlineData("[2020] EWCOP 5 (T1)", "ewcop/t1/2020/5")]
    [InlineData("[2020] EWCC 5", "ewcc/2020/5")]
    [InlineData("[2020] EWCR 5", "ewcr/2020/5")]
    [InlineData("[2020] UKUT 5 (IAC)", "ukut/iac/2020/5")]
    [InlineData("[2020] UKAIT 5", "ukait/2020/5")]
    [InlineData("[2020] EAT 5", "eat/2020/5")]
    [InlineData("[2020] UKFTT 5 (TC)", "ukftt/tc/2020/5")]
    [InlineData("[2020] UKIPTrib 5", "ukiptrib/2020/5")]
    public void MakeUriComponent_NormalizedCitation_ReturnsLowercaseUriPath(string normalized, string expected)
    {
        Citations.MakeUriComponent(normalized).ShouldBe(expected);
    }

    [Theory]
    [InlineData("not a citation")]
    [InlineData("[2020] uksc 1")] // MakeUriComponent expects an already-normalized (uppercase) citation
    [InlineData("")]
    public void MakeUriComponent_UnrecognisedOrDenormalizedInput_ReturnsNull(string normalized)
    {
        Citations.MakeUriComponent(normalized).ShouldBeNull();
    }

    [Fact]
    public void MakeUriComponent_NullInput_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Citations.MakeUriComponent(null));
    }

    [Theory]
    [InlineData("uksc/2020/1")]
    [InlineData("ewca/civ/2020/1")]
    [InlineData("ewcop/t1/2020/5")]
    [InlineData("ewca/civ/2020/1/press-summary/2")]
    public void IsValidUriComponent_WellFormedUri_ReturnsTrue(string uri)
    {
        Citations.IsValidUriComponent(uri).ShouldBeTrue();
    }

    [Theory]
    [InlineData("EWCA/civ/2020/1")] // uppercase is not accepted
    [InlineData("ewca/civ/2020")] // missing case number
    [InlineData("ewca//2020/1")] // empty subdivision segment
    [InlineData("2020/1")] // missing court
    [InlineData("")]
    public void IsValidUriComponent_MalformedUri_ReturnsFalse(string uri)
    {
        Citations.IsValidUriComponent(uri).ShouldBeFalse();
    }

    [Fact]
    public void IsValidUriComponent_NullUri_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Citations.IsValidUriComponent(null));
    }

    [Theory]
    [InlineData("uksc/2020/1", 2020)]
    [InlineData("ewca/civ/2020/1", 2020)]
    [InlineData("ewftt/tc/1999/42", 1999)]
    public void YearFromUriComponent_ValidUri_ReturnsYear(string uri, int expected)
    {
        Citations.YearFromUriComponent(uri).ShouldBe(expected);
    }

    [Fact]
    public void YearFromUriComponent_NullUri_ReturnsNull()
    {
        Citations.YearFromUriComponent(null).ShouldBeNull();
    }

    [Fact]
    public void YearFromUriComponent_UnrecognisedUri_Throws()
    {
        // the method assumes a matching uri and parses an empty capture group when it doesn't find one
        Should.Throw<FormatException>(() => Citations.YearFromUriComponent("not a uri"));
    }

    [Theory]
    [InlineData("uksc/2020/1", 1)]
    [InlineData("ewca/civ/2020/42", 42)]
    [InlineData("ewftt/tc/1999/007", 7)]
    public void NumberFromUriComponent_ValidUri_ReturnsNumber(string uri, int expected)
    {
        Citations.NumberFromUriComponent(uri).ShouldBe(expected);
    }

    [Fact]
    public void NumberFromUriComponent_NullUri_ReturnsNull()
    {
        Citations.NumberFromUriComponent(null).ShouldBeNull();
    }

    [Fact]
    public void NumberFromUriComponent_UnrecognisedUri_Throws()
    {
        Should.Throw<FormatException>(() => Citations.NumberFromUriComponent("not a uri"));
    }
}
