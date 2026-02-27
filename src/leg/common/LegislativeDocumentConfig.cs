namespace UK.Gov.Legislation.Common {

/// <summary>
/// Configuration for legislative document parsing, allowing different document types
/// to specify their specific styles, titles, and other document-type-specific settings.
/// </summary>
public class LegislativeDocumentConfig {

    /// <summary>
    /// The document titles that should be recognized in the header (e.g., "Impact Assessment", "Explanatory Memorandum To")
    /// </summary>
    public string[] DocumentTitles { get; set; }

    /// <summary>
    /// The Word style name used for section titles (e.g., "IASectionTitle", "EMSectionTitle")
    /// </summary>
    public string SectionTitleStyle { get; set; }

    /// <summary>
    /// The Word style name used for level 1 subheadings (e.g., "IALevel1Subheading", "EMLevel1Subheading")
    /// </summary>
    public string Level1SubheadingStyle { get; set; }

    /// <summary>
    /// The Word style name used for level 2 subheadings (e.g., "IALevel2Subheading", "EMLevel2Subheading")
    /// </summary>
    public string Level2SubheadingStyle { get; set; }

    /// <summary>
    /// The URI suffix to append to regulation URIs (e.g., "/ia", "/em")
    /// </summary>
    public string UriSuffix { get; set; }

    /// <summary>
    /// The default document type name to use if header parsing fails
    /// </summary>
    public string DefaultDocumentType { get; set; }

    /// <summary>
    /// Document type mappings from header titles to internal type names
    /// </summary>
    public System.Collections.Generic.Dictionary<string, string> DocumentTypeMapping { get; set; }

    /// <summary>
    /// Creates configuration for Impact Assessments
    /// </summary>
    public static LegislativeDocumentConfig ForImpactAssessments() {
        return new LegislativeDocumentConfig {
            DocumentTitles = new[] { "Impact Assessment" },
            SectionTitleStyle = "IASectionTitle",
            Level1SubheadingStyle = "IALevel1Subheading",
            Level2SubheadingStyle = "IALevel2Subheading",
            UriSuffix = "/ia",
            DefaultDocumentType = "ImpactAssessment",
            DocumentTypeMapping = new System.Collections.Generic.Dictionary<string, string> {
                { "Impact Assessment", "ImpactAssessment" }
            }
        };
    }

    /// <summary>
    /// Creates configuration for Explanatory Memoranda
    /// </summary>
    public static LegislativeDocumentConfig ForExplanatoryMemoranda() {
        return new LegislativeDocumentConfig {
            DocumentTitles = new[] { "Explanatory Memorandum To", "Policy Note" },
            SectionTitleStyle = "EMSectionTitle",
            Level1SubheadingStyle = "EMLevel1Subheading",
            Level2SubheadingStyle = "EMLevel2Subheading",
            UriSuffix = "/memorandum",
            DefaultDocumentType = "ExplanatoryMemorandum",
            DocumentTypeMapping = new System.Collections.Generic.Dictionary<string, string> {
                { "Explanatory Memorandum To", "ExplanatoryMemorandum" },
                { "Policy Note", "PolicyNote" }
            }
        };
    }
}

}
