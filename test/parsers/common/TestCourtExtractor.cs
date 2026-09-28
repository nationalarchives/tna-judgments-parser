#nullable enable

namespace test.parsers.common;

using System;

using Microsoft.Extensions.Time.Testing;

using Shouldly;

using UK.Gov.Legislation.Judgments.Parse;

using Xunit;


public class TestCourtExtractor
{
    readonly FakeTimeProvider fakeTimeProvider;

    readonly CourtExtractor courtExtractor;

    public TestCourtExtractor()
    {
        fakeTimeProvider = new FakeTimeProvider();
        courtExtractor = new CourtExtractor(fakeTimeProvider);
    }

    [Theory]
    [InlineData("[2026] EWHC 2168 (Comm)", 2026, 10, 01, "EWHC-BPD-Commercial")]
    [InlineData("[2027] EWHC 2168 (Comm)", 2027, 01, 01, "EWHC-BPD-Commercial")]
    [InlineData("[2026] EWHC 2168 (Comm)", 2026, 09, 30, "EWHC-KBD-Commercial")]
    [InlineData("[2021] EWHC 2168 (Comm)", 2021, 12, 01, "EWHC-QBD-Commercial")]
    [InlineData("[2026] EWHC 349 (IPEC)", 2026, 10, 01, "EWHC-BPD-IPEC")]
    [InlineData("[2026] EWHC 349 (IPEC)", 2026, 09, 30, "EWHC-Chancery-IPEC")]
    [InlineData("[2026] EWHC 2142 (Ch)", 2026, 10, 01, "EWHC-Chancery")]
    [InlineData("[2026] EWHC 2142 (Ch)", 2026, 09, 30, "EWHC-Chancery")]
    [InlineData("[2026] EWHC 2142 (BP)", 2026, 10, 01, "EWHC-BPD")]
    [InlineData("[2026] EWHC 2142 (BP)", 2026, 09, 30, "EWHC-BPD")]
    [InlineData("[2026] UKFTT 1104 (HESC)", 2026, 10, 01, "UKFTT-HESC")]
    public void FromCitationAndDate_WithDecisionDate_ReturnsExpectedCourtCode(string citation, int year, int month, int day, string expectedCourtCode)
    {
        var decisionDate = new DateOnly(year, month, day);

        var result = courtExtractor.FromCitationAndDate(citation, decisionDate);

        result.ShouldNotBeNull();
        result.Code.ShouldBe(expectedCourtCode);
    }


    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not a citation")]
    public void FromCitationAndDate_WithInvalidCitation_ReturnsNull(string citation)
    {
        var decisionDate = new DateOnly(2026, 01, 01);

        var result = courtExtractor.FromCitationAndDate(citation, decisionDate);

        result.ShouldBeNull();
    }


    [Theory]
    [InlineData("[2026] EWHC 2168 (Comm)", 2026, 10, 01, "EWHC-BPD-Commercial")]
    [InlineData("[2027] EWHC 2168 (Comm)", 2027, 01, 01, "EWHC-BPD-Commercial")]
    [InlineData("[2026] EWHC 2168 (Comm)", 2026, 09, 30, "EWHC-KBD-Commercial")]
    [InlineData("[2021] EWHC 2168 (Comm)", 2021, 12, 01, "EWHC-QBD-Commercial")]
    [InlineData("[2026] EWHC 349 (IPEC)", 2026, 10, 01, "EWHC-BPD-IPEC")]
    [InlineData("[2026] EWHC 349 (IPEC)", 2026, 09, 30, "EWHC-Chancery-IPEC")]
    [InlineData("[2026] EWHC 2142 (Ch)", 2026, 10, 01, "EWHC-Chancery")]
    [InlineData("[2026] EWHC 2142 (Ch)", 2026, 09, 30, "EWHC-Chancery")]
    [InlineData("[2026] EWHC 2142 (BP)", 2026, 10, 01, "EWHC-BPD")]
    [InlineData("[2026] EWHC 2142 (BP)", 2026, 09, 30, "EWHC-BPD")]
    [InlineData("[2026] UKFTT 1104 (HESC)", 2026, 10, 01, "UKFTT-HESC")]
    public void FromCitationAndDate_NoDecisionDate_ReturnsExpectedCourtCode(string citation, int year, int month, int day, string expectedCourtCode)
    {
        var now = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
        fakeTimeProvider.AdjustTime(now);

        var result = courtExtractor.FromCitationAndDate(citation, null);

        result.ShouldNotBeNull();
        result.Code.ShouldBe(expectedCourtCode);
    }
}
