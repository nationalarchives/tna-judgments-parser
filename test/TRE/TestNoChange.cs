#nullable enable

using Shouldly;

using test.ApiTests;

using UK.Gov.NationalArchives.CaseLaw.TRE;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace test.TRE;

public class TestNoChange
{
    public static readonly TheoryData<int> IndicesTheoryData = TestParser_Judgments.IndicesTheoryData;

    [Theory]
    [MemberData(nameof(IndicesTheoryData))]
    public void TestJudgments_WithMetadataFromOutputOfCleanRun_AreTheSameAsOutputFromCleanRun(int i)
    {
        var docx = DocumentHelpers.ReadDocx(i);
        var cleanRunResponse = TestInputInjection.LambdaTest(docx, null);

        var externalMetadataFromCleanRun = new ParserInputs
        {
            DocumentType = cleanRunResponse.Meta.DocumentType,
            Metadata = new InputMetadata
            {
                URI = Api.URI.ExtractShortURIComponent(cleanRunResponse.Meta.Uri),
                Cite = cleanRunResponse.Meta.Cite,
                Court = cleanRunResponse.Meta.Court,
                Date = cleanRunResponse.Meta.Date,
                Name = cleanRunResponse.Meta.Name
            }
        };

        var runWithExternalMetadataResponse = TestInputInjection.LambdaTest(docx, externalMetadataFromCleanRun);

        runWithExternalMetadataResponse.Meta.ShouldBeEquivalentTo(cleanRunResponse.Meta);

        var cleanRunResponseXml = DocumentHelpers.RemoveNonDeterministicMetadata(cleanRunResponse.Xml, Xslt);
        var runWithExternalMetadataResponseXml =
            DocumentHelpers.RemoveNonDeterministicMetadata(runWithExternalMetadataResponse.Xml, Xslt);

        Assert.Equal(cleanRunResponseXml, runWithExternalMetadataResponseXml);
    }

    /// <summary>
    /// if the parser is given a date, it can't tell whether it's a 'decision' or a 'hearing' date
    /// </summary>
    private const string Xslt = """
                                <?xml version='1.0'?>
                                <xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' xmlns:uk='https://caselaw.nationalarchives.gov.uk/akn'>
                                  <xsl:template match='akn:FRBRManifestation/akn:FRBRdate/@date'/>
                                  <xsl:template match="akn:FRBRdate/@name[.='hearing']">
                                    <xsl:attribute name="name">decision</xsl:attribute>
                                  </xsl:template>
                                  <xsl:template match='@*|node()'>
                                    <xsl:copy>
                                      <xsl:apply-templates select='@*|node()'/>
                                    </xsl:copy>
                                  </xsl:template>
                                </xsl:stylesheet>
                                """;
}
