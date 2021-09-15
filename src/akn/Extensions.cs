
using System;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

internal static class PartyRoleExtensions {

    internal static string EId(this PartyRole role) {
        string name = Enum.GetName(typeof(PartyRole), role);
        return Regex.Replace(name, @"([A-Z])", @"-$1").TrimStart('-').ToLower();
    }
    internal static string ShowAs(this PartyRole role) {
        string name = Enum.GetName(typeof(PartyRole), role);
        return Regex.Replace(name, @"([A-Z])", @" $1").TrimStart();
    }

}

}
