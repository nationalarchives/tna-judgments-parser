#nullable enable

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Backlog.Csv;

internal class BooleanSkipConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        return text?.Trim().ToLower() switch
        {
            null or "" or "n" or "no" or "f" or "false" or "0" => false,
            _ => true // return true when there is any value that is not explicitly negative
        };
    }
}
