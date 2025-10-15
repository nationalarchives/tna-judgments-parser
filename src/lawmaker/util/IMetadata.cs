#nullable enable
using System.Collections.Generic;

namespace UK.Gov.Legislation.Lawmaker;

/// <summary>
/// Describes an object possibly containing some metadata for a document.
/// </summary>
interface IMetadata
{
    /// <summary>
    /// The metadata the implementer could determine.
    /// </summary>
    /// <remarks>
    /// May return an empty collection if no metadata was determined.
    /// </remarks>
    IEnumerable<Reference> Metadata { get; }
}
