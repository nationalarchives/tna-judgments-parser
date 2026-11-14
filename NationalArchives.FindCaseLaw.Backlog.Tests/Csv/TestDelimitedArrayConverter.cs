using System;

using Backlog.Csv;

using CsvHelper;

using Xunit;

namespace NationalArchives.FindCaseLaw.Backlog.Tests.Csv;

public class TestDelimitedArrayConverter
{
    private readonly DelimitedArrayConverter _converter = new();

    [Theory]
    [InlineData("   ", new string[] { })]
    [InlineData("  item1  ", new[] { "item1" })]
    [InlineData("  item1  ,  item2  ,  item3  ", new[] { "item1", "item2", "item3" })]
    [InlineData("  item1 item2  ,  item3 item4  ", new[] { "item1 item2", "item3 item4" })]
    [InlineData(" , ", new string[] { })]
    [InlineData(" , , ", new string[] { })]
    [InlineData(" item1 , item2 ", new[] { "item1", "item2" })]
    [InlineData("", new string[] { })]
    [InlineData(",", new string[] { })]
    [InlineData(",,", new string[] { })]
    [InlineData(",,,", new string[] { })]
    [InlineData(",,item1,,item2,,", new[] { "item1", "item2" })]
    [InlineData(",item1,", new[] { "item1" })]
    [InlineData(",item1,,item2,  ,item3,,", new[] { "item1", "item2", "item3" })]
    [InlineData(",item1,item2", new[] { "item1", "item2" })]
    [InlineData("123", new[] { "123" })]
    [InlineData("3,1,2", new[] { "3", "1", "2" })]
    [InlineData("ITEM1,ITEM2", new[] { "ITEM1", "ITEM2" })]
    [InlineData("Item1,Item2", new[] { "Item1", "Item2" })]
    [InlineData("\n", new string[] { })]
    [InlineData("\t", new string[] { })]
    [InlineData("\t,\t", new string[] { })]
    [InlineData("\titem1\t,\titem2\t", new[] { "item1", "item2" })]
    [InlineData("\u00A0", new string[] { })]
    [InlineData("\u00A0,\u00A0", new string[] { })]
    [InlineData("\u00A0item1\u00A0,item2", new[] { "item1", "item2" })]
    [InlineData("item1 item2 item3,item4 item5", new[] { "item1 item2 item3", "item4 item5" })]
    [InlineData("item1 item2", new[] { "item1 item2" })]
    [InlineData("item1 item2,item3 item4", new[] { "item1 item2", "item3 item4" })]
    [InlineData("item1 item2,item3", new[] { "item1 item2", "item3" })]
    [InlineData("item1", new[] { "item1" })]
    [InlineData("item1,  ,item2", new[] { "item1", "item2" })]
    [InlineData("item1, item2", new[] { "item1", "item2" })]
    [InlineData("item1,,item2", new[] { "item1", "item2" })]
    [InlineData("item1,ITEM1,Item1", new[] { "item1", "ITEM1", "Item1" })]
    [InlineData("item1,\u00A0,item2", new[] { "item1", "item2" })]
    [InlineData("item1,item1", new[] { "item1", "item1" })]
    [InlineData("item1,item1,item1", new[] { "item1", "item1", "item1" })]
    [InlineData("item1,item2", new[] { "item1", "item2" })]
    [InlineData("item1,item2,", new[] { "item1", "item2" })]
    [InlineData("item1,item2,item1", new[] { "item1", "item2", "item1" })]
    [InlineData("item1,item2,item3", new[] { "item1", "item2", "item3" })]
    [InlineData("item1,item2,item3,item4", new[] { "item1", "item2", "item3", "item4" })]
    [InlineData("item2", new[] { "item2" })]
    [InlineData("item3,item1,item2", new[] { "item3", "item1", "item2" })]
    [InlineData("item3,item2,item1", new[] { "item3", "item2", "item1" })]
    [InlineData("item1;item2", new[] { "item1", "item2" })]
    [InlineData("item1; item2", new[] { "item1", "item2" })]
    [InlineData("item1,item2;item3", new[] { "item1", "item2", "item3" })]
    [InlineData("item1,item2;item3 ;", new[] { "item1", "item2", "item3" })]
    [InlineData(" ; ", new string[] { })]
    [InlineData("item1 ; item2 ; item3", new[] { "item1", "item2", "item3" })]
    public void Read_ReturnsExpectedArray(string input, string[] expected)
    {
        using var csv = CsvReader.FromText($"\"{input}\"", new CsvReaderOptions { HasHeader = false });
        csv.Read();

        var result = _converter.Read(csv, 0);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Write_Always_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() => _converter.Write(null!, ["item1"]));
    }
}
