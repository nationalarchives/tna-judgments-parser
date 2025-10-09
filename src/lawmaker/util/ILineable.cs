#nullable enable
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker;

/// <summary>
/// Implementers of the this interface can convert to a series of <c>WLine</c>
/// Which is useful for doing textual analysis without drilling down into the
/// structure.
/// </summary>
interface ILineable
{
    /// <summary>
    /// Returns a sequence of the `WLines` present in the implementer.
    /// The order is up to the implementer.
    /// </summary>
    /// <remarks>
    /// Each `WLine` should be a valid reference and allow mutation.
    /// </remarks>
    IEnumerable<WLine> Lines { get; }
    WLine? GetLastLine() => Lines.LastOrDefault();

}