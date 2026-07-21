#nullable enable

using System;

using Backlog;

using Shouldly;

using Xunit;

namespace test.backlog;

public class TestBacklogParserWorker
{
    [Fact]
    public void MakeStubResponse_SetsDocumentTypeToDecision()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants;

        var result = BacklogParserWorker.MakeStubResponse(csvLine);

        result.Meta.DocumentType.ShouldBe("decision");
    }

    [Fact]
    public void MakeStubResponse_SetsCourt()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with { Court = "ET" };

        var result = BacklogParserWorker.MakeStubResponse(csvLine);

        result.Meta.Court.ShouldBe("ET");
    }

    [Fact]
    public void MakeStubResponse_SetsDate()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            DecisionDateTime = new DateTime(2026, 07, 08, 09, 10, 11, 12, DateTimeKind.Utc)
        };

        var result = BacklogParserWorker.MakeStubResponse(csvLine);

        result.Meta.Date.ShouldBe("2026-07-08");
    }

    [Fact]
    public void MakeStubResponse_SetsName()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Claimants = "some claimant",
            Respondent = "some respondent"
        };

        var result = BacklogParserWorker.MakeStubResponse(csvLine);

        result.Meta.Name.ShouldBe("some claimant v some respondent");
    }

    [Fact]
    public void MakeStubResponse_WithNcn_PutsNcnIntoCite()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with { Ncn = "My NCN" };

        var result = BacklogParserWorker.MakeStubResponse(csvLine);

        result.Meta.Cite.ShouldBe("My NCN");
    }

    [Fact]
    public void MakeStubResponse_WithoutNcn_SetsCiteToNull()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with { Ncn = null };

        var result = BacklogParserWorker.MakeStubResponse(csvLine);

        result.Meta.Cite.ShouldBeNull();
    }
}
