using System;

using Backlog.Csv;

using CsvHelper;

using Xunit;

namespace NationalArchives.FindCaseLaw.Backlog.Tests.Csv;

public class TestDecisionDateConverter
{
    private readonly DecisionDateConverter decisionDateConverter = new();

    [Theory]
    [InlineData("2025-01-15", 2025, 1, 15, 0, 0, 0)]
    [InlineData("2025/01/15", 2025, 1, 15, 0, 0, 0)]
    [InlineData("2025 01 15", 2025, 1, 15, 0, 0, 0)]
    [InlineData("2025-1-5", 2025, 1, 5, 0, 0, 0)]
    [InlineData("0001-01-01", 1, 1, 1, 0, 0, 0)]
    [InlineData("9999-12-31", 9999, 12, 31, 0, 0, 0)]
    [InlineData("2024-02-29", 2024, 2, 29, 0, 0, 0)]
    [InlineData("2025-01-15 09:30:45", 2025, 1, 15, 9, 30, 45)]
    [InlineData("2025-01-15T09:30:45", 2025, 1, 15, 9, 30, 45)]
    [InlineData("2025-01-15 23:59:59", 2025, 1, 15, 23, 59, 59)]
    [InlineData("  2025-01-15  ", 2025, 1, 15, 0, 0, 0)]
    [InlineData("\t2025-01-15\t", 2025, 1, 15, 0, 0, 0)]
    [InlineData(" 2025-01-15 ", 2025, 1, 15, 0, 0, 0)]
    [InlineData("2025-01", 2025, 1, 1, 0, 0, 0)]
    public void Read_WithValidDate_ReturnsParsedDate(string input, int year, int month, int day, int hour, int minute,
        int second)
    {
        using var csv = CsvReader.FromText($"\"{input}\"", new CsvReaderOptions { HasHeader = false });
        csv.Read();

        var result = decisionDateConverter.Read(csv, 0);

        Assert.Equal(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("    ")]
    [InlineData("\t")]
    [InlineData(" ")]
    public void Read_WithEmptyOrWhitespace_ThrowsDecisionDateMustBeProvided(string input)
    {
        using var csv = CsvReader.FromText($"\"{input}\"", new CsvReaderOptions { HasHeader = false });
        csv.Read();

        var exception = Assert.Throws<CsvValidationException>(() => decisionDateConverter.Read(csv, 0));

        Assert.Equal("Decision date must be provided", exception.Message);
    }

    [Theory]
    [InlineData("15-01-2025")]
    [InlineData("15/01/2025")]
    [InlineData("01/15/2025")]
    [InlineData("1-15-2025")]
    [InlineData("not-a-date")]
    [InlineData("202")]
    [InlineData("25-01-15")]
    [InlineData("-2025-01-15")]
    [InlineData("Jan 15 2025")]
    [InlineData("15 January 2025")]
    [InlineData("January 2025")]
    [InlineData("T2025-01-15")]
    [InlineData("a2025-01-15")]
    [InlineData("0")]
    [InlineData("null")]
    [InlineData("2025-13-01")]
    [InlineData("2025-00-10")]
    [InlineData("2025-01-32")]
    [InlineData("2025-01-00")]
    [InlineData("2025-02-30")]
    [InlineData("2023-02-29")]
    [InlineData("2025-04-31")]
    [InlineData("2025-01-15 25:00:00")]
    [InlineData("9315-01-2025")]
    [InlineData("2025-xx-yy")]
    [InlineData("0000-01-01")]
    public void Read_WithBadDate_ThrowsUnsupportedDecisionDate(string input)
    {
        using var csv = CsvReader.FromText($"\"{input}\"", new CsvReaderOptions { HasHeader = false });
        csv.Read();

        var exception = Assert.Throws<CsvValidationException>(() => decisionDateConverter.Read(csv, 0));

        Assert.Equal("Unsupported decision date. Ensure dates are in yyyy-MM-dd format", exception.Message);
    }

    [Fact]
    public void Read_ReadsTheRequestedColumn()
    {
        using var csv = CsvReader.FromText("ignored,2025-03-04,also ignored",
            new CsvReaderOptions { HasHeader = false });
        csv.Read();

        var result = decisionDateConverter.Read(csv, 1);

        Assert.Equal(new DateTime(2025, 3, 4, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void Write_Always_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() => decisionDateConverter.Write(null!, new DateTime(2025, 1, 15)));
    }
}
