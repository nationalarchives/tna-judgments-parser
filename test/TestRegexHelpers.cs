#nullable enable

using System;

using UK.Gov.Legislation.Judgments;

using Xunit;

namespace test;

public class TestRegexHelpers
{
    [Fact]
    public void GetFirstMatch_WithNoPatterns_ShouldThrowArgumentOutOfRangeException()
    {
        var action = () => RegexHelpers.GetFirstMatch("my input");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal("orderedRegexPatterns", exception.ParamName);
    }

    [Fact]
    public void GetFirstMatch_WithNoMatches_ShouldReturnResultForLastMatchTried()
    {
        var actual = RegexHelpers.GetFirstMatch("my input",
            "my first regex pattern that doesn't match anything",
            "my second regex pattern that doesn't match anything");

        Assert.False(actual.Success);
    }

    [Theory]
    [InlineData("y", "m(y)", "in(put)")]
    [InlineData("in", "no match m(y)", @"\s+([a-z]{2})", "in(put)")]
    [InlineData("put", "no match m(y)", "not a match either", "in(put)")]
    public void GetFirstMatch_WithMatches_ShouldReturnResultForFirstSuccessfulMatch(string expectedGroupCapture, params string[] orderedRegexPatterns)
    {
        var actual = RegexHelpers.GetFirstMatch("my input", orderedRegexPatterns);

        Assert.True(actual.Success);
        Assert.Equal(expectedGroupCapture, actual.Groups[1].Value);
    }
}
