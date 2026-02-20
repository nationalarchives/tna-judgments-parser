using System;

using Backlog.Csv;

namespace test.backlog;

public static class CsvMetadataLineHelper
{
    /// <summary>
    /// Has a sample value in each required field
    /// </summary>
    internal static CsvLine DummyLine = new()
    {
        id = "007",
        court = "UKFTT-GRC",
        FilePath = "",
        Extension = ".pdf",
        decision_datetime = DateTime.MinValue,
        CaseNo = "ABC/2023/001",
        respondent = "The respondent"
    };

    internal static CsvLine DummyLineWithClaimants = DummyLine with { claimants = "The claimants" };
}
