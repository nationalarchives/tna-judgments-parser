#nullable enable

using System;
using System.Globalization;
using System.Text.RegularExpressions;

using CsvHelper;

namespace Backlog.Csv;

internal partial class DecisionDateConverter : ICsvConverter<DateTime>
{
    public DateTime Read(CsvReader reader, int index)
    {
        var text = reader.GetString(index).Trim();

        if (text.Length == 0)
        {
            throw new CsvValidationException("Decision date must be provided");
        }

        if (!StartsWithYear().IsMatch(text)
            || !DateTime.TryParse(text, CultureInfo.InvariantCulture, out var decisionDate))
        {
            throw new CsvValidationException("Unsupported decision date. Ensure dates are in yyyy-MM-dd format");
        }

        return decisionDate;
    }

    public void Write(CsvWriter writer, DateTime value) => throw new NotSupportedException();

    [GeneratedRegex(@"^\d\d\d\d")]
    private static partial Regex StartsWithYear();
}
