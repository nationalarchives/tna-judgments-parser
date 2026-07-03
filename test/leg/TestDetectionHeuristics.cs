using Xunit;

using UK.Gov.Legislation.Common;
using IaHelper = UK.Gov.Legislation.ImpactAssessments.Helper;

namespace UK.Gov.Legislation.Test {

public class TestDetectionHeuristics {

    [Theory]
    [InlineData("Annex", true)]
    [InlineData("Annex 1", true)]
    [InlineData("Annex 12", true)]
    [InlineData("- Annex -", true)]
    [InlineData("Appendix", true)]
    [InlineData("Annex A", true)]
    [InlineData("Annex Z", true)]
    [InlineData("Annex AA", true)]
    [InlineData("Appendix A", true)]
    [InlineData("Annex A - Glossary", true)]
    [InlineData("Annex A: Glossary", true)]
    [InlineData("Annex A. Details", true)]
    [InlineData("Annex A \u2013 Estimated Cost Assumptions", true)]
    [InlineData("Annex B \u2014 Methodology", true)]
    [InlineData("Annexes", true)]
    [InlineData("ANNEXES", true)]
    [InlineData("Appendices", true)]
    [InlineData("Annexes:", true)]
    [InlineData("L. Annexes", true)]
    [InlineData("1. Annex A", true)]
    [InlineData("A: Annex B", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("Annex A is referenced here in body prose.", false)]
    [InlineData("See Annex A", false)]
    [InlineData("This paragraph mentions Annex A in passing", false)]
    [InlineData("Background", false)]
    [InlineData("Summary of findings", false)]
    public void IsLegAnnexHeading(string input, bool expected) {
        Assert.Equal(expected, BaseLegislativeDocumentParser.IsLegAnnexHeading(input));
    }

    [Theory]
    [InlineData("Figure 1", true)]
    [InlineData("Figure 12 Total customers", true)]
    [InlineData("Figure A-1 Entry rates", true)]
    [InlineData("Fig. 1", true)]
    [InlineData("Table 1", true)]
    [InlineData("Chart 1: Estimated numbers", true)]
    [InlineData("Box 1 Eligible population", true)]
    [InlineData("Diagram 3 - overview", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("Figure references are listed below", false)]
    [InlineData("1. Summary Analysis", false)]
    [InlineData("Description of options", false)]
    [InlineData("Table of contents", false)]
    public void IsFigureOrTableCaption(string input, bool expected) {
        Assert.Equal(expected, IaHelper.IsFigureOrTableCaption(input));
    }

    [Theory]
    [InlineData("Title:", true)]
    [InlineData("Title: Heat Networks Market Framework Regulations", true)]
    [InlineData("IA No:", true)]
    [InlineData("IA number:", true)]
    [InlineData("RPC Reference No:", true)]
    [InlineData("RPC reference number: RPC-DESNZ-4427", true)]
    [InlineData("Lead department or agency: DESNZ", true)]
    [InlineData("Department or agency:", true)]
    [InlineData("Type of measure: Secondary Legislation", true)]
    [InlineData("Contact for enquiries:", true)]
    [InlineData("Date: 22/10/2024", true)]
    [InlineData("Stage: Final", true)]
    [InlineData("Impact Assessment (IA)", true)]
    [InlineData("Impact Assessment", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("Background", false)]
    [InlineData("Summary of findings", false)]
    [InlineData("The title of this Act is...", false)]
    public void IsIaCoverHeading(string input, bool expected) {
        Assert.Equal(expected, IaHelper.IsIaCoverHeading(input));
    }

}

}
