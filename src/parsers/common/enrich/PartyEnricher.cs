using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments.Utils;

namespace UK.Gov.Legislation.Judgments.Parse;

// there are "third" paries in EWCA/Civ/2015/631

internal class PartyEnricher : Enricher
{
    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        throw new NotSupportedException();
    }

    protected override WLine Enrich(WLine line)
    {
        throw new NotSupportedException();
    }

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        var before = blocks.ToArray();
        var after = new List<IBlock>();
        var i = 0;
        while (i < before.Length)
        {
            if (IsInTheMatterOf3(before, i))
            {
                after.AddRange(EnrichInTheMatterOf3(before, i));
                i += 3;
                break;
            }

            if (IsInTheMatterOf4(before, i))
            {
                after.AddRange(EnrichInTheMatterOf4(before, i));
                i += 4;
                break;
            }

            if (IsThreeLinePartyBlock(before, i))
            {
                after.AddRange(EnrichThreeLinePartyBlock(before, i));
                i += 3;
                break;
            }

            if (IsFourLinePartyBlock(before, i))
            {
                after.AddRange(EnrichFourLinePartyBlock(before, i));
                i += 4;
                break;
            }

            if (IsFiveLinePartyBlock(before, i))
            {
                after.AddRange(EnrichFiveLinePartyBlock(before, i));
                i += 5;
                break;
            }

            var rest = before[i..];
            if (TryEnrichMultiLinePartyBlock(rest, false, out var found) ||
                TryEnrichMultiLinePartyBlockWithInlineRoles(rest, out found) ||
                TryEnrichMultiLinePartyBlockWithTwoGroupsBeforeV(rest, out found))
            {
                after.AddRange(found);
                i += found.Count;
                break;
            }

            after.Add(before[i] switch
            {
                WTable table => EnrichTable(table),
                WLine line => EnrichLineWithDocTitle(line),
                _ => before[i]
            });
            i += 1;
        }

        after.AddRange(before.Skip(i));
        return after;
    }

    private static bool IsInTheMatterOf3(IBlock[] before, int i)
    {
        // EWCA/Civ/2008/1303
        return i <= before.Length - 3
            && before[i] is WLine line1 && IsBeforePartyMarker(line1)
            && before[i + 1] is WLine line2 && IsInTheMatterOf1(line2)
            && before[i + 2] is WLine line3 && IsAfterPartyMarker(line3);
    }

    private static List<IBlock> EnrichInTheMatterOf3(IBlock[] before, int i)
    {
        return
        [
            before[i],
            MakeDocTitle((WLine)before[i + 1]),
            before[i + 2]
        ];
    }

    private static bool IsInTheMatterOf4(IBlock[] before, int i)
    {
        // EWHC/QB/2017/2921, EWHC/Ch/2006/3549
        return i <= before.Length - 4
            && before[i] is WLine line1 && IsBeforePartyMarker(line1)
            && before[i + 1] is WLine line2 && IsInTheMatterOf1(line2)
            && before[i + 2] is WLine line3 && IsInTheMatterOf2(line3)
            && before[i + 3] is WLine line4 && IsAfterPartyMarker(line4);
    }

    private static List<IBlock> EnrichInTheMatterOf4(IBlock[] before, int i)
    {
        return
        [
            before[i],
            MakeDocTitle((WLine)before[i + 1]),
            MakeDocTitle((WLine)before[i + 2]),
            before[i + 3]
        ];
    }

    /* three and four */

    private static bool IsRexOrRegina(WLine line)
    {
        var content = Regex.Replace(line.NormalizedContent, @"\s+", "");
        return content.Equals("REX", StringComparison.OrdinalIgnoreCase)
            || content.Equals("REGINA", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThreeLinePartyBlock(IBlock[] before, int i)
    {
        return i <= before.Length - 4
            && before[i] is WLine line1 && IsRexOrRegina(line1)
            && before[i + 1] is WLine line2 && IsBetweenPartyMarker(line2)
            && before[i + 2] is WLine line3 && IsPartyName(line3)
            && before[i + 3] is WLine line4 && IsAfterPartyMarker(line4);
    }

    private static List<IBlock> EnrichThreeLinePartyBlock(IBlock[] before, int i)
    {
        return
        [
            MakeParty((WLine)before[i], PartyRole.BeforeTheV),
            before[i + 1],
            MakeParty((WLine)before[i + 2], PartyRole.AfterTheV)
        ];
    }

    private static bool IsFourLinePartyBlock(IBlock[] before, int i)
    {
        return i <= before.Length - 5
            && before[i] is WLine line1 && IsRexOrRegina(line1)
            && before[i + 1] is WLine line2 && IsBetweenPartyMarker(line2)
            && before[i + 2] is WLine line3 && IsPartyName(line3)
            && before[i + 3] is WLine line4 && IsPartyName(line4)
            && before[i + 4] is WLine line5 && IsAfterPartyMarker(line5);
    }

    private static List<IBlock> EnrichFourLinePartyBlock(IBlock[] before, int i)
    {
        return
        [
            MakeParty((WLine)before[i], PartyRole.BeforeTheV),
            before[i + 1],
            MakeParty((WLine)before[i + 2], PartyRole.AfterTheV),
            MakeParty((WLine)before[i + 3], PartyRole.AfterTheV)
        ];
    }

    /* five */

    private static bool IsFiveLinePartyBlock(IBlock[] before, int i)
    {
        return i <= before.Length - 5
            && before[i] is WLine line1 && IsBeforePartyMarker(line1)
            && before[i + 1] is WLine line2 && IsPartyName(line2)
            && before[i + 2] is WLine line3 && (IsBetweenPartyMarker(line3) || IsBetweenPartyMarker2(line3))
            && before[i + 3] is WLine line4 && IsPartyName(line4)
            && before[i + 4] is WLine line5 && IsAfterPartyMarker(line5);
    }

    private static List<IBlock> EnrichFiveLinePartyBlock(IBlock[] before, int i)
    {
        return
        [
            before[i],
            MakeParty((WLine)before[i + 1], PartyRole.BeforeTheV),
            before[i + 2],
            MakeParty((WLine)before[i + 3], PartyRole.AfterTheV),
            before[i + 4]
        ];
    }

    /* multi-line */

    private static bool TryEnrichMultiLinePartyBlock(IBlock[] rest, bool successive, out List<IBlock> enriched)
    {
        enriched = null;
        if (rest.Length == 0)
        {
            return false;
        }

        var i = 0;
        var line = rest[i];
        if (line is not WLine beforeLine
            || (!IsBeforePartyMarker(beforeLine)
                && !IsBeforePartyMarker2(beforeLine)
                && !(successive && IsBeforePartyMarker3(beforeLine))))
        {
            return false;
        }

        List<IBlock> result = [line];
        i += 1;
        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        if (line is WLine inPrivate
            && inPrivate.NormalizedContent.Equals("IN PRIVATE", StringComparison.OrdinalIgnoreCase))
        {
            // EWHC/Admin/2012/2822
            result.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return false;
            }

            line = rest[i];
        }

        if (line is WLine betweenMarker2 && IsBeforePartyMarker2(betweenMarker2))
        {
            result.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return false;
            }

            _ = rest[i];
        }

        if (!TryEnrichPartyNamesWithRoleLabel(rest[i..], out var firstGroupOfParites))
        {
            return false;
        }

        result.AddRange(firstGroupOfParites);
        i += firstGroupOfParites.Count;
        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        /* no "v" or "and" in EWHC/Comm/2013/3920 */
        if (line is WLine vOrAndMarker && (IsBetweenPartyMarker(vOrAndMarker) || IsBetweenPartyMarker2(vOrAndMarker)))
        {
            result.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return false;
            }

            _ = rest[i];
        }

        if (!TryEnrichPartyNamesWithRoleLabel(rest[i..], out var secondGroupOfParites))
        {
            return false;
        }

        result.AddRange(secondGroupOfParites);
        i += secondGroupOfParites.Count;
        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];

        if (line is WLine andMarker && IsBetweenPartyMarker2(andMarker))
        {
            result.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return false;
            }

            _ = rest[i];
        }

        if (TryEnrichPartyNamesWithRoleLabel(rest[i..], out var thirdGroupOfParites))
        {
            result.AddRange(thirdGroupOfParites);
            i += thirdGroupOfParites.Count;
        }

        if (TryEnrichPartyNamesWithRoleLabel(rest[i..], out var fourthGroupOfParites))
        {
            result.AddRange(fourthGroupOfParites);
            i += fourthGroupOfParites.Count;
        }

        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        if (line is WLine afterLine && IsAfterPartyMarker(afterLine))
        {
            result.Add(line);
            enriched = result;
            return true;
        }

        if (TryEnrichMultiLinePartyBlock(rest[i..], true, out var another))
        {
            result.AddRange(another);
        }

        enriched = result;
        return true;
    }

    private static bool TryEnrichMultiLinePartyBlockWithInlineRoles(IBlock[] rest, out List<IBlock> enriched)
    {
        // EWHC/Admin/2018/3311
        enriched = null;
        if (rest.Length == 0)
        {
            return false;
        }

        var i = 0;
        var line = rest[i];
        if (line is not WLine beforeLine || (!IsBeforePartyMarker(beforeLine) && !IsBeforePartyMarker2(beforeLine)))
        {
            return false;
        }

        List<IBlock> result = [line];
        i += 1;
        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        if (line is WLine betweenMarker2 && IsBeforePartyMarker2(betweenMarker2))
        {
            // perhaps do this only if first line isn't marker 2
            result.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return false;
            }

            line = rest[i];
        }

        if (line is not WLine partyLine1 || !IsPartyNameAndRole(partyLine1))
        {
            return false;
        }

        var party1 = MakePartyAndRole(partyLine1);
        result.Add(party1);
        i += 1;
        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        if (line is WLine vOrAndMarker && (IsBetweenPartyMarker(vOrAndMarker) || IsBetweenPartyMarker2(vOrAndMarker)))
        {
            result.Add(line);
            i += 1;
        }
        else
        {
            return false;
        }

        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        if (line is not WLine partyLine2 || !IsPartyNameAndRole(partyLine2))
        {
            return false;
        }

        var party2 = MakePartyAndRole(partyLine2);
        result.Add(party2);
        i += 1;
        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        if (line is not WLine afterLine || !IsAfterPartyMarker(afterLine))
        {
            return false;
        }

        result.Add(line);
        enriched = result;
        return true;
    }

    /* this one has two types of parties before the v */
    private static bool TryEnrichMultiLinePartyBlockWithTwoGroupsBeforeV(IBlock[] rest, out List<IBlock> enriched)
    {
        // EWHC/Admin/2015/897
        enriched = null;
        if (rest.Length == 0)
        {
            return false;
        }

        var i = 0;
        var line = rest[i];
        if (line is not WLine beforeLine || !IsBeforePartyMarker(beforeLine))
        {
            return false;
        }

        List<IBlock> result = [line];
        i += 1;
        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        /* between */
        if (line is not WLine betweenMarker2 || !IsBeforePartyMarker2(betweenMarker2))
        {
            return false;
        }

        result.Add(line);
        i += 1;
        if (!TryEnrichPartyNamesWithRoleLabel(rest[i..], out var firstGroupOfParites))
        {
            return false;
        }

        result.AddRange(firstGroupOfParites);
        i += firstGroupOfParites.Count;
        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        /* and */
        if (line is not WLine andMarker || !IsBetweenPartyMarker2(andMarker))
        {
            return false;
        }

        result.Add(line);
        i += 1;
        if (i == rest.Length)
        {
            return false;
        }

        _ = rest[i];
        if (!TryEnrichPartyNamesWithRoleLabel(rest[i..], out var secondGroupOfParites))
        {
            return false;
        }

        result.AddRange(secondGroupOfParites);
        i += secondGroupOfParites.Count;
        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        /* v */
        if (line is not WLine vMarker || !IsBetweenPartyMarker(vMarker))
        {
            return false;
        }

        result.Add(line);
        i += 1;
        if (i == rest.Length)
        {
            return false;
        }

        _ = rest[i];
        if (!TryEnrichPartyNamesWithRoleLabel(rest[i..], out var thirdGroupOfParites))
        {
            return false;
        }

        result.AddRange(thirdGroupOfParites);
        i += thirdGroupOfParites.Count;
        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        if (line is WLine afterLine && IsAfterPartyMarker(afterLine))
        {
            result.Add(line);
            enriched = result;
            return true;
        }

        return false;
    }

    private static bool TryEnrichPartyNamesWithRoleLabel(IBlock[] rest, out List<IBlock> enriched)
    {
        return TryEnrichPartyNamesWithRoleLabel(rest, IsPartyRole, GetPartyRole, out enriched);
    }

    private static bool TryEnrichPartyNamesWithRoleLabel(IBlock[] rest, Func<WLine, bool> test,
        Func<WLine, PartyRole> construct, out List<IBlock> enriched)
    {
        enriched = null;
        var i = 0;
        if (i == rest.Length)
        {
            return false;
        }

        if (rest[i] is not WLine firstPartyLine || !IsPartyName(firstPartyLine))
        {
            return false;
        }

        List<WLine> stack = [firstPartyLine];
        i += 1;
        while (true)
        {
            if (i == rest.Length || rest[i] is not WLine line)
            {
                return false;
            }

            if (test(line))
            {
                var role1 = construct(line);
                enriched =
                [
                    .. stack.Select(l => MakeParty(l, role1)),
                    MakeRole(line, role1)
                ];
                return true;
            }

            if (IsPartyName(line))
            {
                stack.Add(line);
                i += 1;
            }
            else
            {
                return false;
            }
        }
    }

    private static bool IsBeforePartyMarker(WLine line)
    {
        var normalized = line.NormalizedContent;
        if (Regex.IsMatch(normalized, @"^-( -)+$"))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^-+$"))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^_+$"))
        {
            return true;
        }

        return false;
    }

    private static bool IsBeforePartyMarker2(WLine line)
    {
        var normalized = line.NormalizedContent;
        normalized = Regex.Replace(normalized, @"\s+", "").TrimEnd(':', '-');
        if (normalized.Equals("BETWEEN", StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsBeforePartyMarker3(WLine line)
    {
        var normalized = line.NormalizedContent;
        normalized = Regex.Replace(normalized, @"\s+", "").TrimEnd(':');
        if (normalized.Equals("AND BETWEEN", StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsInTheMatterOfSomething(WLine line)
    {
        var lineContents = line.Contents.ToArray();
        if (lineContents.Length != 1)
        {
            return false;
        }

        if (lineContents[0] is not WText wText)
        {
            return false;
        }

        if (Regex.IsMatch(wText.Text, "^IN THE MATTER OF [A-Z]", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(wText.Text, "^RE: [A-Z]", RegexOptions.IgnoreCase)) // EWCA/Crim/2007/14
        {
            return true;
        }

        return false;
    }

    private static bool IsInTheMatterOf1(WLine line)
    {
        var lineContents = line.Contents.ToArray();
        if (lineContents.Length == 1)
        {
            if (lineContents[0] is not WText wText)
            {
                return false;
            }

            return Regex.IsMatch(wText.Text.Trim(), "IN THE MATTER OF", RegexOptions.IgnoreCase);
        }

        if (lineContents.Length == 3)
        {
            // EWCA/Civ/2008/1303
            if (lineContents[0] is not WText wText)
            {
                return false;
            }

            if (lineContents[1] is not WLineBreak)
            {
                return false;
            }

            return Regex.IsMatch(wText.Text.Trim(), "IN THE MATTER OF", RegexOptions.IgnoreCase);
        }

        return false;
    }

    private static bool IsInTheMatterOf2(WLine line)
    {
        var lineContents = line.Contents.ToArray();
        if (lineContents.Length != 1)
        {
            return false;
        }

        if (lineContents[0] is not WText)
        {
            return false;
        }

        return true;
    }

    private static WLine MakeDocTitle(WLine line)
    {
        var lineContents = line.Contents.ToArray();

        var docTitle = new WDocTitle((WText)lineContents[0]);
        var contents = lineContents.Skip(1).Prepend(docTitle);

        return WLine.Make(line, contents);
    }

    private static bool IsPartyName(WLine line)
    {
        if (IsBeforePartyMarker(line)
            || IsBeforePartyMarker2(line)
            || IsBetweenPartyMarker(line)
            || IsBetweenPartyMarker2(line)
            || IsAfterPartyMarker(line)
            || IsPartyRole(line))
        {
            return false;
        }

        var lineContents = line.Contents.ToArray();
        if (lineContents.Length == 0)
        {
            return false;
        }

        if (lineContents.All(inline => inline is WText) &&
            lineContents.Cast<WText>().Any(IsNotBlank))
        {
            return true;
        }

        if (lineContents.All(inline => inline is ITextOrWhitespace) &&
            lineContents.Any(inline => inline is WText wText && IsNotBlank(wText)))
        {
            return true;
        }

        if (lineContents.Length == 1)
        {
            if (lineContents[0] is not WText wText1)
            {
                return false;
            }

            if (IsBlank(wText1))
            {
                return false;
            }

            return true;
        }

        if (lineContents.Length == 2)
        {
            if (lineContents[0] is WTab && lineContents[1] is WText wText2 &&
                IsNotBlank(wText2)) // EWHC/Fam/2017/3707
            {
                return true;
            }

            if (lineContents[0] is WText wText3 && lineContents[1] is WText wText4 &&
                IsNotBlank(wText3) &&
                IsBlank(wText4)) // EWCA/Crim/2014/465
            {
                return true;
            }

            if (lineContents[0] is WText wText5 && lineContents[1] is WText wText6 &&
                Regex.IsMatch(wText5.Text, @"^\(\d\) +$") &&
                IsNotBlank(wText6))
            {
                return true;
            }

            if (lineContents[0] is WText wText7 && lineContents[1] is WText wText8 &&
                Regex.IsMatch(wText7.Text, @"^\d\. +$") &&
                IsNotBlank(wText8)) // EWCA/Civ/2004/993
            {
                return true;
            }

            return false;
        }

        if (lineContents.Length == 3)
        {
            // EWHC/Admin/2012/3928, EWHC/Admin/2007/552
            if (lineContents[0] is not WText)
            {
                return false;
            }

            if (lineContents[1] is not WText wText2)
            {
                return false;
            }

            if (lineContents[2] is not WText)
            {
                return false;
            }

            if (IsNotBlank(wText2))
            {
                return false;
            }

            return true; // not same formatting in EWHC/Admin/2004/1823
        }

        if (lineContents.Length == 4)
        {
            // EWHC/Fam/2017/3707
            if (lineContents[0] is not WTab)
            {
                return false;
            }

            if (lineContents[1] is not WText)
            {
                return false;
            }

            if (lineContents[2] is not WText wText2)
            {
                return false;
            }

            if (IsNotBlank(wText2))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static WLine MakeParty(WLine line, PartyRole? role)
    {
        var lineContents = line.Contents.ToArray();
        if (lineContents.Length == 1)
        {
            var text = (WText)lineContents[0];
            var party = new WParty(text) { Role = role };
            // use MakeOrSplitParty
            return WLine.Make(line, [party]);
        }

        if (lineContents.All(inline => inline is WText) &&
            lineContents.Cast<WText>().Any(IsNotBlank))
        {
            var party = new WParty2(lineContents.Cast<WText>()) { Role = role };
            return WLine.Make(line, [party]);
        }

        if (lineContents.All(inline => inline is ITextOrWhitespace) &&
            lineContents.Any(inline => inline is WText wText && IsNotBlank(wText)))
        {
            var before = lineContents.TakeWhile(inline => inline is not IFormattedText).ToArray();
            var main = lineContents.Skip(before.Length);
            var party = new WParty2(main.Cast<ITextOrWhitespace>()) { Role = role };
            var contents = before.Append(party);
            return WLine.Make(line, contents);
        }

        if (lineContents.Length == 2)
        {
            if (string.IsNullOrWhiteSpace(((WText)lineContents[1]).Text))
            {
                var party = new WParty((WText)lineContents[0]) { Role = role };
                return WLine.Make(line, [party, lineContents[1]]);
            }
            else
            {
                var party = new WParty((WText)lineContents[1]) { Role = role };
                return WLine.Make(line, [lineContents[0], party]);
            }
        }

        if (lineContents.Length == 3)
        {
            var party = new WParty2(lineContents.Cast<IFormattedText>()) { Role = role };
            return WLine.Make(line, [party]);
        }

        if (lineContents.Length == 4)
        {
            var first = (WTab)lineContents[0];
            var party = new WParty2(lineContents.Skip(1).Cast<IFormattedText>()) { Role = role };
            return WLine.Make(line, [first, party]);
        }

        throw new Exception();
    }

    private static WLine MakeRole(WLine line, PartyRole role)
    {
        return WLine.Make(line, [new WRole { Role = role, Contents = line.Contents }]);
    }

    private static readonly Dictionary<string, PartyRole> PartyRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["(APPELLANT)"] = PartyRole.Appellant,
        ["(APPELLANTS)"] = PartyRole.Appellant,
        ["1st Appellant"] = PartyRole.Appellant,
        ["Appellant / Claimant"] = PartyRole.Appellant,
        ["Appellant / Third Defendant"] = PartyRole.Appellant,
        ["Appellant"] = PartyRole.Appellant, // EWCA/Civ/2003/1686
        ["Appellant/ Defendant"] = PartyRole.Appellant,
        ["Appellant/ Respondent"] = PartyRole.Appellant, // [2021] EWCA Civ 1792
        ["Appellant/Appellant"] = PartyRole.Appellant,
        ["Appellant/Applicant"] = PartyRole.Appellant, // [2021] EWCA Crim 1877
        ["Appellant/Claimant"] = PartyRole.Appellant,
        ["Appellant/Defendant"] = PartyRole.Appellant,
        ["Appellant/First Defendant"] = PartyRole.Appellant,
        ["Appellants / Defendants"] = PartyRole.Appellant,
        ["Appellants"] = PartyRole.Appellant,
        ["Appellants/ Claimants"] = PartyRole.Appellant,
        ["Appellants/Claimants"] = PartyRole.Appellant,
        ["Applicant/Appellant"] = PartyRole.Appellant,
        ["Claimant / Appellant"] = PartyRole.Appellant,
        ["Claimant/ Appellant"] = PartyRole.Appellant,
        ["Claimant/Appellant"] = PartyRole.Appellant,
        ["Claimants/ Appellants"] = PartyRole.Appellant,
        ["Claimants/Appellants"] = PartyRole.Appellant, // [2021] EWCA Civ 1799
        ["Defendant/ Appellant"] = PartyRole.Appellant,
        ["Defendant/Appellant"] = PartyRole.Appellant,
        ["Defendants / Appellants"] = PartyRole.Appellant,
        ["Defendants/ Appellants"] = PartyRole.Appellant,
        ["Defendants/Appellants"] = PartyRole.Appellant,
        ["Defendants/Appellants/"] = PartyRole.Appellant,
        ["FIRST DEFENDANT’S SOLICITOR/APPELLANT"] = PartyRole.Appellant, // EWCA/Civ/2006/1032
        ["Respondent/Appellant"] = PartyRole.Appellant,
        ["Third Party/Appellant"] = PartyRole.Appellant, // [2022] EWHC 34 (Ch)

        ["1st Applicant"] = PartyRole.Applicant,
        ["2nd Applicant"] = PartyRole.Applicant,
        ["Applicant"] = PartyRole.Applicant,
        ["Applicant/ Claimant"] = PartyRole.Applicant,
        ["Applicants"] = PartyRole.Applicant,
        ["Applicants/Claimants"] = PartyRole.Applicant,
        ["Claimant/Applicant"] = PartyRole.Applicant,
        ["Defendant/ Applicant"] = PartyRole.Applicant,
        ["Respondent/Applicant"] = PartyRole.Applicant,

        ["(CLAIMANTS)"] = PartyRole.Claimant,
        ["(Claimant)"] = PartyRole.Claimant,
        ["Additional Claimant"] = PartyRole.Claimant,
        ["Claimant / Defendant to Counterclaim"] = PartyRole.Claimant,
        ["Claimant"] = PartyRole.Claimant,
        ["Claimant/Part 20 Defendant"] = PartyRole.Claimant,
        ["Claimants"] = PartyRole.Claimant,
        ["First Claimant"] = PartyRole.Claimant,
        ["Second Claimant"] = PartyRole.Claimant,

        ["(1st DEFENDANT)"] = PartyRole.Defendant,
        ["(2nd DEFENDANT)"] = PartyRole.Defendant,
        ["(3rd DEFENDANT)"] = PartyRole.Defendant,
        ["(DEFENDANTS)"] = PartyRole.Defendant,
        ["(Defendant)"] = PartyRole.Defendant,
        ["(FIRST DEFENDANT)"] = PartyRole.Defendant,
        ["(SECOND DEFENDANT)"] = PartyRole.Defendant,
        ["Applicants/Defendants"] = PartyRole.Defendant,
        ["Defendant / Counterclaimant"] = PartyRole.Defendant,
        ["Defendant"] = PartyRole.Defendant,
        ["Defendant/Part 20 Claimant"] = PartyRole.Defendant,
        ["Defendants"] = PartyRole.Defendant,
        ["First Defendant"] = PartyRole.Defendant,
        ["Second Defendant"] = PartyRole.Defendant,
        ["Third Defendant"] = PartyRole.Defendant,

        ["(INTERESTED PARTIES)"] = PartyRole.InterestedParty,
        ["(INTERESTED PARTY)"] = PartyRole.InterestedParty,
        ["Interested Parties"] = PartyRole.InterestedParty,
        ["Interested Party"] = PartyRole.InterestedParty,
        ["Interested parties"] = PartyRole.InterestedParty,
        ["Second Interested Party"] = PartyRole.InterestedParty,
        ["Third Interested Party"] = PartyRole.InterestedParty,

        ["Intervener"] = PartyRole.Intervener,
        ["Interveners"] = PartyRole.Intervener,

        ["Petitioner"] = PartyRole.Petitioner,
        ["Petitioners"] = PartyRole.Petitioner,

        ["requested person"] = PartyRole.RequestedPerson, // [2022] EWHC 273 (Admin)
        ["requested persons"] = PartyRole.RequestedPerson, // [2022] EWHC 273 (Admin)

        ["requesting state"] = PartyRole.RequestingState,

        ["(RESPONDENT)"] = PartyRole.Respondent,
        ["(RESPONDENTS)"] = PartyRole.Respondent,
        ["1st Respondent"] = PartyRole.Respondent,
        ["2nd Respondent"] = PartyRole.Respondent,
        ["3rd Respondent"] = PartyRole.Respondent, // EWCA/Civ/2012/378
        ["Claimant / Respondent"] = PartyRole.Respondent,
        ["Claimant/ Respondent"] = PartyRole.Respondent,
        ["Claimant/Respondent"] = PartyRole.Respondent,
        ["Claimants/Respondents"] = PartyRole.Respondent,
        ["Clamaints/ Respondents"] = PartyRole.Respondent,
        ["Defendant / Respondent"] = PartyRole.Respondent,
        ["Defendant/ Respondent"] = PartyRole.Respondent,
        ["Defendant/Respondent"] = PartyRole.Respondent,
        ["Defendants/ Respondents"] = PartyRole.Respondent,
        ["Defendants/Respondents"] = PartyRole.Respondent,
        ["First Respondent"] = PartyRole.Respondent,
        ["Fourth Respondent"] = PartyRole.Respondent,
        ["Petitioner/Respondent"] = PartyRole.Respondent,
        ["Respond-ents/ Defendants"] = PartyRole.Respondent,
        ["Respondent / Defendant"] = PartyRole.Respondent,
        ["Respondent"] = PartyRole.Respondent, // EWCA/Civ/2003/1686
        ["Respondent/ Claimant"] = PartyRole.Respondent,
        ["Respondent/ First Defendant"] = PartyRole.Respondent,
        ["Respondent/Claimant"] = PartyRole.Respondent,
        ["Respondent/Defendants"] = PartyRole.Respondent,
        ["Respondent/Petitioner"] = PartyRole.Respondent, // [2021] EWCA Civ 1792
        ["Respondent/Respondent"] = PartyRole.Respondent,
        ["Respondents / Claimants"] = PartyRole.Respondent,
        ["Respondents Second and Third/ Defendants"] = PartyRole.Respondent, // EWCA/Civ/2004/1249
        ["Respondents"] = PartyRole.Respondent,
        ["Respondents/ Defendants"] = PartyRole.Respondent, // EWCA/Civ/2015/377, EWHC/QB/2006/582
        ["Respondents/Claimants"] = PartyRole.Respondent,
        ["Respondents/Defendants"] = PartyRole.Respondent,
        ["Respondents/Respondents"] = PartyRole.Respondent,
        ["Respondnet"] = PartyRole.Respondent, // EWHC/Admin/2010/3393
        ["Second Respondent"] = PartyRole.Respondent,
        ["Third Respondent"] = PartyRole.Respondent,
    };

    private static bool IsPartyRole(string s)
    {
        return PartyRoles.ContainsKey(s) || TryGetPartyRole(s, out _);
    }

    private static bool IsPartyRole(WLine line)
    {
        var normalized = line.NormalizedContent;
        return IsPartyRole(normalized);
    }

    private static PartyRole GetAnyPartyRole(string s)
    {
        return IsPartyRole(s) ? GetPartyRole(s) : throw new Exception();
    }

    private static PartyRole GetPartyRole(string s)
    {
        if (PartyRoles.TryGetValue(s, out var role))
        {
            return role;
        }

        return TryGetPartyRole(s, out role) ? role : throw new Exception();
    }

    private static PartyRole GetPartyRole(WLine line)
    {
        var normalized = line.NormalizedContent;
        return GetPartyRole(normalized);
    }

    private static bool IsPartyNameAndRole(WLine line)
    {
        var lineContents = line.Contents.ToArray();
        if (lineContents.Length >= 3)
        {
            var before = lineContents.SkipLast(3);
            if (!before.All(i => i is WTab))
            {
                return false;
            }

            if (lineContents[^3] is not WText)
            {
                return false;
            }

            if (lineContents[^2] is not WTab)
            {
                return false;
            }

            if (lineContents[^1] is not WText wText2)
            {
                return false;
            }

            var s = Regex.Replace(wText2.Text, @"\s+", " ").Trim();
            if (!IsPartyRole(s))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static WLine MakePartyAndRole(WLine line)
    {
        var lineContents = line.Contents.ToArray();
        if (lineContents.Length >= 3)
        {
            var before = lineContents.SkipLast(3);
            var antiPenult = (WText)lineContents[^3];
            var penult = (WTab)lineContents[^2];
            var last = (WText)lineContents[^1];

            var s = Regex.Replace(last.Text, @"\s+", " ").Trim();
            var role = GetAnyPartyRole(s);

            var contents = before.Concat(
            [
                new WParty(antiPenult.Text, antiPenult.properties) { Role = role },
                penult,
                new WRole { Role = role, Contents = [last] }
            ]);

            return WLine.Make(line, contents);
        }

        throw new Exception();
    }

    private static bool IsBetweenPartyMarker(WLine line)
    {
        var normalized = line.NormalizedContent;
        return IsV(normalized);
    }

    private static bool IsBetweenPartyMarker2(WLine line)
    {
        var normalized = line.NormalizedContent;
        return IsAnd(normalized);
    }

    private static bool IsAfterPartyMarker(WLine line)
    {
        if (IsBeforePartyMarker(line))
        {
            return true;
        }

        var content = line.NormalizedContent;
        return content.StartsWith("Computer Aided Transcript", StringComparison.OrdinalIgnoreCase)
            || content.StartsWith("REPORTING RESTRICTIONS APPLY:", StringComparison.OrdinalIgnoreCase);
    }

    /* tables */

    private WTable EnrichTable(WTable table)
    {
        IEnumerable<WRow> rows = null;
        if (table.TypedRows.Count == 3 && TryEnrichThreeRowsWithNoRoles(table.TypedRows, out var threeRows))
        {
            rows = threeRows;
        }

        rows ??= EnrichRows(table.TypedRows);

        return new WTable(table.Main, table.Properties, table.Grid, rows);
    }

    private List<WRow> EnrichRows(List<WRow> originalRows)
    {
        var enrichedRows = new List<WRow>();
        for (var i = 0; i < originalRows.Count; i++)
        {
            var enrichedRow = EnrichRow(originalRows[i]);

            if (originalRows[i] == enrichedRow && i != originalRows.Count - 1)
            {
                enrichedRow = EnrichRowWithPartyRoleFromNextRow(originalRows[i], originalRows[i + 1]);
            }

            enrichedRows.Add(enrichedRow);
        }

        return enrichedRows;
    }

    private static bool IsCellWithContent(ICell cell)
    {
        return !IsEmptyCell(cell);
    }

    private static bool IsEmptyCell(ICell cell)
    {
        return cell.Contents.All(block => block is WLine line && IsEmptyLine(line));
    }

    private static bool IsEmptyLine(WLine line)
    {
        return string.IsNullOrWhiteSpace(line.NormalizedContent);
    }

    private static bool LineHasContent(WLine l)
    {
        return !IsEmptyLine(l);
    }

    private WRow EnrichRow(WRow row)
    {
        var rowCells = row.Cells.ToArray();
        if (rowCells.Length == 2)
        {
            return EnrichTwoCellRow(row);
        }

        if (rowCells is not [WCell first, WCell second, WCell third] || IsCellWithContent(first))
        {
            return row;
        }

        if (TryGetPartyRole(third, out var role))
        {
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                first,
                EnrichCellWithParty(second, role),
                EnrichCellWithPartyRole(third, role)
            ]);
        }

        if (IsInTheMatterOfSomething(second))
        {
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                first,
                EnrichInTheMatterOfSomething(second),
                third
            ]);
        }

        if (TryGetTwoDifferentRoles(third, out var twoRoles))
        {
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                first,
                EnrichPartyNamesWithTwoRoles(second, twoRoles),
                EnrichPartyTypesWithTwoRoles(third, twoRoles)
            ]);
        }

        return row;
    }

    private WRow EnrichTwoCellRow(WRow row)
    {
        var rowCells = row.Cells.ToArray();
        var first = (WCell)rowCells[0];
        var second = (WCell)rowCells[1];

        if (IsCellWithContent(first) && TryGetPartyRole(second, out var role))
        {
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                EnrichCellWithParty(first, role),
                EnrichCellWithPartyRole(second, role)
            ]);
        }

        return row;

    }

    private bool TryEnrichThreeRowsWithNoRoles(List<WRow> rows, out WRow[] enrichedRows)
    {
        // EWCA/Crim/2007/854, EWCA/Crim/2014/465
        enrichedRows = null;
        var firstRowCells = rows[0].Cells.ToArray();
        if (firstRowCells.Length != 3)
        {
            return false;
        }

        var secondRowCells = rows[1].Cells.ToArray();
        if (secondRowCells.Length != 3)
        {
            return false;
        }

        var thirdRowCells = rows[2].Cells.ToArray();
        if (thirdRowCells.Length != 3)
        {
            return false;
        }

        if (IsCellWithContent(firstRowCells[0]))
        {
            return false;
        }

        if (IsCellWithContent(firstRowCells[^1]))
        {
            return false;
        }

        if (IsCellWithContent(secondRowCells[0]))
        {
            return false;
        }

        if (IsCellWithContent(secondRowCells[^1]))
        {
            return false;
        }

        if (IsCellWithContent(thirdRowCells[0]))
        {
            return false;
        }

        if (IsCellWithContent(thirdRowCells[^1]))
        {
            return false;
        }

        var middle1 = rows[0].TypedCells[1];
        var middle2 = rows[1].TypedCells[1];
        var middle3 = rows[2].TypedCells[1];
        if (!middle1.Contents.All(block => block is WLine line &&
                                           (IsEmptyLine(line) || (IsPartyName(line) && !IsPartyRole(line)))))
        {
            return false;
        }

        if (!middle2.Contents.All(block => block is WLine line &&
                                           (IsEmptyLine(line) || IsBetweenPartyMarker(line) ||
                                            IsBetweenPartyMarker2(line))))
        {
            return false;
        }

        if (middle2.Contents.OfType<WLine>().Count(LineHasContent) != 1)
        {
            return false;
        }

        if (!middle3.Contents.All(block => block is WLine line &&
                                           (IsEmptyLine(line) || (IsPartyName(line) && !IsPartyRole(line)))))
        {
            return false;
        }

        var newMiddle1 = new WCell(middle1.Row, middle1.Props,
            middle1.Contents.Cast<WLine>()
                   .Select(line => IsEmptyLine(line) ? line : MakeParty(line, PartyRole.BeforeTheV)));
        var newMiddle3 = new WCell(middle3.Row, middle3.Props,
            middle3.Contents.Cast<WLine>()
                   .Select(line => IsEmptyLine(line) ? line : MakeParty(line, PartyRole.AfterTheV)));
        enrichedRows =
        [

            new(rows[0].Table, rows[0].TablePropertyExceptions, rows[0].Properties,
            [
                    rows[0].TypedCells[0],
                    newMiddle1,
                    rows[0].TypedCells[^1]
            ]),

            rows[1],
            new(rows[2].Table, rows[2].TablePropertyExceptions, rows[2].Properties,
            [
                    rows[2].TypedCells[0],
                    newMiddle3,
                    rows[2].TypedCells[^1]
            ])
        ];
        return true;
    }

    private WRow EnrichRowWithPartyRoleFromNextRow(WRow row, WRow next)
    {
        if (row.Cells.ToArray() is [WCell thisRowFirstCell, WCell thisRowMiddleCell, WCell thisRowLastCell]
            && next.Cells.ToArray() is [WCell nextRowFirstCell, WCell nextRowMiddleCell, WCell nextRowLastCell]
            && IsEmptyCell(thisRowFirstCell) && IsCellWithContent(thisRowMiddleCell) && IsEmptyCell(thisRowLastCell)
            && IsEmptyCell(nextRowFirstCell) && IsEmptyCell(nextRowMiddleCell) && IsCellWithContent(nextRowLastCell)
            && TryGetPartyRole(nextRowLastCell, out var role))
        {
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                thisRowFirstCell,
                EnrichCellWithParty(thisRowMiddleCell, role),
                thisRowLastCell
            ]);
        }

        return row;
    }

    public static bool TryGetPartyRole(string s, out PartyRole role)
    {
        if (s.Split('/', 2) is [var beforeSlash, var afterSlash]
            && !string.IsNullOrWhiteSpace(beforeSlash) && !string.IsNullOrWhiteSpace(afterSlash))
        {
            return TryGetPartyRoleForCombinedLabels(beforeSlash, afterSlash, out role);
        }

        if (s.Split(" and ", 2) is [var beforeAnd, var afterAnd]
            && !string.IsNullOrWhiteSpace(beforeAnd) && !string.IsNullOrWhiteSpace(afterAnd))
        {
            return TryGetPartyRoleForCombinedLabels(beforeAnd, afterAnd, out role);
        }

        return TryGetPartyRoleForSingleLabel(s, out role);
    }

    public static bool TryGetPartyRole(WCell cell, out PartyRole role)
    {
        var lineContents = cell.Contents
                               .OfType<WLine>()
                               .Where(LineHasContent)
                               .Select(l => l.NormalizedContent.ToLower())
                               .ToArray();
        switch (lineContents)
        {
            case [var one] when TryGetPartyRole(one, out role):
                return true;

            case ["defendant/", var two] when two.EndsWith("claimant"): // EWHC/Ch/2008/2079
                role = PartyRole.Defendant;
                return true;

            case ["claimant/", var two] when two.EndsWith("defendant"): // EWHC/Ch/2008/2079
                role = PartyRole.Claimant;
                return true;

            case ["respondents", var two] when two.StartsWith("respondent"): // EWHC/Fam/2013/1956
                role = PartyRole.Respondent;
                return true;

            case [var one, var two] when TwoLinePartyRoles.TryGetValue((one, two), out role)
                || TryGetPartyRoleForCombinedLabels(one, two, out role):
                return true;

            case ["defendants", "part 20 claimant/", "appellant"]:
                role = PartyRole.Appellant;
                return true;

            case ["respondents", "appellant", "respondent"]: // EWCA/Civ/2010/180
                role = PartyRole.Respondent;
                return true;

            case { Length: >= 2 } when cell.Contents.All(block => block is WLine):
                foreach (var (pattern, patternRole) in NLinePartyRolePatterns)
                {
                    if (lineContents.All(pattern.IsMatch))
                    {
                        role = patternRole;
                        return true;
                    }
                }

                role = default;
                return false;

            default:
                role = default;
                return false;
        }
    }

    private static bool TryGetTwoDifferentRoles(WCell cell, out (PartyRole first, PartyRole second) roles)
    {
        var linesWithContent = cell.Contents.OfType<WLine>().Where(LineHasContent).ToArray();
        if (linesWithContent.Length == 2
            && TryGetPartyRole(linesWithContent[0].NormalizedContent, out var role1)
            && TryGetPartyRole(linesWithContent[1].NormalizedContent, out var role2)
            && role1 != role2)
        {
            roles = (role1, role2);
            return true;
        }

        roles = default;
        return false;
    }

    private static WCell EnrichCellWithPartyRole(WCell cell, PartyRole role)
    {
        return new WCell(cell.Row, cell.Props, cell.Contents.Cast<WLine>()
                                                   .Select(line => IsEmptyLine(line)
                                                       ? line
                                                       : WLine.Make(line,
                                                       [
                                                           new WRole { Role = role, Contents = line.Contents }
                                                       ])));
    }

    private static readonly Dictionary<(string one, string two), PartyRole> TwoLinePartyRoles =
        new(new OrdinalIgnoreCaseTupleComparer())
        {
            [("Defendant/", "Appellant")] = PartyRole.Appellant, // EWCA/Civ/2011/1383
            [("Claimant/", "Appellant")] = PartyRole.Appellant, // EWCA/Civ/2011/1277
            [("Appellants/", "Defendants")] = PartyRole.Appellant,
            [("Appellants/", "Defendants & Counterclaimants")] = PartyRole.Appellant, // EWCA/Civ/2017/97
            [("Appellants/", "Claimants")] = PartyRole.Appellant, // EWCA/Civ/2015/377
            [("Appellants", "Claimants")] = PartyRole.Appellant, // EWCA/Civ/2018/601
            [("Appellant/", "Claimant")] = PartyRole.Appellant, // EWHC/Ch/2017/541
            [("Appellant", "/Claimant")] = PartyRole.Appellant, // [2021] EWHC 3453 (QB)
            [("Appellant/", "Defendant")] = PartyRole.Appellant, // EWHC/QB/2013/196
            [("Claimants/", "Appellants")] = PartyRole.Appellant, // EWHC/Admin/2016/321
            [("Defendants/", "Appellants")] = PartyRole.Appellant, // EWCA/Civ/2004/277
            [("Respondent/", "Appellant")] = PartyRole.Appellant, // [2021] EWCA Civ 1961

            [("Defendant/", "Applicant")] = PartyRole.Applicant, // EWHC/Ch/2017/916
            [("Defendants/", "Applicants")] = PartyRole.Applicant, // [2021] EWHC 2684 (Comm)

            [("First Defendant", "Second Defendant")] = PartyRole.Defendant, // EWHC/Admin/2010/2
            [("Defendant/Part 20 Claimant", "Part 20 Claimant")] = PartyRole.Defendant, // EWHC/Ch/2003/812
            [("1st Defendant/Part 20 Claimant", "2nd Defendant/Part 20 Defendant")] =
                PartyRole.Defendant, // EWHC/QB/2004/1260
            [("Defendant/", "Cross appellant")] = PartyRole.Defendant, // EWHC/QB/2013/652 ??? other role is Appellant

            [("Claimant/", "Respondent")] = PartyRole.Respondent, // EWCA/Civ/2008/183
            [("Claimants/", "Respondents")] = PartyRole.Respondent, // EWHC/Ch/2017/916
            [("Respondent/", "Claimant")] = PartyRole.Respondent, // EWCA/Civ/2017/97
            [("Respondent", "Intervener")] = PartyRole.Respondent, // EWCA/Civ/2016/176
            [("Respondent/", "Defendant")] = PartyRole.Respondent, // EWHC/Ch/2017/541
            [("Respondent", "Defendant")] = PartyRole.Respondent, // EWCA/Civ/2018/601
            [("1st Respondent", "/Defendant")] = PartyRole.Respondent, // [2021] EWHC 3453 (QB)
            [("Defendant/", "Respondent")] = PartyRole.Respondent, // EWHC/Admin/2015/1639
            [("Defendants/", "Respondents")] = PartyRole.Respondent, // EWHC/Admin/2016/321
            [("1st Respondent", "2nd Respondent")] = PartyRole.Respondent, // EWHC/Fam/2017/364, EWHC/Fam/2013/1864?
            [("1st Respondent", "2ndRespondent")] = PartyRole.Respondent, // EWCA/Civ/2011/1253
            [("Applicant/", "Respondent")] = PartyRole.Respondent, // [2021] EWCA Civ 1725
            [("Appellant/", "Respondent")] = PartyRole.Respondent // [2020] EWHC 3409 (QB)
        };

    private static readonly (Regex Pattern, PartyRole Role)[] NLinePartyRolePatterns =
    [
        (new Regex(@"^\d(st|nd|rd|th)? Defendant$", RegexOptions.IgnoreCase), PartyRole.Defendant),
        (new Regex(@"^(First|Second|Third|Fourth) Defendant$", RegexOptions.IgnoreCase),
            PartyRole.Defendant), // EWHC/Fam/2003/365
        (new Regex(@"^\d(st|nd|rd|th)? Appellant$", RegexOptions.IgnoreCase), PartyRole.Appellant),
        (new Regex(@"^(\d(st|nd|rd|th)? ?)?Respondent$", RegexOptions.IgnoreCase),
            PartyRole.Respondent), // EWFC/HCJ/2014/34, no space in EWHC/Fam/2013/1864
        (new Regex(@"^(First|Second|Third|Fourth) Respondent$", RegexOptions.IgnoreCase), PartyRole.Respondent)
    ];

    private WCell EnrichCellWithParty(WCell cell, PartyRole role)
    {
        var contents = cell.Contents.Select(block =>
            {
                return block switch
                {
                    WOldNumberedParagraph wOldNumberedParagraph =>
                        wOldNumberedParagraph.Contents.ToArray() is [WText wText]
                            ? new WOldNumberedParagraph(wOldNumberedParagraph, [new WParty(wText) { Role = role }])
                            : wOldNumberedParagraph, // EWCA/Civ/2015/455

                    WLine line => EnrichLineWithParty(line, role),

                    _ => block
                };
            }
        ).ToArray();
        return new WCell(cell.Row, cell.Props, contents);
    }

    private static WLine EnrichLineWithParty(WLine line, PartyRole role)
    {
        var lineContents = line.Contents.ToArray();

        return lineContents switch
        {
            [] => line,

            [WText { Text: "SECRETARY OF STATE " }, WLineBreak, WText] // [2021] EWCA Civ 1876
                => WLine.Make(line, [new WParty2(lineContents.Cast<ITextOrWhitespace>()) { Role = role }]),

            _ when lineContents.OfType<WText>().Count(wText => IsNotBlank(wText)
                    && !IsInBrackets(wText.Text)
                    && !IsConnectorText(wText.Text)) == 1
                => WLine.Make(line, lineContents.SelectMany(inline => EnrichWTextWithParties(inline, role)).ToArray()),

            _ when lineContents.OfType<WText>()
                               .Count(wText => IsNotBlank(wText) && !IsInBrackets(wText.Text)
                                   && !IsConnectorText(wText.Text)) > 1
                => WLine.Make(line, [new WParty2(lineContents.Cast<WText>()) { Role = role }]),

            _ => line
        };
    }

    private static IEnumerable<IInline> EnrichWTextWithParties(IInline inline, PartyRole role)
    {
        if (inline is WText text)
        {
            // Is this a case of two party names in one line - ewhc/admin/2022/273
            if (text.Text.StartsWith("(1)") && text.Text.Contains("(2)"))
            {
                var i = text.Text.IndexOf("(2)", StringComparison.Ordinal);
                return
                [
                    new WParty(text.Text[..i], text.properties) { Role = role },
                    new WParty(text.Text[i..], text.properties) { Role = role }
                ];
            }

            // Make sure this is the wText with a party name in it rather than some connection or descriptive text
            if (IsNotBlank(text) && !IsConnectorText(text.Text) && !IsInBrackets(text.Text))
            {
                return [new WParty(text.Text, text.properties) { Role = role }];
            }
        }

        return [inline];
    }

    private static bool IsNotBlank(WText wText)
    {
        return !IsBlank(wText);
    }

    private static bool IsBlank(WText wText)
    {
        return string.IsNullOrWhiteSpace(wText.Text);
    }

    /// <summary>
    /// Returns true if this is a "v" string
    /// Trims ' ', '-', '–' characters and uses case insensitive comparison
    /// </summary>
    private static bool IsV(string s)
    {
        return s.Trim(' ', '-', '–').Equals("v", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Returns true if this is an "and" string
    /// Trims ' ', '-', '–' characters and uses case insensitive comparison
    /// </summary>
    private static bool IsAnd(string s)
    {
        return s.Trim(' ', '-', '–').Equals("and", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Returns true if this is a string enclosed in brackets unless there are nested brackets
    /// "(some string in brackets)   " => true
    /// "(3) Appellant CAKE (Cats Against Kipper Exploitation)" => false
    /// </summary>
    private static bool IsInBrackets(string s)
    {
        return Regex.IsMatch(s, @"^\s*\([^()]+\)\s*$", RegexOptions.IgnoreCase);
    }

    private static bool IsConnectorText(string s)
    {
        return Regex.IsMatch(s, @"^(\s|_|-|–|\d|\.|\+|&|and)*$", RegexOptions.IgnoreCase);
    }

    private WCell EnrichPartyNamesWithTwoRoles(WCell cell, (PartyRole first, PartyRole second) roles)
    {
        var contents = new List<IBlock>();
        var firstPartyFound = false;
        var andFound = false;
        var secondPartyFound = false;
        foreach (var block in cell.Contents)
        {
            if (block is not WLine line)
            {
                return cell;
            }

            if (IsEmptyLine(line))
            {
                contents.Add(block);
                continue;
            }

            var lineContents = line.Contents.ToArray();
            if (lineContents.Length == 1)
            {
                var first = lineContents[0];
                if (first is not WText wText)
                {
                    return cell;
                }

                if (IsBlank(wText))
                {
                    contents.Add(block);
                    continue;
                }

                if (IsInBrackets(wText.Text))
                {
                    contents.Add(block);
                    continue;
                }

                if (IsAnd(wText.Text))
                {
                    andFound = true;
                    contents.Add(block);
                    continue;
                }

                if (andFound)
                {
                    secondPartyFound = true;
                    var party = new WParty(wText) { Role = roles.second };
                    var newLine = WLine.Make(line, [party]);
                    contents.Add(newLine);
                }
                else
                {
                    firstPartyFound = true;
                    var party = new WParty(wText) { Role = roles.first };
                    var newLine = WLine.Make(line, [party]);
                    contents.Add(newLine);
                }
            }
            else if (lineContents.Length == 2)
            {
                // EWHC/Admin/2016/176

                var first = lineContents[0];
                var second = lineContents[1];
                if (first is not WText wText1)
                {
                    contents.Add(block);
                    continue;
                }

                if (second is not WText wText2)
                {
                    contents.Add(block);
                    continue;
                }

                if (IsNotBlank(wText1))
                {
                    contents.Add(block);
                    continue;
                }

                if (IsBlank(wText2))
                {
                    contents.Add(block);
                    continue;
                }

                if (IsInBrackets(wText2.Text))
                {
                    contents.Add(block);
                    continue;
                }

                if (IsAnd(wText2.Text))
                {
                    andFound = true;
                    contents.Add(block);
                    continue;
                }

                if (andFound)
                {
                    secondPartyFound = true;
                    var party = new WParty(wText2) { Role = roles.second };
                    var newLine = WLine.Make(line, [first, party]);
                    contents.Add(newLine);
                }
                else
                {
                    firstPartyFound = true;
                    var party = new WParty(wText2) { Role = roles.first };
                    var newLine = WLine.Make(line, [first, party]);
                    contents.Add(newLine);
                }
            }
            else
            {
                contents.Add(block);
            }
        }

        if (!firstPartyFound)
        {
            return cell;
        }

        if (!andFound)
        {
            return cell;
        }

        if (!secondPartyFound)
        {
            return cell;
        }

        return new WCell(cell.Row, cell.Props, contents);
    }

    private WCell EnrichPartyTypesWithTwoRoles(WCell cell, (PartyRole first, PartyRole second) roles)
    {
        if (cell.Contents.Any(block => block is not WLine))
            return cell;

        var contents = new List<IBlock>();
        var firstPartyFound = false;
        var emptyAfterFirstFound = false;

        foreach (var line in cell.Contents.Cast<WLine>())
        {
            if (IsEmptyLine(line))
            {
                if (firstPartyFound)
                {
                    emptyAfterFirstFound = true;
                }

                contents.Add(line);
                continue;
            }

            if (emptyAfterFirstFound)
            {
                contents.Add(WLine.Make(line, [new WRole { Contents = line.Contents, Role = roles.second }]));
            }
            else
            {
                firstPartyFound = true;
                contents.Add(WLine.Make(line, [new WRole { Contents = line.Contents, Role = roles.first }]));
            }
        }

        if (emptyAfterFirstFound)
        {
            return new WCell(cell.Row, cell.Props, contents);
        }

        return cell;
    }

    private static bool IsInTheMatterOfSomething(WCell cell)
    {
        var cellContents = cell.Contents.ToArray();
        return cellContents is [WLine line] && IsInTheMatterOfSomething(line);
    }

    private WCell EnrichInTheMatterOfSomething(WCell cell)
    {
        var line = MakeDocTitle((WLine)cell.Contents.First());
        return new WCell(cell.Row, cell.Props, [line]);
    }

    private WLine EnrichLineWithDocTitle(WLine line)
    {
        return line.Contents.ToArray() switch
        {
            [WText wText] when wText.Text.StartsWith("IN THE MATTER OF ", StringComparison.InvariantCultureIgnoreCase)
                => WLine.Make(line, [new WDocTitle(wText)]),
            _ => line
        };
    }

    private static readonly HashSet<string> PrefixesToStrip =
    [
        "1st ",
        "2nd ",
        "3rd ",
        "4th ",
        "5th ",
        "6th ",
        "First ",
        "Second ",
        "Third ",
        "Fourth ",
        "Fifth ",
        "Sixth ",
        "Inquiry " // [2022] EWHC 189 (Pat)
    ];

    private static bool TryGetPartyRoleForSingleLabel(string s, out PartyRole role)
    {
        s = Regex.Replace(s, @"\s+", " ").Trim(' ', '/', '(', ')');
        if (s.StartsWith("Part 20 ", StringComparison.OrdinalIgnoreCase))
        {
            s = s.Substring(8);
        }

        if (s.Equals("Third Party", StringComparison.OrdinalIgnoreCase))
        {
            role = PartyRole.ThirdParty;
            return true;
        }

        if (PrefixesToStrip.Any(prefix => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            s = s.Substring(s.IndexOf(' ') + 1);
        }

        return PartyRoles.TryGetValue(s, out role);
    }

    private static bool TryGetPartyRoleForCombinedLabels(string s1, string s2, out PartyRole role)
    {
        if (TryGetPartyRole(s1, out var role1)
            && TryGetPartyRole(s2, out var role2))
        {
            if (role1 == PartyRole.Appellant || role2 == PartyRole.Appellant)
            {
                role = PartyRole.Appellant;
                return true;
            }

            if (role1 == PartyRole.Respondent || role2 == PartyRole.Respondent)
            {
                role = PartyRole.Respondent;
                return true;
            }

            if (role1 == PartyRole.Claimant && role2 == PartyRole.Defendant) // [2022] EWCA Civ 102
            {
                role = PartyRole.Claimant;
                return true;
            }

            if (role1 == PartyRole.Defendant && role2 == PartyRole.Applicant) // [2019] EWHC 3963 (QB)
            {
                role = PartyRole.Applicant;
                return true;
            }
        }

        role = default;
        return false;
    }
}
