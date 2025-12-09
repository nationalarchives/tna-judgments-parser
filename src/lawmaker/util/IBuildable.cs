#nullable enable
namespace UK.Gov.Legislation.Lawmaker;

/// <summary>
/// Describes an object which can be "built" into something.
/// </summary>
/// <typeparam name="T">The type being built.</typeparam>
/// <remarks>
/// <c>IBuildable<XNode></c> is the most common type of this interface
/// implemented in this parser. <seealso cref="SIPreface"/> as an example
/// implementation.
/// </remarks>
interface IBuildable<T>
{
    /// <summary>
    /// Build this object into type <c>T</c>
    /// </summary>
    /// <returns>A new <c>T</c> derived from this object.</returns>
    /// <remarks>
    /// There are no guarentees this method will not change state in the
    /// implementer, though this should be avoided where possible.
    /// </remarks>
    T? Build(Document Document);
}