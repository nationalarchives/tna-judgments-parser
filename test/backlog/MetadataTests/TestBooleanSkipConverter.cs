#nullable enable

using Backlog.Csv;

using CsvHelper;
using CsvHelper.Configuration;

using Moq;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestBooleanSkipConverter
{
    private readonly BooleanSkipConverter converter = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("n")]
    [InlineData("N")]
    [InlineData("no")]
    [InlineData("No")]
    [InlineData("NO")]
    [InlineData("f")]
    [InlineData("F")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData(null)]
    public void ConvertFromString_WithFalsySkipValues_ReturnsFalse(string? skipValue)
    {
        var result = converter.ConvertFromString(skipValue, Mock.Of<IReaderRow>(), new MemberMapData(null));

        var skip = Assert.IsType<bool>(result);
        Assert.False(skip);
    }

    [Theory]
    [InlineData("skip")]
    [InlineData("Skip")]
    [InlineData("skip - for reasons")]
    [InlineData("Already in FCL")]
    [InlineData("Duplicate")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("T")]
    public void ConvertFromString_WithTruthySkipValues_ReturnsTrue(string skipValue)
    {
        var result = converter.ConvertFromString(skipValue, Mock.Of<IReaderRow>(), new MemberMapData(null));

        var skip = Assert.IsType<bool>(result);
        Assert.True(skip);
    }
}
