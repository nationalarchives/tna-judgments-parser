#nullable enable

using System;

using CsvHelper;

namespace Backlog.Csv;

internal class BooleanSkipConverter : ICsvConverter<bool>
{
    public bool Read(CsvReader reader, int index)
    {
        return reader.GetString(index).Trim().ToLower() switch
        {
            "" or "n" or "no" or "f" or "false" or "0" => false,
            _ => true // return true when there is any value that is not explicitly negative
        };
    }

    public void Write(CsvWriter writer, bool value) => throw new NotSupportedException();
}
