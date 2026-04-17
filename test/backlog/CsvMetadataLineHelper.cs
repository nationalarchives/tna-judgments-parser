#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using Backlog.Csv;

using Xunit;

namespace test.backlog;

public static class CsvMetadataLineHelper
{
    /// <summary>
    ///     Has a sample value in each required field
    /// </summary>
    internal static readonly CsvLine DummyLine = new()
    {
        id = "007",
        Court = "UKFTT-GRC",
        FilePath = "",
        Extension = ".pdf",
        DecisionDateTime = DateTime.MinValue,
        CaseNo = "ABC/2023/001",
        Respondent = "The respondent"
    };

    internal static readonly CsvLine DummyLineWithClaimants = DummyLine with { Claimants = "The claimants" };

    internal static void AssertCsvLinesMatch(List<CsvLine> result, params CsvLine[] expectedCsvLines)
    {
        Assert.Collection(result, expectedCsvLines.Select(AssertCsvLineEquals).ToArray());
    }

    internal static Action<CsvLine> AssertCsvLineEquals(CsvLine expectedCsvLine)
    {
        return line =>
            Assert.EquivalentWithExclusions(expectedCsvLine, line, l => l.FullCsvLineContents, l => l.CsvProperties);
    }
}
