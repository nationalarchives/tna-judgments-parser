using System;

using Backlog.Csv;

using CsvHelper;

using Xunit;

namespace NationalArchives.FindCaseLaw.Backlog.Tests.Csv;

public class TestBooleanSkipConverter
{
    private readonly BooleanSkipConverter converter = new();

    [Theory]
    [InlineData("")]
    [InlineData("\u00A0")]
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
    public void Read_WithFalsySkipValues_ReturnsFalse(string skipValue)
    {
        using var csv = CsvReader.FromText(skipValue, new CsvReaderOptions { HasHeader = false });
        csv.Read();

        var result = converter.Read(csv, 0);

        Assert.False(result);
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
    public void Read_WithTruthySkipValues_ReturnsTrue(string skipValue)
    {
        using var csv = CsvReader.FromText(skipValue, new CsvReaderOptions { HasHeader = false });
        csv.Read();

        var result = converter.Read(csv, 0);

        Assert.True(result);
    }

    [Fact]
    public void Write_Always_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() => converter.Write(null!, true));
    }
}
