namespace UK.Gov.Legislation.Common {

/// <summary>
/// Represents a complete metadata record from the associated document mapping CSV
/// for a Code of Practice. Versions are encoded in the filename suffix (_NNN).
/// </summary>
internal class CoPMappingRecord {

    public string CopUri { get; init; }
    public string CopType { get; init; }
    public string CopTitle { get; init; }
    public string CopDate { get; init; }
    public int? CopYear { get; init; }
    public int CopVersion { get; init; }
    public string LegislationUri { get; init; }
    public string LegislationClass { get; init; }
    public int? LegislationYear { get; init; }
    public string LegislationNumber { get; init; }
    public string LegislationTitle { get; init; }
    public string Department { get; init; }
    public string ModifiedDate { get; init; }

}

}
