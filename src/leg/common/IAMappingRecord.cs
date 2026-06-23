namespace UK.Gov.Legislation.Common {

/// <summary>
/// Represents a complete metadata record from the IA to legislation mapping CSV.
/// Maps to the structure of the XML metadata files.
/// </summary>
internal class IAMappingRecord {

    public string UkiaUri { get; init; }
    public int? UkiaYear { get; init; }
    public int? UkiaNumber { get; init; }

    /// <summary>
    /// The IA filename's series prefix (ukia, ssifia, sdsifia). Used to name the
    /// document's images so they match its filename scheme.
    /// </summary>
    public string IaSeries { get; init; }

    /// <summary>
    /// The IA's identity within its parent legislation's /impacts path. For UK ukia
    /// IAs this is the ukia series year/number from the filename; for Scottish SI and
    /// Draft IAs (which have no independent series) it is the parent legislation's
    /// year/number. Held as strings so ISBN-numbered drafts are representable.
    /// </summary>
    public string ImpactsYear { get; init; }
    public string ImpactsNumber { get; init; }

    public string Title { get; init; }
    public string IADate { get; init; }
    public string DocumentStage { get; init; }
    public string DocumentMainType { get; init; }
    public string Department { get; init; }
    public string ModifiedDate { get; init; }
    public string LegislationUri { get; init; }
    public string LegislationClass { get; init; }
    public int? LegislationYear { get; init; }
    public string LegislationNumber { get; init; }

}

}

