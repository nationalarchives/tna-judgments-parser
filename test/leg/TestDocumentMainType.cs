using Xunit;

namespace UK.Gov.Legislation.Common.Test {

/// <summary>
/// Load-bearing assertion: "Draft" in a source label qualifies the parent instrument, not
/// the associated document, so it must never reach a <c>ukm:DocumentMainType</c> value.
/// A draft SI's explanatory memorandum is itself final. The parent's status is recorded on
/// <c>ukm:Legislation/@Class</c>, and duplicating it here previously drifted out of step
/// with that attribute.
/// </summary>
public class TestDocumentMainType {

    [Theory]
    [InlineData("UK Draft SI Explanatory Memorandum", "UnitedKingdomExplanatoryMemorandum")]
    [InlineData("UK SI Explanatory Memorandum", "UnitedKingdomExplanatoryMemorandum")]
    [InlineData("NI Draft SR Explanatory Memorandum", "NorthernIrelandExplanatoryMemorandum")]
    [InlineData("NI Statutory Rule Explanatory Memorandum", "NorthernIrelandExplanatoryMemorandum")]
    [InlineData("Scottish Draft SI Policy Note", "ScottishPolicyNote")]
    [InlineData("Scottish Draft SI Executive Note", "ScottishExecutiveNote")]
    public void EMDropsDraft(string label, string expected) {
        Assert.Equal(expected, EMLegislationMapping.NormalizeDocumentMainType(label));
    }

    [Theory]
    [InlineData("UK Draft SI Transposition Note", "UnitedKingdomTranspositionNote")]
    [InlineData("Scottish Draft SI Transposition Note", "ScottishTranspositionNote")]
    [InlineData("NI Statutory Rule Transposition Note", "NorthernIrelandTranspositionNote")]
    public void TNDropsDraft(string label, string expected) {
        Assert.Equal(expected, TNLegislationMapping.NormalizeDocumentMainType(label));
    }

    [Theory]
    [InlineData("UK Draft SI Code of Practice", "UnitedKingdomCodeOfPractice")]
    [InlineData("Scottish Draft SI Code of Practice", "ScottishCodeOfPractice")]
    public void CoPDropsDraft(string label, string expected) {
        Assert.Equal(expected, CoPLegislationMapping.NormalizeDocumentMainType(label));
    }

    [Theory]
    [InlineData("UK Draft SI Other Document", "UnitedKingdomOtherDocument")]
    [InlineData("Scottish Draft SI Other Document", "ScottishOtherDocument")]
    [InlineData("NI Statutory Rule Other Document", "NorthernIrelandOtherDocument")]
    public void ODDropsDraft(string label, string expected) {
        Assert.Equal(expected, ODLegislationMapping.NormalizeDocumentMainType(label));
    }

    /// <summary>
    /// Welsh has its own branch. It previously had none, so every Welsh "Other Document"
    /// fell through to the bare "OtherDocument" fallback.
    /// </summary>
    [Fact]
    public void ODRecognisesWelsh() {
        Assert.Equal("WelshOtherDocument",
            ODLegislationMapping.NormalizeDocumentMainType("Welsh SI Other Document"));
    }

    /// <summary>
    /// A bare "ni" substring test also matches the "ni" inside "UnitedKingdom", which
    /// classified a UK document as Northern Irish.
    /// </summary>
    [Fact]
    public void JurisdictionDoesNotMatchNiInsideUnitedKingdom() {
        Assert.NotEqual("NorthernIreland",
            DocumentMainTypeNormalizer.Jurisdiction("UnitedKingdomImpactAssessment"));
    }

    [Theory]
    [InlineData("Scottish SI Impact Assessment", "ScottishImpactAssessment")]
    [InlineData("Scottish Draft SI Impact Assessment", "ScottishImpactAssessment")]
    [InlineData("Scottish SI Equality Impact Assessment", "ScottishEqualityImpactAssessment")]
    [InlineData("UK SI Equality Impact Assessment", "UnitedKingdomEqualityImpactAssessment")]
    public void IANormalisesDescriptiveLabels(string label, string expected) {
        Assert.Equal(expected, IALegislationMapping.NormalizeDocumentMainType(label));
    }

    /// <summary>
    /// The ukia rows already carry a CLML-style value rather than a descriptive label.
    /// </summary>
    [Fact]
    public void IAPassesThroughAlreadyNormalisedValue() {
        Assert.Equal("UnitedKingdomImpactAssessment",
            IALegislationMapping.NormalizeDocumentMainType("UnitedKingdomImpactAssessment"));
    }

    /// <summary>
    /// No emitted value may contain "Draft", whatever the source label says.
    /// </summary>
    [Theory]
    [InlineData("UK Draft SI Explanatory Memorandum")]
    [InlineData("Scottish Draft SI Impact Assessment")]
    [InlineData("Scottish Draft SI Other Document")]
    [InlineData("UK Draft SI Transposition Note")]
    [InlineData("UK Draft SI Code of Practice")]
    public void NoEmittedValueContainsDraft(string label) {
        Assert.DoesNotContain("Draft", EMLegislationMapping.NormalizeDocumentMainType(label));
        Assert.DoesNotContain("Draft", IALegislationMapping.NormalizeDocumentMainType(label));
        Assert.DoesNotContain("Draft", TNLegislationMapping.NormalizeDocumentMainType(label));
        Assert.DoesNotContain("Draft", CoPLegislationMapping.NormalizeDocumentMainType(label));
        Assert.DoesNotContain("Draft", ODLegislationMapping.NormalizeDocumentMainType(label));
        Assert.DoesNotContain("Draft", ENLegislationMapping.NormalizeDocumentMainType(label));
    }

}

}
