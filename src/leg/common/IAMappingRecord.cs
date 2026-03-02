namespace UK.Gov.Legislation.Common {

/// <summary>
/// Represents a complete metadata record from the IA to legislation mapping CSV.
/// Maps to the structure of the XML metadata files.
/// </summary>
internal class IAMappingRecord {
    
    public string UkiaUri { get; init; }
    public int? UkiaYear { get; init; }
    public int? UkiaNumber { get; init; }
    public string Title { get; init; }
    public string IADate { get; init; }
    public string DocumentStage { get; init; }
    public string DocumentMainType { get; init; }
    public string Department { get; init; }
    public string ModifiedDate { get; init; }
    public string PDFDate { get; init; }
    public string LegislationUri { get; init; }
    public string LegislationClass { get; init; }
    public int? LegislationYear { get; init; }
    public string LegislationNumber { get; init; }

}

}

