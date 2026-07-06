using Xunit;

namespace UK.Gov.Legislation.Common.Test {

/// <summary>
/// Direct tests of <see cref="RegulationNumber"/>. Each test row names
/// the production repro it came from (validator round 1 / 2 / 3 of the
/// EM batch). Add a new row whenever a real .docx surfaces a pattern
/// the existing rows don't cover.
/// </summary>
public class TestRegulationNumber {

    // ----- Normalize steps -----

    [Theory]
    [InlineData("201 6",       "2016")]              // intra-digit space
    [InlineData("9 7 8 0 1 1", "97 80 11")]          // pairs collapse, gaps between pairs remain
    [InlineData("2016",        "2016")]              // unchanged
    public void CollapseIntraDigitWhitespace(string input, string expected) =>
        Assert.Equal(expected, RegulationNumber.CollapseIntraDigitWhitespace(input));

    [Theory]
    [InlineData("S. R.",     "S.R.")]                // dotted initials collapse
    [InlineData("S.S. I.",   "S.S.I.")]              // chain
    [InlineData("R. No.",    "R. No.")]              // does NOT collapse (No. is a word, not an initial)
    [InlineData("S.R.",      "S.R.")]                // already-collapsed unchanged
    public void CollapseDottedInitials(string input, string expected) =>
        Assert.Equal(expected, RegulationNumber.CollapseDottedInitials(input));

    [Theory]
    [InlineData("N o. 48",   "No. 48")]              // round 3: nisrem_20160048_en
    [InlineData("N  o.  48", "No.  48")]             // multiple spaces
    [InlineData("No. 48",    "No. 48")]              // already correct
    [InlineData("Anno.",     "Anno.")]               // bare "no" inside a word — left alone
    public void RepairInterruptedNo(string input, string expected) =>
        Assert.Equal(expected, RegulationNumber.RepairInterruptedNo(input));

    [Theory]
    [InlineData("S.R.  2016   No.  48", "S.R. 2016 No. 48")]
    [InlineData("  trim me  ",          "trim me")]
    public void CollapseMultipleSpaces(string input, string expected) =>
        Assert.Equal(expected, RegulationNumber.CollapseMultipleSpaces(input));

    // ----- end-to-end Normalize -----

    [Theory]
    [InlineData("S.R. 201 6   No. xxx",  "S.R. 2016 No. xxx")]  // round 1: nidsrem_9780338003737
    [InlineData("S. R. 2016 No. 36",     "S.R. 2016 No. 36")]   // round 1: nisrem_20160036
    [InlineData("2016  SR  N o.  48",    "2016 SR No. 48")]     // round 3: nisrem_20160048
    [InlineData("S.R. No. 73",           "S.R. No. 73")]        // round 3: nisrem_20160073 (no over-collapse)
    public void Normalize_(string input, string expected) =>
        Assert.Equal(expected, RegulationNumber.Normalize(input));

    // ----- Is: real (URI-generating) numbers -----

    [Theory]
    [InlineData("2013 No. 2911")]                    // standard UK SI
    [InlineData("2013 No.1473")]                     // round 1: uksiem_20131473 (no space after dot)
    [InlineData("2013 No. 1571 L. 17)")]             // round 2: uksiem_20131571 (missing open paren)
    [InlineData("2013 No. 100 (L. 5)")]              // standard with L. clause
    [InlineData("S.R. 2016 No. 32")]                 // standard NI SR
    [InlineData("S. R. 2016 No. 36")]                // round 1: nisrem_20160036
    [InlineData("2016 SR No. 48")]                   // round 3: nisrem_20160048 (year-first)
    [InlineData("2016 NO. 34")]                      // round 2: nisrem_20160034 (uppercase NO.)
    [InlineData("S.S.I. 2014/123")]                  // SSI
    public void Is_RealNumber(string input) =>
        Assert.True(RegulationNumber.Is(input), $"expected match for: {input}");

    // ----- Is: unassigned (draft) numbers -----

    [Theory]
    [InlineData("2013 No. [XXXX]")]                  // round 1: ukdsiem_9780111100240
    [InlineData("2013 No. [DRAFT]")]                 // round 1: ukdsiem_9780111540145
    [InlineData("2013 No. ")]                        // round 1: ukdsiem_9780111100325 (blank number)
    [InlineData("S.R. 2016 No. [XXXX]")]
    [InlineData("S.R. 201 6 No.xxx")]                // round 1: nidsrem_9780338003737 (no space after dot)
    [InlineData("2014 [XXXX]")]                      // round 2: ukdsiem_9780111112960 (no "No.")
    [InlineData("2018 No. ****")]                    // round 3: ukdsiem_9780111160817 (asterisks)
    [InlineData("2013 No. [1751")]                   // round 3: uksiem_20131751 (broken bracket)
    [InlineData("2016 NO [XXXX]")]                   // round 3: nidsrem_9780338004642 (no period after NO)
    [InlineData("S.R. No. 73")]                      // round 3: nisrem_20160073 (no year)
    [InlineData("SR No. 76")]                        // round 3: nisrem_20160076 (no dots, no year)
    [InlineData("No. ")]                             // paired EM placeholder
    public void Is_UnassignedNumber(string input) =>
        Assert.True(RegulationNumber.Is(input), $"expected match for {input}");

    // ----- Is: should NOT match (negative cases) -----

    [Theory]
    [InlineData("")]
    [InlineData("Hello world")]
    [InlineData("This explanatory memorandum has been prepared")]
    [InlineData("Section 1")]
    [InlineData("Introduction")]
    public void Is_Negative(string input) =>
        Assert.False(RegulationNumber.Is(input), $"unexpected match for {input}");

    // ----- MakeURI -----

    [Theory]
    [InlineData("2013 No. 2911",     "uksi/2013/2911")]
    [InlineData("2013 No.1473",      "uksi/2013/1473")]   // tolerates no space after dot
    [InlineData("S.R. 2016 No. 32",  "nisr/2016/32")]
    [InlineData("S. R. 2016 No. 36", "nisr/2016/36")]     // tolerates space between initials
    [InlineData("2016 SR No. 48",    "nisr/2016/48")]     // year-first NI SR
    [InlineData("S.S.I. 2014/123",   "ssi/2014/123")]
    public void MakeURI_Real(string input, string expected) =>
        Assert.Equal(expected, RegulationNumber.MakeURI(input));

    [Theory]
    [InlineData("2013 No. [XXXX]")]                    // drafts have no URI
    [InlineData("S.R. No. 73")]                        // year-less, no URI
    public void MakeURI_NullForUnassigned(string input) =>
        Assert.Null(RegulationNumber.MakeURI(input));

}

}
