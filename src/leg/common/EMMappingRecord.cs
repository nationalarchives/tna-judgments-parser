namespace UK.Gov.Legislation.Common {

/// <summary>
/// Represents a complete metadata record from the EM to legislation mapping CSV.
/// Maps to the structure of the XML metadata files for Explanatory Memoranda.
/// </summary>
internal class EMMappingRecord {
    
    public string EmUri { get; init; }
    public string EmType { get; init; }
    public string EmTitle { get; init; }
    public string EmDate { get; init; }
    public int? EmYear { get; init; }
    public int EmVersion { get; init; }
    public string LegislationUri { get; init; }
    public string LegislationClass { get; init; }
    public int? LegislationYear { get; init; }
    public string LegislationNumber { get; init; }
    public string LegislationTitle { get; init; }
    public string Department { get; init; }
    public string ModifiedDate { get; init; }

}

}
