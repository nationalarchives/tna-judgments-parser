namespace UK.Gov.Legislation.Common {

/// <summary>
/// Constants for table of contents generation
/// </summary>
public static class TableOfContentsConstants {
    
    /// <summary>
    /// CSS class for TOC entries
    /// </summary>
    public const string TocEntryStyle = "TOCEntry";
    
    /// <summary>
    /// Block name for title in cover page
    /// </summary>
    public const string TitleBlockName = "title";
    
    /// <summary>
    /// Default titles for different document types
    /// </summary>
    public static class DefaultTitles {
        public const string ExplanatoryMemorandum = "Explanatory Memorandum";
        public const string ImpactAssessment = "Impact Assessment";
        public const string LegislativeDocument = "Legislative Document";
    }
    
    /// <summary>
    /// TOC item attributes
    /// </summary>
    public static class TocItemAttributes {
        public const string Level = "level";
        public const string Href = "href";
    }
    
    /// <summary>
    /// Inline element names for TOC
    /// </summary>
    public static class InlineNames {
        public const string TocNum = "tocNum";
        public const string TocHeading = "tocHeading";
    }
    
    /// <summary>
    /// URL fragment template for section links
    /// </summary>
    public const string SectionHrefTemplate = "#section-{0}";
}

}
