#nullable enable

using System;

using CsvHelper;

namespace Backlog.Csv;

internal class DelimitedArrayConverter : ICsvConverter<string[]>
{
    private static readonly char[] Delimiters = [',', ';'];

    public string[] Read(CsvReader reader, int index)
    {
        return reader.GetString(index)
                     .Split(Delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public void Write(CsvWriter writer, string[] value) => throw new NotSupportedException();
}
