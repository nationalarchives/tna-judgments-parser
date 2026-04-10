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
    /// Whether section headings must have a number matching the section number regex (default true).
    /// EN documents use unnumbered headings like "Overview of the Act".
    /// </summary>
    public bool SectionTitleRequiresNumber { get; set; } = true;

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

    /// <summary>
    /// Creates configuration for Explanatory Notes
    /// </summary>
    public static LegislativeDocumentConfig ForExplanatoryNotes() {
        return new LegislativeDocumentConfig {
            DocumentTitles = new[] { "EXPLANATORY NOTES", "Explanatory Notes" },
            SectionTitleStyle = "Heading1",
            SectionTitleRequiresNumber = false,
            Level1SubheadingStyle = "HSubheading2",
            Level2SubheadingStyle = "HSubheading3",
            UriSuffix = "/notes",
            DefaultDocumentType = "ExplanatoryNotes",
            DocumentTypeMapping = new System.Collections.Generic.Dictionary<string, string> {
                { "EXPLANATORY NOTES", "ExplanatoryNotes" },
                { "Explanatory Notes", "ExplanatoryNotes" }
            }
        };
    }

    /// <summary>
    /// Creates configuration for Transposition Notes
    /// </summary>
    public static LegislativeDocumentConfig ForTranspositionNotes() {
        return new LegislativeDocumentConfig {
            DocumentTitles = new[] { "Transposition Note", "TRANSPOSITION NOTE" },
            SectionTitleStyle = "EMSectionTitle",
            Level1SubheadingStyle = "EMLevel1Subheading",
            Level2SubheadingStyle = "EMLevel2Subheading",
            UriSuffix = "/transposition-note",
            DefaultDocumentType = "TranspositionNote",
            DocumentTypeMapping = new System.Collections.Generic.Dictionary<string, string> {
                { "Transposition Note", "TranspositionNote" },
                { "TRANSPOSITION NOTE", "TranspositionNote" }
            }
        };
    }

    /// <summary>
    /// Creates configuration for Codes of Practice
    /// </summary>
    public static LegislativeDocumentConfig ForCodesOfPractice() {
        return new LegislativeDocumentConfig {
            DocumentTitles = new[] { "Code of Practice", "CODE OF PRACTICE" },
            SectionTitleStyle = "EMSectionTitle",
            Level1SubheadingStyle = "EMLevel1Subheading",
            Level2SubheadingStyle = "EMLevel2Subheading",
            UriSuffix = "/code-of-practice",
            DefaultDocumentType = "CodeOfPractice",
            DocumentTypeMapping = new System.Collections.Generic.Dictionary<string, string> {
                { "Code of Practice", "CodeOfPractice" },
                { "CODE OF PRACTICE", "CodeOfPractice" }
            }
        };
    }

    /// <summary>
    /// Creates configuration for Other Documents (catch-all for associated documents
    /// stored as ukm:OtherDocument in legislation.gov.uk's MarkLogic metadata).
    /// </summary>
    public static LegislativeDocumentConfig ForOtherDocuments() {
        return new LegislativeDocumentConfig {
            DocumentTitles = System.Array.Empty<string>(),
            SectionTitleStyle = "EMSectionTitle",
            Level1SubheadingStyle = "EMLevel1Subheading",
            Level2SubheadingStyle = "EMLevel2Subheading",
            UriSuffix = "/other-document",
            DefaultDocumentType = "OtherDocument",
            DocumentTypeMapping = new System.Collections.Generic.Dictionary<string, string>()
        };
    }
}

}
