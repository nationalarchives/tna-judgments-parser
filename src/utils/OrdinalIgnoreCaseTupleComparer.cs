using System;
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments.Utils;

internal sealed class OrdinalIgnoreCaseTupleComparer : IEqualityComparer<(string one, string two)>
{
    public bool Equals((string one, string two) x, (string one, string two) y)
    {
        return string.Equals(x.one, y.one, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.two, y.two, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode((string one, string two) obj)
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.one),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.two));
    }
}
