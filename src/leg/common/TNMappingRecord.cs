namespace UK.Gov.Legislation.Common {

/// <summary>
/// Represents a complete metadata record from the associated document mapping CSV
/// for a Transposition Note. Transposition Notes are not versioned.
/// </summary>
internal class TNMappingRecord {

    public string TnUri { get; init; }
    public string TnType { get; init; }
    public string TnTitle { get; init; }
    public string TnDate { get; init; }
    public int? TnYear { get; init; }
    public string LegislationUri { get; init; }
    public string LegislationClass { get; init; }
    public int? LegislationYear { get; init; }
    public string LegislationNumber { get; init; }
    public string LegislationTitle { get; init; }
    public string Department { get; init; }
    public string ModifiedDate { get; init; }

}

}
