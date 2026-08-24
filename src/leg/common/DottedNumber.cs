using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Common;


/// <summary>
/// A dotted decimal paragraph number — "1.", "1.1", "3.10.", "1.1.1" — parsed
/// into its integer components.
/// </summary>
/// <remarks>
/// <para>
/// Comparison is numeric, never textual: "3.10" follows "3.9", which a string
/// comparison gets backwards. <c>od/sdsiod_9780111061145_en_002</c> exercises
/// exactly that case.
/// </para>
/// <para>
/// Depth is capped at three components because that is the limit of what
/// <c>HardNumbers</c> extracts (see
/// <c>src/parsers/optimized/HardNumbers.cs</c>); a "1.1.1.1" never reaches the
/// model as a number at all. See <c>src/leg/NUMBERING.md</c> §7.
/// </para>
/// </remarks>
internal readonly struct DottedNumber : IEquatable<DottedNumber>
{

    /// <summary>The most components <c>HardNumbers</c> will extract.</summary>
    internal const int MaxComponents = 3;

    // [0-9] rather than \d: \d matches non-ASCII digits in .NET by default.
    private static readonly Regex Shape =
        new(@"^[0-9]+(?:\.[0-9]+)*\.?$", RegexOptions.Compiled);

    private readonly int[] components;

    private DottedNumber(int[] components)
    {
        this.components = components;
    }

    /// <summary>Number of components: "1." is 1, "1.1" is 2, "1.1.1" is 3.</summary>
    internal int Depth => components?.Length ?? 0;

    /// <summary>The final component, compared as an integer.</summary>
    internal int Last => components[^1];

    internal bool IsValid => components is not null;

    /// <summary>
    /// Parses a number, tolerating a trailing dot on any form. Both "1." and
    /// "1" yield depth 1; both "5.1" and "5.1." yield depth 2. All three
    /// combinations occur in the corpus, so canonicalisation happens here
    /// rather than at each call site.
    /// </summary>
    internal static bool TryParse(string text, out DottedNumber number)
    {
        number = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var trimmed = text.Trim();
        if (!Shape.IsMatch(trimmed))
            return false;
        if (trimmed.EndsWith('.'))
            trimmed = trimmed[..^1];
        if (trimmed.Length == 0)
            return false;

        var parts = trimmed.Split('.');
        if (parts.Length > MaxComponents)
            return false;

        var parsed = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], out parsed[i]))
                return false;  // overflow only; Shape already guarantees digits

        number = new DottedNumber(parsed);
        return true;
    }

    /// <summary>
    /// Whether this number is a direct child of <paramref name="parent"/> —
    /// one component deeper, sharing every component of the parent.
    /// "1.1" is a child of "1."; "1.1.1" is not.
    /// </summary>
    internal bool IsChildOf(DottedNumber parent)
    {
        if (!IsValid || !parent.IsValid)
            return false;
        if (Depth != parent.Depth + 1)
            return false;
        for (var i = 0; i < parent.Depth; i++)
            if (components[i] != parent.components[i])
                return false;
        return true;
    }

    public bool Equals(DottedNumber other)
    {
        if (!IsValid || !other.IsValid)
            return IsValid == other.IsValid;
        return components.SequenceEqual(other.components);
    }

    public override bool Equals(object obj) => obj is DottedNumber other && Equals(other);

    public override int GetHashCode()
    {
        if (!IsValid)
            return 0;
        HashCode hash = new();
        foreach (var c in components)
            hash.Add(c);
        return hash.ToHashCode();
    }

    public override string ToString() =>
        IsValid ? string.Join('.', components) : "<invalid>";

}
