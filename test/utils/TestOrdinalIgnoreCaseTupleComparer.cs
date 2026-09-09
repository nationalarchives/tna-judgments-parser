using System;
using System.Collections.Generic;

using Shouldly;

using UK.Gov.Legislation.Judgments.Utils;

using Xunit;

namespace test.utils;

public class TestOrdinalIgnoreCaseTupleComparer
{
    private static readonly OrdinalIgnoreCaseTupleComparer Comparer = new();

    [Theory]
    [InlineData("Claimant", "Defendant", "Claimant", "Defendant")]
    [InlineData("Claimant", "Defendant", "CLAIMANT", "DEFENDANT")]
    [InlineData("Claimant", "Defendant", "claimant", "defendant")]
    [InlineData("Claimant", "Defendant", "ClAiMaNt", "DeFeNdAnT")]
    [InlineData("", "", "", "")]
    [InlineData(null, null, null, null)]
    public void Equals_ReturnsTrue_ForCaseInsensitiveMatches(string one1, string two1, string one2, string two2)
    {
        Comparer.Equals((one1, two1), (one2, two2)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Claimant", "Defendant", "Appellant", "Defendant")]
    [InlineData("Claimant", "Defendant", "Claimant", "Appellant")]
    [InlineData("Claimant", "Defendant", "Appellant", "Respondent")]
    [InlineData("Claimant", "Defendant", null, "Defendant")]
    [InlineData("Claimant", "Defendant", "Claimant", null)]
    [InlineData(null, "Defendant", "Claimant", "Defendant")]
    [InlineData("Claimant", "", "Claimant", "Defendant")]
    public void Equals_ReturnsFalse_ForDifferingValues(string one1, string two1, string one2, string two2)
    {
        Comparer.Equals((one1, two1), (one2, two2)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Claimant", "Defendant", "Claimant", "Defendant")]
    [InlineData("Claimant", "Defendant", "CLAIMANT", "DEFENDANT")]
    [InlineData("Claimant", "Defendant", "claimant", "defendant")]
    [InlineData("", "", "", "")]
    public void GetHashCode_MatchesForCaseInsensitiveEquivalents(string one1, string two1, string one2, string two2)
    {
        Comparer.GetHashCode((one1, two1)).ShouldBe(Comparer.GetHashCode((one2, two2)));
    }

    [Fact]
    public void GetHashCode_Throws_ForNullValues()
    {
        Should.Throw<ArgumentNullException>(() => Comparer.GetHashCode((null, null)));
    }

    [Fact]
    public void Dictionary_LooksUpValue_RegardlessOfCase()
    {
        var dictionary = new Dictionary<(string one, string two), int>(new OrdinalIgnoreCaseTupleComparer())
        {
            [("Claimant/", "Appellant")] = 1
        };

        dictionary.TryGetValue(("CLAIMANT/", "appellant"), out var value).ShouldBeTrue();
        value.ShouldBe(1);
    }

    [Fact]
    public void Dictionary_TreatsDifferentOrderingAsDistinctKeys()
    {
        var dictionary = new Dictionary<(string one, string two), int>(new OrdinalIgnoreCaseTupleComparer())
        {
            [("Claimant", "Defendant")] = 1
        };

        dictionary.ContainsKey(("Defendant", "Claimant")).ShouldBeFalse();
    }
}
