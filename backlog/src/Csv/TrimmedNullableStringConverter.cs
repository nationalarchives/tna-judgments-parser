#nullable enable

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Backlog.Csv;

internal class TrimmedNullableStringConverter : DefaultTypeConverter
{
    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
