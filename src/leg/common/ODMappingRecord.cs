namespace UK.Gov.Legislation.Common {

/// <summary>
/// Represents a complete metadata record from the associated document mapping CSV
/// for an Other Document (ukm:OtherDocument in MarkLogic). Versions encoded in filename suffix.
/// </summary>
internal class ODMappingRecord {

    public string OdUri { get; init; }
    public string OdType { get; init; }
    public string OdTitle { get; init; }
    public string OdDate { get; init; }
    public int? OdYear { get; init; }
    public int OdVersion { get; init; }
    public string LegislationUri { get; init; }
    public string LegislationClass { get; init; }
    public int? LegislationYear { get; init; }
    public string LegislationNumber { get; init; }
    public string LegislationTitle { get; init; }
    public string Department { get; init; }
    public string ModifiedDate { get; init; }

}

}
