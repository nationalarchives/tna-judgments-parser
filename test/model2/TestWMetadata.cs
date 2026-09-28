#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using Moq;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

namespace test.model2;

public class TestWMetadata
{
    private static ILine LineWith(params IInline[] contents)
    {
        return Mock.Of<ILine>(l => l.Contents == contents);
    }

    private static WMetadata CreateWMetadata(IJudgment judgment)
    {
        var emptyWordDoc = WordprocessingDocument.Create(new MemoryStream(), WordprocessingDocumentType.Document);
        emptyWordDoc.AddMainDocumentPart();

        return new WMetadata(emptyWordDoc.MainDocumentPart!, judgment);
    }

    private static Mock<IJudgment> SetupMockJudgment(IEnumerable<IAnnex>? annexes = null,
        IList<IDecision>? body = null,
        IEnumerable<IBlock>? conclusions = null,
        IEnumerable<IBlock>? coverPage = null,
        IEnumerable<IBlock>? header = null,
        IEnumerable<IInternalAttachment>? internalAttachments = null)
    {
        var mockJudgment = new Mock<IJudgment>();

        mockJudgment.Setup(j => j.Annexes).Returns(annexes ?? []);
        mockJudgment.Setup(j => j.Body).Returns(body ?? []);
        mockJudgment.Setup(j => j.Conclusions).Returns(conclusions ?? []);
        mockJudgment.Setup(j => j.CoverPage).Returns(coverPage ?? []);
        mockJudgment.Setup(j => j.Header).Returns(header ?? []);
        mockJudgment.Setup(j => j.InternalAttachments).Returns(internalAttachments ?? []);

        return mockJudgment;
    }

    [Fact]
    public void Court_WithCourtTypeInHeader_StoresValueForSubsequentCalls()
    {
        var mockJudgment = SetupMockJudgment(
            header: [LineWith(new WCourtType("COURT OF APPEAL (CIVIL DIVISION)", null) { Code = Courts.CoA_Civil.Code })]
        );

        var wMetadata = CreateWMetadata(mockJudgment.Object);

        mockJudgment.VerifyGet(j => j.Header, Times.Never);

        // Trigger get court
        _ = wMetadata.Court;

        // Assert it was retrieved from the header
        mockJudgment.VerifyGet(j => j.Header, Times.Once);
        mockJudgment.Invocations.Clear();

        //Trigger get court again
        _ = wMetadata.Court;

        // Assert that this time it was not retrieved from the header (and thus was retrieved from the cache)
        mockJudgment.VerifyGet(j => j.Header, Times.Never);
    }

    [Fact]
    public void Court_NoCourtTypeOrCitation_IsNull()
    {
        var mockJudgment = SetupMockJudgment();
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.ShouldBeOfType<WMetadata>()
                 .Court.ShouldBeNull();
    }

    [Fact]
    public void Court_WCourtTypeInHeader_ReturnsCourt()
    {
        var mockJudgment = SetupMockJudgment(header:
            [LineWith(new WCourtType("High Court", null) { Code = Courts.EWHC.Code })]
        );
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.ShouldBeOfType<WMetadata>()
                 .Court.ShouldBe(Courts.EWHC);
    }

    [Fact]
    public void Court_OnlyWCourtType2InHeader_ReturnsCourt()
    {
        var mockJudgment = SetupMockJudgment(
            header: [LineWith(new WCourtType2 { Code = Courts.EWCOP.Code })]
        );
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.Court.ShouldBe(Courts.EWCOP);
    }

    [Fact]
    public void Court_NoCourtTypeInHeader_IsExtractedFromCitation()
    {
        var mockJudgment = SetupMockJudgment(
            header: [LineWith(new WNeutralCitation("[2020] UKSC 5", null))]
        );
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.Court.ShouldBe(Courts.SupremeCourt);
    }

    [Theory]
    [InlineData("[2021] EWFC 45 (B)", "EWFC-B", "EWFC")]
    [InlineData("[2021] EWCOP 12 (T1)", "EWCOP-T1", "EWCOP")]
    [InlineData("[2021] EWCOP 12 (T2)", "EWCOP-T2", "EWCOP")]
    [InlineData("[2021] EWCOP 12 (T3)", "EWCOP-T3", "EWCOP")]
    public void Court_CitationHasSpecialistSuffix_IsRefinedToSpecialistCourt(string citation, string expectedCode,
        string parentCode)
    {
        var mockJudgment = SetupMockJudgment(header:
        [
            LineWith(new WCourtType("court", null) { Code = parentCode }),
            LineWith(new WNeutralCitation(citation, null))
        ]);
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.Court?.Code.ShouldBe(expectedCode);
    }

    [Fact]
    public void Cite_WithNeutralCitationInHeader_StoresValueForSubsequentCalls()
    {
        var mockJudgment = SetupMockJudgment(
            header: [LineWith(new WNeutralCitation("[2020] EWHC 100 (Ch)", null))]
        );

        var wMetadata = CreateWMetadata(mockJudgment.Object);

        mockJudgment.VerifyGet(j => j.Header, Times.Never);

        // Trigger get cite
        _ = wMetadata.Cite;

        // Assert it was retrieved from the header
        mockJudgment.VerifyGet(j => j.Header, Times.Once);
        mockJudgment.Invocations.Clear();

        //Trigger get cite again
        _ = wMetadata.Cite;

        // Assert that this time it was not retrieved from the header (and thus was retrieved from the cache)
        mockJudgment.VerifyGet(j => j.Header, Times.Never);
    }

    [Fact]
    public void Cite_NeutralCitationInHeader_IsNormalized()
    {
        var mockJudgment = SetupMockJudgment(
            header: [LineWith(new WNeutralCitation("[2020] ewhc 100 (ch)", null))]
        );
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.Cite.ShouldBe("[2020] EWHC 100 (Ch)");
    }

    [Fact]
    public void Cite_AbsentFromHeader_ReadFromCoverPage()
    {
        var mockJudgment = SetupMockJudgment(
            coverPage: [LineWith(new WNeutralCitation("[2019] UKSC 5", null))]
        );
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.Cite.ShouldBe("[2019] UKSC 5");
    }

    [Fact]
    public void Cite_NeutralCitationAbsent_ReadFromNeutralCitation2()
    {
        var mockJudgment = SetupMockJudgment(
            header: [LineWith(new WNeutralCitation2 { Contents = [new WText("[2020] EWCA Civ 100", null)] })]
        );
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.Cite.ShouldBe("[2020] EWCA Civ 100");
    }

    [Fact]
    public void Cite_NoCitationPresent_IsNull()
    {
        var mockJudgment = SetupMockJudgment();
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.Cite.ShouldBeNull();
    }

    [Fact]
    public void ShortUriComponentYearAndNumber_NeutralCitationInHeader_AreDerivedFromCite()
    {
        var mockJudgment = SetupMockJudgment(header: [LineWith(new WNeutralCitation("[2020] EWHC 100 (Ch)", null))]);
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.ShortUriComponent.ShouldBe("ewhc/ch/2020/100");
        wMetadata.Year.ShouldBe(2020);
        wMetadata.Number.ShouldBe(100);
    }

    [Fact]
    public void ShortUriComponentYearAndNumber_NoCite_AreNull()
    {
        var mockJudgment = SetupMockJudgment();
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.ShortUriComponent.ShouldBeNull();
        wMetadata.Year.ShouldBeNull();
        wMetadata.Number.ShouldBeNull();
    }

    [Fact]
    public void WorkExpressionAndManifestationUris_WithShortUriComponent_AreBuiltFromIt()
    {
        var mockJudgment = SetupMockJudgment(header: [LineWith(new WNeutralCitation("[2020] EWHC 100 (Ch)", null))]);
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.WorkThis.ShouldBe("https://caselaw.nationalarchives.gov.uk/id/ewhc/ch/2020/100");
        wMetadata.WorkURI.ShouldBe(wMetadata.WorkThis);
        wMetadata.ExpressionThis.ShouldBe("https://caselaw.nationalarchives.gov.uk/ewhc/ch/2020/100");
        wMetadata.ExpressionUri.ShouldBe(wMetadata.ExpressionThis);
        wMetadata.ManifestationThis.ShouldBe(wMetadata.ExpressionThis + "/data.xml");
        wMetadata.ManifestationUri.ShouldBe(wMetadata.ManifestationThis);
    }

    [Fact]
    public void CaseNos_CoverPageAndHeaderBothPresent_ReturnsAllCaseNumbers()
    {
        var mockJudgment = SetupMockJudgment(
            coverPage: [LineWith(new WCaseNo("COVER-1", null))],
            header: [LineWith(new WCaseNo("HEADER-1", null))]);

        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.CaseNos().ShouldBe(["COVER-1", "HEADER-1"]);
    }

    [Fact]
    public void CaseNos_NonePresent_IsEmpty()
    {
        var mockJudgment = SetupMockJudgment();
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.CaseNos().ShouldBeEmpty();
    }

    [Fact]
    public void Date_NoDatesPresent_IsNull()
    {
        var mockJudgment = SetupMockJudgment();
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.Date.ShouldBeNull();
    }

    [Fact]
    public void Date_SingleWDocDateInHeader_ReturnsThatDate()
    {
        var mockJudgment = SetupMockJudgment(header:
        [
            LineWith(new WDocDate("1 December 2020", null, new DateTime(2020, 12, 1, 0, 0, 0, DateTimeKind.Utc)))
        ]);
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.Date.ShouldNotBeNull();
        wMetadata.Date.Date.ShouldBe("2020-12-01");
        wMetadata.Date.Name.ShouldBe("judgment");
    }

    [Fact]
    public void Date_MultipleWDocDatesPresent_PicksTheLatestDate()
    {
        var mockJudgment = SetupMockJudgment(header:
        [
            LineWith(new WDocDate("1 January 2020", null, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc))),
            LineWith(new WDocDate("15 March 2021", null, new DateTime(2021, 3, 15, 0, 0, 0, DateTimeKind.Utc)))
        ]);
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.Date.ShouldNotBeNull();
        wMetadata.Date.Date.ShouldBe("2021-03-15");
    }

    [Fact]
    public void Jurisdictions_WDocJurisdictionInHeader_ReturnsJurisdiction()
    {
        var mockJudgment = SetupMockJudgment(header:
            [LineWith(new WDocJurisdiction { ShortName = "EW", LongName = "England and Wales" })]
        );
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        var jurisdiction = wMetadata.Jurisdictions.ShouldHaveSingleItem();
        jurisdiction.ShortName.ShouldBe("EW");
        jurisdiction.LongName.ShouldBe("England and Wales");
        jurisdiction.Id.ShouldBe("jurisdiction-ew");
    }

    [Fact]
    public void ExternalAttachments_NoAttachmentsProvided_IsEmpty()
    {
        var mockJudgment = SetupMockJudgment();
        var wMetadata = CreateWMetadata(mockJudgment.Object);

        wMetadata.ExternalAttachments.ShouldBeEmpty();
    }
}
