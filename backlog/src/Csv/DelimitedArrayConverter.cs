#nullable enable

using System;
using System.Linq;

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Backlog.Csv;

internal class DelimitedArrayConverter : DefaultTypeConverter
{
    private readonly char[]? delimiters = [',', ';'];

    public override object ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var items = text.Split(delimiters);

        var cleanWhitespace = items.Select(item => item.Trim());
        var blanksRemoved = cleanWhitespace.Where(item => !string.IsNullOrWhiteSpace(item));

        return blanksRemoved.ToArray();
    }
}
