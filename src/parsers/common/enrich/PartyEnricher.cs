using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse;

// there are "third" paries in EWCA/Civ/2015/631

internal class PartyEnricher : Enricher
{
    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        var before = blocks.ToArray();
        var after = new List<IBlock>();
        var i = 0;
        while (i < before.Length)
        {
            if (IsInTheMatterOf3(before, i))
            {
                var enriched3 = EnrichInTheMatterOf3(before, i);
                after.AddRange(enriched3);
                i += 3;
                break;
            }

            if (IsInTheMatterOf4(before, i))
            {
                var enriched4 = EnrichInTheMatterOf4(before, i);
                after.AddRange(enriched4);
                i += 4;
                break;
            }

            if (IsThreeLinePartyBlock(before, i))
            {
                var enriched3 = EnrichThreeLinePartyBlock(before, i);
                after.AddRange(enriched3);
                i += 3;
                break;
            }

            if (IsFourLinePartyBlock(before, i))
            {
                var enriched4 = EnrichFourLinePartyBlock(before, i);
                after.AddRange(enriched4);
                i += 4;
                break;
            }

            if (IsFiveLinePartyBlock(before, i))
            {
                var enriched5 = EnrichFiveLinePartyBlock(before, i);
                after.AddRange(enriched5);
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

            var block = before[i];
            var enriched1 = EnrichBlock(block);
            after.Add(enriched1);
            i += 1;
        }

        after.AddRange(before.Skip(i));
        return after;
    }

    private static bool IsInTheMatterOf3(IBlock[] before, int i)
    {
        // EWCA/Civ/2008/1303
        if (i > before.Length - 3)
        {
            return false;
        }

        var line1 = before[i];
        var line2 = before[i + 1];
        var line3 = before[i + 2];
        return
            IsBeforePartyMarker(line1) &&
            IsInTheMatterOf1(line2) &&
            IsAfterPartyMarker(line3);
    }

    private static List<IBlock> EnrichInTheMatterOf3(IBlock[] before, int i)
    {
        return
        [
            before[i],
            MakeDocTitle(before[i + 1]),
            before[i + 2]
        ];
    }

    private static bool IsInTheMatterOf4(IBlock[] before, int i)
    {
        // EWHC/QB/2017/2921, EWHC/Ch/2006/3549
        if (i > before.Length - 4)
        {
            return false;
        }

        var line1 = before[i];
        var line2 = before[i + 1];
        var line3 = before[i + 2];
        var line4 = before[i + 3];
        _ = IsBeforePartyMarker(line1);
        _ = IsInTheMatterOf1(line2);
        _ = IsInTheMatterOf2(line3);
        _ = IsAfterPartyMarker(line4);
        return
            IsBeforePartyMarker(line1) &&
            IsInTheMatterOf1(line2) &&
            IsInTheMatterOf2(line3) &&
            IsAfterPartyMarker(line4);
    }

    private static List<IBlock> EnrichInTheMatterOf4(IBlock[] before, int i)
    {
        return
        [
            before[i],
            MakeDocTitle(before[i + 1]),
            MakeDocTitle(before[i + 2]),
            before[i + 3]
        ];
    }

    /* three and four */

    private static bool IsRexOrRegina(WLine line)
    {
        var content = line.NormalizedContent;
        if (content == "REX")
        {
            return true;
        }

        if (content == "R E X")
        {
            return true;
        }

        if (content == "REGINA")
        {
            return true;
        }

        if (content == "R E G I N A")
        {
            return true;
        }

        return false;
    }

    private static bool IsThreeLinePartyBlock(IBlock[] before, int i)
    {
        if (i > before.Length - 4)
        {
            return false;
        }

        var block1 = before[i];
        var block2 = before[i + 1];
        var block3 = before[i + 2];
        var block4 = before[i + 3];
        if (block1 is not WLine line1)
        {
            return false;
        }

        if (!IsRexOrRegina(line1))
        {
            return false;
        }

        if (!IsBetweenPartyMarker(block2))
        {
            return false;
        }

        if (!IsPartyName(block3))
        {
            return false;
        }

        if (!IsAfterPartyMarker(block4))
        {
            return false;
        }

        return true;
    }

    private static List<IBlock> EnrichThreeLinePartyBlock(IBlock[] before, int i)
    {
        var line1 = before[i];
        var line2 = before[i + 1];
        var line3 = before[i + 2];
        return
        [
            MakeParty(line1, PartyRole.BeforeTheV),
            line2,
            MakeParty(line3, PartyRole.AfterTheV)
        ];
    }

    private static bool IsFourLinePartyBlock(IBlock[] before, int i)
    {
        if (i > before.Length - 5)
        {
            return false;
        }

        var block1 = before[i];
        var block2 = before[i + 1];
        var block3 = before[i + 2];
        var block4 = before[i + 3];
        var block5 = before[i + 4];
        if (block1 is not WLine line1)
        {
            return false;
        }

        if (!IsRexOrRegina(line1))
        {
            return false;
        }

        if (!IsBetweenPartyMarker(block2))
        {
            return false;
        }

        if (!IsPartyName(block3))
        {
            return false;
        }

        if (!IsPartyName(block4))
        {
            return false;
        }

        if (!IsAfterPartyMarker(block5))
        {
            return false;
        }

        return true;
    }

    private static List<IBlock> EnrichFourLinePartyBlock(IBlock[] before, int i)
    {
        var line1 = before[i];
        var line2 = before[i + 1];
        var line3 = before[i + 2];
        var line4 = before[i + 3];
        return
        [
            MakeParty(line1, PartyRole.BeforeTheV),
            line2,
            MakeParty(line3, PartyRole.AfterTheV),
            MakeParty(line4, PartyRole.AfterTheV)
        ];
    }

    /* five */

    private static bool IsFiveLinePartyBlock(IBlock[] before, int i)
    {
        if (i > before.Length - 5)
        {
            return false;
        }

        var line1 = before[i];
        var line2 = before[i + 1];
        var line3 = before[i + 2];
        var line4 = before[i + 3];
        var line5 = before[i + 4];
        return
            IsBeforePartyMarker(line1) &&
            IsPartyName(line2) &&
            (IsBetweenPartyMarker(line3) || IsBetweenPartyMarker2(line3)) &&
            IsPartyName(line4) &&
            IsAfterPartyMarker(line5);
    }

    private static List<IBlock> EnrichFiveLinePartyBlock(IBlock[] before, int i)
    {
        return
        [
            before[i],
            MakeParty(before[i + 1], PartyRole.BeforeTheV),
            before[i + 2],
            MakeParty(before[i + 3], PartyRole.AfterTheV),
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
        if (!IsBeforePartyMarker(line) && !IsBeforePartyMarker2(line) && !(successive && IsBeforePartyMarker3(line)))
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
        if (line is WLine inPrivate && inPrivate.NormalizedContent == "IN PRIVATE")
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

        if (IsBeforePartyMarker2(line))
        {
            result.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return false;
            }

            _ = rest[i];
        }

        if (!TryEnrichFirstPartyGroup(rest[i..], out var firstGroupOfParites))
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
        if (IsBetweenPartyMarker(line) || IsBetweenPartyMarker2(line))
        {
            result.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return false;
            }

            _ = rest[i];
        }

        if (!TryEnrichSecondPartyGroup(rest[i..], out var secondGroupOfParites))
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

        if (IsBetweenPartyMarker2(line))
        {
            result.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return false;
            }

            _ = rest[i];
        }

        if (TryEnrichSecondPartyGroup(rest[i..], out var thirdGroupOfParites))
        {
            result.AddRange(thirdGroupOfParites);
            i += thirdGroupOfParites.Count;
        }

        if (TryEnrichSecondPartyGroup(rest[i..], out var fourthGroupOfParites))
        {
            result.AddRange(fourthGroupOfParites);
            i += fourthGroupOfParites.Count;
        }

        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        if (IsAfterPartyMarker(line))
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
        if (!IsBeforePartyMarker(line) && !IsBeforePartyMarker2(line))
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
        if (IsBeforePartyMarker2(line))
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

        if (!IsPartyNameAndRole(line))
        {
            return false;
        }

        var party1 = MakePartyAndRole(line);
        result.Add(party1);
        i += 1;
        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        if (IsBetweenPartyMarker(line) || IsBetweenPartyMarker2(line))
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
        if (!IsPartyNameAndRole(line))
        {
            return false;
        }

        var party2 = MakePartyAndRole(line);
        result.Add(party2);
        i += 1;
        if (i == rest.Length)
        {
            return false;
        }

        line = rest[i];
        if (!IsAfterPartyMarker(line))
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
        if (!IsBeforePartyMarker(line))
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
        if (!IsBeforePartyMarker2(line))
        {
            return false;
        }

        result.Add(line);
        i += 1;
        if (!TryEnrichFirstPartyGroup(rest[i..], out var firstGroupOfParites))
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
        if (!IsBetweenPartyMarker2(line))
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
        if (!TryEnrichFirstPartyGroup(rest[i..], out var secondGroupOfParites))
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
        if (!IsBetweenPartyMarker(line))
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
        if (!TryEnrichSecondPartyGroup(rest[i..], out var thirdGroupOfParites))
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
        if (IsAfterPartyMarker(line))
        {
            result.Add(line);
            enriched = result;
            return true;
        }

        return false;
    }

    private static bool TryEnrichFirstPartyGroup(IBlock[] rest, out List<IBlock> enriched)
    {
        return TryEnrichPartyNamesWithRoleLabel(rest, IsFirstPartyType, GetFirstPartyRole, out enriched);
    }

    private static bool TryEnrichSecondPartyGroup(IBlock[] rest, out List<IBlock> enriched)
    {
        return TryEnrichPartyNamesWithRoleLabel(rest, IsSecondPartyType, GetSecondPartyRole, out enriched);
    }

    private static bool TryEnrichPartyNamesWithRoleLabel(IBlock[] rest, Func<IBlock, bool> test, Func<IBlock, PartyRole> construct, out List<IBlock> enriched)
    {
        enriched = null;
        var i = 0;
        if (i == rest.Length)
        {
            return false;
        }

        var line = rest[i];
        if (!IsPartyName(line))
        {
            return false;
        }

        List<IBlock> stack = [line];
        i += 1;
        while (true)
        {
            if (i == rest.Length)
            {
                return false;
            }

            line = rest[i];
            if (test(line))
            {
                var role1 = construct(line);
                enriched =
                [
                    .. stack.Select(block => MakeParty(block, role1)),
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

    private static bool IsBeforePartyMarker(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

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

    private static bool IsBeforePartyMarker2(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

        var normalized = line.NormalizedContent;
        normalized = Regex.Replace(normalized, @"\s+", "").TrimEnd(':', '-');
        if (normalized.Equals("BETWEEN", StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsBeforePartyMarker3(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

        var normalized = line.NormalizedContent;
        normalized = Regex.Replace(normalized, @"\s+", "").TrimEnd(':');
        if (normalized.Equals("AND BETWEEN", StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsInTheMatterOfSomething(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

        var lineContents = line.Contents.ToArray();
        if (lineContents.Length != 1)
        {
            return false;
        }

        if (lineContents[0] is not WText wText)
        {
            return false;
        }

        if (Regex.IsMatch(wText.Text, @"^IN THE MATTER OF [A-Z]", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(wText.Text, @"^RE: [A-Z]")) // EWCA/Crim/2007/14
        {
            return true;
        }

        return false;
    }

    private static bool IsInTheMatterOf1(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

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

    private static bool IsInTheMatterOf2(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

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

    private static WLine MakeDocTitle(IBlock block)
    {
        var line = (WLine)block;
        var lineContents = line.Contents.ToArray();

        var docTitle = new WDocTitle((WText)lineContents[0]);
        var contents = lineContents.Skip(1).Prepend(docTitle);

        return WLine.Make(line, contents);
    }

    private static bool IsPartyName(IBlock block)
    {
        if (IsBeforePartyMarker(block))
        {
            return false;
        }

        if (IsBeforePartyMarker2(block))
        {
            return false;
        }

        if (IsBetweenPartyMarker(block))
        {
            return false;
        }

        if (IsBetweenPartyMarker2(block))
        {
            return false;
        }

        if (IsAfterPartyMarker(block))
        {
            return false;
        }

        if (IsFirstPartyType(block))
        {
            return false;
        }

        if (IsSecondPartyType(block))
        {
            return false;
        }

        if (block is not WLine line)
        {
            return false;
        }

        var lineContents = line.Contents.ToArray();
        if (lineContents.Length == 0)
        {
            return false;
        }

        if (lineContents.All(inline => inline is WText) &&
            lineContents.Cast<WText>().Any(wText => !string.IsNullOrWhiteSpace(wText.Text)))
        {
            return true;
        }

        if (lineContents.All(inline => inline is ITextOrWhitespace) &&
            lineContents.Any(inline => inline is WText wText && !string.IsNullOrWhiteSpace(wText.Text)))
        {
            return true;
        }

        if (lineContents.Length == 1)
        {
            if (lineContents[0] is not WText wText1)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(wText1.Text))
            {
                return false;
            }

            return true;
        }

        if (lineContents.Length == 2)
        {
            if (lineContents[0] is WTab && lineContents[1] is WText wText2 &&
                !string.IsNullOrWhiteSpace(wText2.Text)) // EWHC/Fam/2017/3707
            {
                return true;
            }

            if (lineContents[0] is WText wText3 && lineContents[1] is WText wText4 &&
                !string.IsNullOrWhiteSpace(wText3.Text) &&
                string.IsNullOrWhiteSpace(wText4.Text)) // EWCA/Crim/2014/465
            {
                return true;
            }

            if (lineContents[0] is WText wText5 && lineContents[1] is WText wText6 &&
                Regex.IsMatch(wText5.Text, @"^\(\d\) +$") &&
                !string.IsNullOrWhiteSpace(wText6.Text))
            {
                return true;
            }

            if (lineContents[0] is WText wText7 && lineContents[1] is WText wText8 &&
                Regex.IsMatch(wText7.Text, @"^\d\. +$") &&
                !string.IsNullOrWhiteSpace(wText8.Text)) // EWCA/Civ/2004/993
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

            if (!string.IsNullOrWhiteSpace(wText2.Text))
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

            if (!string.IsNullOrWhiteSpace(wText2.Text))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static IInline[] MakeOrSplitParty(string text, RunProperties props, PartyRole role)
    {
        if (text.StartsWith("(1)") && text.Contains("(2)"))
        {
            // ewhc/admin/2022/273
            var i = text.IndexOf("(2)");
            var text1 = text.Substring(0, i);
            var text2 = text.Substring(i);
            var party1 = new WParty(text1, props) { Role = role };
            var party2 = new WParty(text2, props) { Role = role };
            return [party1, party2];
        }

        var party = new WParty(text, props) { Role = role };
        return [party];
    }

    private static WLine MakeParty(IBlock name, PartyRole? role)
    {
        var line = (WLine)name;
        var lineContents = line.Contents.ToArray();
        if (lineContents.Length == 1)
        {
            var text = (WText)lineContents[0];
            var party = new WParty(text) { Role = role };
            // use MakeOrSplitParty
            return WLine.Make(line, [party]);
        }

        if (lineContents.All(inline => inline is WText) &&
            lineContents.Cast<WText>().Any(wText => !string.IsNullOrWhiteSpace(wText.Text)))
        {
            var party = new WParty2(lineContents.Cast<WText>()) { Role = role };
            return WLine.Make(line, [party]);
        }

        if (lineContents.All(inline => inline is ITextOrWhitespace) &&
            lineContents.Any(inline => inline is WText wText && !string.IsNullOrWhiteSpace(wText.Text)))
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

    private static WLine MakeRole(IBlock block, PartyRole role)
    {
        var line = (WLine)block;
        return WLine.Make(line, [new WRole { Role = role, Contents = line.Contents }]);
    }

    private static bool IsAnyPartyType(string s)
    {
        if (IsFirstPartyType(s))
        {
            return true;
        }

        if (IsSecondPartyType(s))
        {
            return true;
        }

        return false;
    }

    private static bool IsFirstPartyType(string s)
    {
        ISet<string> firstPartyTypes = new HashSet<string>
        {
            "Claimant",
            "Claimants",
            "(Claimant)",
            "(CLAIMANT)",
            "(CLAIMANTS)",
            "Claimant/part 20 Defendant",
            "First Claimant",
            "Second Claimant",
            "Claimant / Defendant to Counterclaim",
            "Claimant/Respondent",
            "Claimant/ Respondent",
            "CLAIMANT/RESPONDENT",
            "Respondent/Claimant",
            "Claimants/Respondents",
            "CLAIMANTS/RESPONDENTS",
            "Respondent", // EWCA/Civ/2003/1686
            "Applicant",
            "Applicants",
            "Claimant/Applicant",
            "Claimant/Appellant",
            "Claimants/Appellants",
            "CLAIMANT/APPELLANT",
            "Appellant",
            "(APPELLANT)",
            "(APPELLANTS)",
            "Appellant/Appellant",
            "Applicant/Appellant",
            "Appellant/Applicant",
            "Appellant/Claimant",
            "Appellants/ Claimants",
            "Petitioner"
        };
        if (firstPartyTypes.Contains(s))
        {
            return true;
        }

        return TryGetPartyRole(s, out _);
    }

    private static bool IsFirstPartyType(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

        var normalized = line.NormalizedContent;
        return IsFirstPartyType(normalized);
    }

    private static PartyRole GetAnyPartyRole(string s)
    {
        if (IsFirstPartyType(s))
        {
            return GetFirstPartyRole(s);
        }

        if (IsSecondPartyType(s))
        {
            return GetSecondPartyRole(s);
        }

        throw new Exception();
    }

    private static PartyRole GetFirstPartyRole(string s)
    {
        switch (s)
        {
            case "Claimant":
            case "Claimants":
            case "(Claimant)":
            case "(CLAIMANT)":
            case "(CLAIMANTS)":
            case "Claimant/part 20 Defendant":
            case "First Claimant":
            case "Second Claimant":
            case "Claimant / Defendant to Counterclaim":
                return PartyRole.Claimant;
            case "Claimant/Respondent":
            case "Claimant/ Respondent":
            case "CLAIMANT/RESPONDENT":
            case "Respondent/Claimant":
            case "Claimants/Respondents":
            case "CLAIMANTS/RESPONDENTS":
            case "Respondent":
                return PartyRole.Respondent;
            case "Applicant":
            case "Applicants":
            case "Claimant/Applicant":
                return PartyRole.Applicant;
            case "Appellant":
            case "(APPELLANT)":
            case "(APPELLANTS)":
            case "Appellant/Appellant":
            case "Applicant/Appellant":
            case "Appellant/Applicant":
            case "Appellant/Claimant":
            case "Appellants/ Claimants":
            case "Claimant/Appellant":
            case "Claimants/Appellants":
            case "CLAIMANT/APPELLANT":
                return PartyRole.Appellant;
            case "Petitioner":
                return PartyRole.Petitioner;
            default:
                return TryGetPartyRole(s, out var role) ? role : throw new Exception();
        }
    }

    private static PartyRole GetFirstPartyRole(IBlock block)
    {
        if (block is not WLine line)
        {
            throw new Exception();
        }

        var normalized = line.NormalizedContent;
        return GetFirstPartyRole(normalized);
    }

    private static bool IsPartyNameAndRole(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

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
            if (!IsAnyPartyType(s))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static WLine MakePartyAndRole(IBlock block)
    {
        var line = (WLine)block;
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

    private static bool IsBetweenPartyMarker(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

        var normalized = line.NormalizedContent;
        return IsV(normalized);
    }

    private static bool IsBetweenPartyMarker2(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

        var normalized = line.NormalizedContent;
        return IsAnd(normalized);
    }

    private static bool IsSecondPartyType(string s)
    {
        ISet<string> secondPartyTypes = new HashSet<string>
        {
            "Defendant",
            "Defendants",
            "(Defendant)",
            "(DEFENDANT)",
            "(DEFENDANTS)",
            "Defendant/Part 20 Claimant",
            "First Defendant",
            "Second Defendant",
            "(FIRST DEFENDANT)",
            "(SECOND DEFENDANT)",
            "(1ST DEFENDANT)",
            "(2ND DEFENDANT)",
            "(1st DEFENDANT)",
            "(2nd DEFENDANT)",
            "(3rd DEFENDANT)",
            "Applicants/Defendants",
            "Defendant/Appellant",
            "DEFENDANT/APPELLANT",
            "Defendants/Appellants",
            "Defendants / Appellants",
            "Appellant/Defendant",
            "Appellant/First Defendant",
            "Defendant / Counterclaimant",
            "Appellant", // EWCA/Civ/2003/1686
            "Respondent",
            "Respondents",
            "(RESPONDENT)",
            "(RESPONDENTS)",
            "Defendant/Respondent",
            "Defendants/Respondents",
            "DEFENDANT/RESPONDENT",
            "DEFENDANTS/RESPONDENTS",
            "Respondent/Respondent",
            "Respondents/Respondents",
            "Respondents/Defendants",
            "Respondents/ Defendants",
            "Respondnet", // EWHC/Admin/2010/3393
            "First Respondent",
            "Second Respondent",
            "Interested Party",
            "Interested Parties",
            "(INTERESTED PARTY)",
            "(INTERESTED PARTIES)",
            "Second Interested Party",
            "Third Interested Party",
            "FIRST DEFENDANT’S SOLICITOR/APPELLANT",
            "Third Party/Appellant",
            "Intervener",
            "Interveners",
            "Additional Claimant"
        };
        if (secondPartyTypes.Contains(s))
        {
            return true;
        }

        return TryGetPartyRole(s, out _);
    }

    private static bool IsSecondPartyType(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

        var normalized = line.NormalizedContent;
        return IsSecondPartyType(normalized);
    }

    private static PartyRole GetSecondPartyRole(string s)
    {
        switch (s)
        {
            case "Defendant":
            case "Defendants":
            case "(Defendant)":
            case "(DEFENDANT)":
            case "(DEFENDANTS)":
            case "Defendant/Part 20 Claimant":
            case "First Defendant":
            case "Second Defendant":
            case "(FIRST DEFENDANT)":
            case "(SECOND DEFENDANT)":
            case "(1ST DEFENDANT)":
            case "(2ND DEFENDANT)":
            case "(1st DEFENDANT)":
            case "(2nd DEFENDANT)":
            case "(3rd DEFENDANT)":
            case "Applicants/Defendants":
            case "Defendant / Counterclaimant":
                return PartyRole.Defendant;
            case "Defendant/Appellant":
            case "DEFENDANT/APPELLANT":
            case "Defendants/Appellants":
            case "Defendants / Appellants":
            case "Appellant/Defendant":
            case "Appellant/First Defendant":
            case "Appellant":
            case "FIRST DEFENDANT’S SOLICITOR/APPELLANT": // EWCA/Civ/2006/1032
            case "Third Party/Appellant": // [2022] EWHC 34 (Ch)
                return PartyRole.Appellant;
            case "Respondent":
            case "Respondents":
            case "(RESPONDENT)":
            case "(RESPONDENTS)":
            case "Defendant/Respondent":
            case "Defendants/Respondents":
            case "DEFENDANT/RESPONDENT":
            case "DEFENDANTS/RESPONDENTS":
            case "Respondent/Respondent":
            case "Respondents/Respondents":
            case "Respondents/Defendants":
            case "Respondents/ Defendants":
            case "First Respondent":
            case "Second Respondent":
            case "Respondnet": // EWHC/Admin/2010/3393
                return PartyRole.Respondent;
            case "Interested Party":
            case "Interested Parties":
            case "(INTERESTED PARTY)":
            case "(INTERESTED PARTIES)":
            case "Second Interested Party":
            case "Third Interested Party":
                return PartyRole.InterestedParty;
            case "Intervener":
            case "Interveners":
                return PartyRole.Intervener;
            case "Additional Claimant":
                return PartyRole.Claimant;
            default:
                return TryGetPartyRole(s, out var role) ? role : throw new Exception();
        }
    }

    private static PartyRole GetSecondPartyRole(IBlock block)
    {
        if (block is not WLine line)
        {
            throw new Exception();
        }

        var normalized = line.NormalizedContent;
        return GetSecondPartyRole(normalized);
    }

    private static bool IsAfterPartyMarker(IBlock block)
    {
        if (IsBeforePartyMarker(block))
        {
            return true;
        }

        if (block is not WLine line)
        {
            return false;
        }

        var content = line.NormalizedContent;
        if (content.StartsWith("Computer Aided Transcript"))
        {
            return true;
        }

        if (content.StartsWith("REPORTING RESTRICTIONS APPLY:"))
        {
            return true;
        }

        return false;
    }


    private IBlock EnrichBlock(IBlock block)
    {
        if (block is WTable table)
        {
            return EnrichTable(table);
        }

        if (block is WLine line)
        {
            return Enrich(line);
        }

        return block;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        return line;
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
                enrichedRow = EnrichRow(originalRows[i], originalRows[i + 1]);
            }

            enrichedRows.Add(enrichedRow);
        }

        return enrichedRows;
    }

    private static bool IsEmptyCell(ICell cell)
    {
        return cell.Contents.All(block => block is WLine line && IsEmptyLine(line));
    }

    private static bool IsEmptyCell(WCell cell)
    {
        return cell.Contents.All(block => block is WLine line && IsEmptyLine(line));
    }

    private static bool IsEmptyLine(IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

        return IsEmptyLine(line);
    }

    private static bool IsEmptyLine(WLine line)
    {
        return string.IsNullOrWhiteSpace(line.NormalizedContent);
    }

    private WRow EnrichRow(WRow row)
    {
        var rowCells = row.Cells.ToArray();
        if (rowCells.Length == 2)
        {
            return EnrichTwoCellRow(row);
        }

        if (rowCells.Length != 3)
        {
            return row;
        }

        var first = (WCell)rowCells[0];
        var second = (WCell)rowCells[1];
        var third = (WCell)rowCells[2];
        if (!IsEmptyCell(first))
        {
            return row;
        }

        if (TryGetPartyRole(third, out var role))
        {
            second = EnrichCell(second, role);
            third = EnrichCellWithPartyRole(third, role);
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                first,
                second,
                third
            ]);
        }

        if (IsInTheMatterOfSomething(second))
        {
            second = EnrichInTheMatterOfSomething(second);
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                first,
                second,
                third
            ]);
        }

        if (TryGetTwoDifferentRoles(third, out var twoRoles))
        {
            second = EnrichPartyNamesWithTwoRoles(second, twoRoles);
            third = EnrichPartyTypesWithTwoRoles(third, twoRoles);
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                first,
                second,
                third
            ]);
        }

        return row;
    }

    private WRow EnrichTwoCellRow(WRow row)
    {
        var rowCells = row.Cells.ToArray();
        var first = (WCell)rowCells[0];
        var second = (WCell)rowCells[1];
        if (IsEmptyCell(first))
        {
            return row;
        }

        if (!TryGetPartyRole(second, out var role))
        {
            return row;
        }

        first = EnrichCell(first, role);
        second = EnrichCellWithPartyRole(second, role);
        return new WRow(row.Table, row.TablePropertyExceptions, row.Properties, [first, second]);
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

        if (!IsEmptyCell(firstRowCells[0]))
        {
            return false;
        }

        if (!IsEmptyCell(firstRowCells[^1]))
        {
            return false;
        }

        if (!IsEmptyCell(secondRowCells[0]))
        {
            return false;
        }

        if (!IsEmptyCell(secondRowCells[^1]))
        {
            return false;
        }

        if (!IsEmptyCell(thirdRowCells[0]))
        {
            return false;
        }

        if (!IsEmptyCell(thirdRowCells[^1]))
        {
            return false;
        }

        var middle1 = rows[0].TypedCells[1];
        var middle2 = rows[1].TypedCells[1];
        var middle3 = rows[2].TypedCells[1];
        if (!middle1.Contents.All(block => block is WLine line &&
                                           (IsEmptyLine(line) || (IsPartyName(line) && !IsFirstPartyType(line)))))
        {
            return false;
        }

        if (!middle2.Contents.All(block => block is WLine line &&
                                           (IsEmptyLine(line) || IsBetweenPartyMarker(line) ||
                                            IsBetweenPartyMarker2(line))))
        {
            return false;
        }

        if (middle2.Contents.Count(block => !IsEmptyLine(block)) != 1)
        {
            return false;
        }

        if (!middle3.Contents.All(block => block is WLine line &&
                                           (IsEmptyLine(line) || (IsPartyName(line) && !IsSecondPartyType(line)))))
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

    private WRow EnrichRow(WRow row, WRow next)
    {
        var rowCells = row.Cells.ToArray();
        if (rowCells.Length != 3)
        {
            return row;
        }

        var nextCells = next.Cells.ToArray();
        if (nextCells.Length != 3)
        {
            return row;
        }

        var before = (WCell)rowCells[0];
        if (!IsEmptyCell(before))
        {
            return row;
        }

        var after = (WCell)rowCells[2];
        if (!IsEmptyCell(after))
        {
            return row;
        }

        if (!IsEmptyCell(nextCells[0]))
        {
            return row;
        }

        if (!IsEmptyCell(nextCells[1]))
        {
            return row;
        }

        var partyCell = (WCell)rowCells[1];
        var roleCell = (WCell)nextCells[2];
        if (IsEmptyCell(partyCell))
        {
            return row;
        }

        if (IsEmptyCell(roleCell))
        {
            return row;
        }

        if (TryGetPartyRole(roleCell, out var role))
        {
            partyCell = EnrichCell(partyCell, role);
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                    before,
                    partyCell,
                    after
            ]);
        }

        return row;
    }

    public static bool TryGetPartyRole(WCell cell, out PartyRole role)
    {
        if (TryGetOneLinePartyRole(cell, out role))
        {
            return true;
        }

        if (TryGetTwoLinePartyRole(cell, out role))
        {
            return true;
        }

        return TryGetNLinePartyRole(cell, out role);
    }

    private static bool TryGetTwoDifferentRoles(WCell cell, out (PartyRole first, PartyRole second) roles)
    {
        var lines = cell.Contents.Where(block => !IsEmptyLine(block)).ToArray();
        if (lines.Length == 2
            && TryGetOneLinePartyRole(lines[0], out var role1)
            && TryGetOneLinePartyRole(lines[1], out var role2)
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

    private static bool TryGetOneLinePartyRole(WCell cell, out PartyRole role)
    {
        var lines = cell.Contents.Where(block => !IsEmptyLine(block)).ToArray();
        if (lines.Length == 1)
        {
            return TryGetOneLinePartyRole(lines[0], out role);
        }

        role = default;
        return false;

    }

    private static readonly Dictionary<string, PartyRole> OneLinePartyRoleLabels = new()
    {
        ["Appellant"] = PartyRole.Appellant,
        ["APPELLANT"] = PartyRole.Appellant,
        ["Appellants"] = PartyRole.Appellant,
        ["Defendant/Appellant"] = PartyRole.Appellant,
        ["Defendant/ Appellant"] = PartyRole.Appellant,
        ["Defendants/Appellants"] = PartyRole.Appellant,
        ["Appellants / Defendants"] = PartyRole.Appellant,
        ["Defendants/Appellants/"] = PartyRole.Appellant,
        ["Appellant/ Defendant"] = PartyRole.Appellant,
        ["Appellants/Claimants"] = PartyRole.Appellant,
        ["Appellants/ Claimants"] = PartyRole.Appellant,
        ["Claimant/Appellant"] = PartyRole.Appellant,
        ["Claimant/ Appellant"] = PartyRole.Appellant,
        ["Claimant / Appellant"] = PartyRole.Appellant,
        ["Appellant / Claimant"] = PartyRole.Appellant,
        ["Appellant / Third Defendant"] = PartyRole.Appellant,
        ["1st Appellant"] = PartyRole.Appellant,
        ["Respondent/Appellant"] = PartyRole.Appellant,
        ["Defendants/ Appellants"] = PartyRole.Appellant,
        ["Appellant/ Respondent"] = PartyRole.Appellant, // [2021] EWCA Civ 1792
        ["Claimants/ Appellants"] = PartyRole.Appellant,
        ["Claimants/Appellants"] = PartyRole.Appellant, // [2021] EWCA Civ 1799
        ["Appellant/Applicant"] = PartyRole.Appellant, // [2021] EWCA Crim 1877

        ["Applicant"] = PartyRole.Applicant,
        ["Applicants"] = PartyRole.Applicant,
        ["Respondent/Applicant"] = PartyRole.Applicant,
        ["Applicants/Claimants"] = PartyRole.Applicant,
        ["Applicant/ Claimant"] = PartyRole.Applicant,
        ["Claimant/Applicant"] = PartyRole.Applicant,
        ["Defendant/ Applicant"] = PartyRole.Applicant,
        ["1st Applicant"] = PartyRole.Applicant,
        ["2nd Applicant"] = PartyRole.Applicant,
        ["Claimant"] = PartyRole.Claimant,
        ["Claimants"] = PartyRole.Claimant,
        ["Claimant/Part 20 Defendant"] = PartyRole.Claimant,
        ["Claimant/part 20 Defendant"] = PartyRole.Claimant,
        ["Claimant / Defendant to Counterclaim"] = PartyRole.Claimant,
        ["Additional Claimant"] = PartyRole.Claimant,
        ["Defendant"] = PartyRole.Defendant,
        ["Defendants"] = PartyRole.Defendant,
        ["Defendant/Part 20 Claimant"] = PartyRole.Defendant,
        ["First Defendant"] = PartyRole.Defendant,
        ["Second Defendant"] = PartyRole.Defendant,
        ["Third Defendant"] = PartyRole.Defendant,
        ["Defendant / Counterclaimant"] = PartyRole.Defendant,
        ["Interested Party"] = PartyRole.InterestedParty,
        ["Interested parties"] = PartyRole.InterestedParty,
        ["Petitioner"] = PartyRole.Petitioner,
        ["Respondent"] = PartyRole.Respondent,
        ["RESPONDENT"] = PartyRole.Respondent,
        ["Respondents"] = PartyRole.Respondent,
        ["Claimant/Respondent"] = PartyRole.Respondent,
        ["Claimant/ Respondent"] = PartyRole.Respondent,
        ["Claimant / Respondent"] = PartyRole.Respondent,
        ["Clamaints/ Respondents"] = PartyRole.Respondent,
        ["Respondent/Claimant"] = PartyRole.Respondent,
        ["Respondent/ Claimant"] = PartyRole.Respondent,
        ["Defendant/Respondent"] = PartyRole.Respondent,
        ["Defendant/ Respondent"] = PartyRole.Respondent,
        ["Defendant / Respondent"] = PartyRole.Respondent,
        ["Defendants/Respondents"] = PartyRole.Respondent,
        ["Defendants/ Respondents"] = PartyRole.Respondent,
        ["Petitioner/Respondent"] = PartyRole.Respondent,
        ["First Respondent"] = PartyRole.Respondent,
        ["Second Respondent"] = PartyRole.Respondent,
        ["Third Respondent"] = PartyRole.Respondent,
        ["Fourth Respondent"] = PartyRole.Respondent,
        ["1st Respondent"] = PartyRole.Respondent,
        ["2nd Respondent"] = PartyRole.Respondent,
        ["3rd Respondent"] = PartyRole.Respondent, // EWCA/Civ/2012/378
        ["Respondents/Defendants"] = PartyRole.Respondent,
        ["Respond-ents/ Defendants"] = PartyRole.Respondent,
        ["Respondents/ Defendants"] = PartyRole.Respondent, // EWCA/Civ/2015/377, EWHC/QB/2006/582
        ["Respondent/Defendants"] = PartyRole.Respondent,
        ["Respondent / Defendant"] = PartyRole.Respondent,
        ["Respondents Second and Third/ Defendants"] = PartyRole.Respondent, // EWCA/Civ/2004/1249
        ["Respondent/Petitioner"] = PartyRole.Respondent, // [2021] EWCA Civ 1792
        ["Respondents/Claimants"] = PartyRole.Respondent,
        ["Respondents / Claimants"] = PartyRole.Respondent,
        ["Respondent/ First Defendant"] = PartyRole.Respondent
    };

    private static bool TryGetOneLinePartyRole(IBlock block, out PartyRole role)
    {
        if (block is not WLine line)
        {
            role = default;
            return false;
        }

        var normalized = line.NormalizedContent;
        if (OneLinePartyRoleLabels.TryGetValue(normalized, out role))
        {
            return true;
        }

        return TryGetPartyRole(normalized, out role);
    }

    private static bool TryGetTwoLinePartyRole(WCell cell, out PartyRole role)
    {
        var lines = cell.Contents.Where(l => !IsEmptyLine(l)).ToArray();

        if (lines is [WLine line1, WLine line2])
        {
            return TryGetTwoLinePartyRole(line1.NormalizedContent, line2.NormalizedContent, out role);
        }

        role = default;
        return false;
    }

    /// <summary>
    ///  one/two combinations that aren't exact matches, so can't live in TwoLinePartyRoles
    /// </summary>
    private static bool TryGetTwoLinePartyRoleFromPattern(string one, string two, out PartyRole role)
    {
        if (one == "Defendant/" && two.EndsWith("Claimant")) // EWHC/Ch/2008/2079
        {
            role = PartyRole.Defendant;
            return true;
        }

        if (one == "Claimant/" && two.EndsWith("Defendant")) // EWHC/Ch/2008/2079
        {
            role = PartyRole.Claimant;
            return true;
        }

        if (one == "Respondents" && two.StartsWith("Respondent")) // EWHC/Fam/2013/1956
        {
            role = PartyRole.Respondent;
            return true;
        }

        role = default;
        return false;
    }

    private static readonly Dictionary<(string one, string two), PartyRole> TwoLinePartyRoles = new()
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

    private static bool TryGetTwoLinePartyRole(string one, string two, out PartyRole role)
    {
        return TryGetTwoLinePartyRoleFromPattern(one, two, out role)
            || TwoLinePartyRoles.TryGetValue((one, two), out role)
            || TryGetPartyRoleForCombinedLabels(one, two, out role);
    }

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

    private static bool TryGetNLinePartyRole(WCell cell, out PartyRole role)
    {
        var blocks = cell.Contents.Where(block => !IsEmptyLine(block)).ToArray();
        role = default;
        if (blocks.Length < 2)
        {
            return false;
        }

        if (!blocks.All(block => block is WLine))
        {
            return false;
        }

        if (blocks.Length == 3)
        {
            var one = ((WLine)blocks[0]).NormalizedContent;
            var two = ((WLine)blocks[1]).NormalizedContent;
            var three = ((WLine)blocks[2]).NormalizedContent;
            if (one == "Defendants" && two == "Part 20 Claimant/" && three == "Appellant")
            {
                role = PartyRole.Appellant;
                return true;
            }

            if (one == "Respondents" && two == "Appellant" && three == "Respondent") // EWCA/Civ/2010/180
            {
                role = PartyRole.Respondent;
                return true;
            }
        }

        foreach (var (pattern, patternRole) in NLinePartyRolePatterns)
        {
            if (blocks.Cast<WLine>().All(line => pattern.IsMatch(line.NormalizedContent)))
            {
                role = patternRole;
                return true;
            }
        }

        return false;
    }

    private WCell EnrichCell(WCell cell, PartyRole role)
    {
        var contents = cell.Contents.Select(block =>
            {
                if (block is WOldNumberedParagraph np)
                {
                    // EWCA/Civ/2015/455
                    var npContents = np.Contents.ToArray();
                    if (npContents.Length != 1 || npContents[0] is not WText wText2)
                    {
                        return np;
                    }

                    var party2 = new WParty(wText2) { Role = role };
                    return new WOldNumberedParagraph(np, [party2]);
                }

                if (block is not WLine line)
                {
                    return block;
                }

                var lineContents = line.Contents.ToArray();
                if (lineContents.Length == 0)
                {
                    return line;
                }

                Func<IInline, bool> filter = inline =>
                {
                    if (inline is not WText wText)
                    {
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(wText.Text))
                    {
                        return false;
                    }

                    var trimmed = wText.Text.Trim();
                    if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
                    {
                        return false;
                    }

                    if (IsAnd(trimmed))
                    {
                        return false;
                    }

                    return true;
                };
                var filtered = lineContents.Where(filter);
                if (filtered.Count() == 1)
                {
                    var mapped = lineContents.SelectMany(inline => filter(inline)
                        ? MakeOrSplitParty(((WText)inline).Text, ((WText)inline).properties, role)
                        : [inline]);
                    return WLine.Make(line, mapped);
                }

                if (lineContents.Any(inline => inline is WText wt && Regex.IsMatch(wt.Text, @"^\(\d+\) ")) &&
                    lineContents.All(inline => inline is WLineBreak || (inline is WText wt &&
                                                                        (string.IsNullOrEmpty(wt.Text) ||
                                                                         Regex.IsMatch(wt.Text, @"^\(\d+\) ")))))
                {
                    var mapped = lineContents.Select(inline =>
                    {
                        if (inline is WText wt)
                        {
                            if (string.IsNullOrEmpty(wt.Text))
                            {
                                return inline;
                            }

                            return new WParty(wt) { Role = role };
                        }

                        return inline;
                    });
                    return WLine.Make(line, mapped);
                }

                /* these should be rewritten so they do nothing if their conditions aren't met (instead of returning) */
                if (lineContents.Length == 1)
                {
                    if (lineContents[0] is not WText wText)
                    {
                        return line;
                    }

                    if (string.IsNullOrWhiteSpace(wText.Text))
                    {
                        return line;
                    }

                    var trimmed = wText.Text.Trim();
                    if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
                    {
                        return line;
                    }

                    if (IsAnd(trimmed))
                    {
                        return line;
                    }

                    var party = new WParty(wText) { Role = role };
                    return WLine.Make(line, [party]);
                }

                if (lineContents.Length == 2)
                {
                    if (lineContents[0] is not WText wText1)
                    {
                        return line;
                    }

                    if (lineContents[1] is not WText wText2)
                    {
                        return line;
                    }

                    if (string.IsNullOrWhiteSpace(wText1.Text))
                    {
                        return line;
                    }

                    if (!string.IsNullOrWhiteSpace(wText2.Text))
                    {
                        return line;
                    }

                    var trimmed = wText1.Text.Trim();
                    if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
                    {
                        return line;
                    }

                    if (IsAnd(trimmed))
                    {
                        return line;
                    }

                    var party = new WParty(wText1) { Role = role };
                    return WLine.Make(line, [party, lineContents[1]]);
                }

                if (lineContents.Length == 3)
                {
                    // [2021] EWCA Civ 1876
                    if (lineContents[0] is WText wText1
                        && lineContents[1] is WLineBreak
                        && lineContents[2] is WText
                        && wText1.Text == "SECRETARY OF STATE ")
                    {
                        var party = new WParty2(lineContents.Cast<ITextOrWhitespace>()) { Role = role };
                        return WLine.Make(line, [party]);
                    }
                }

                if (lineContents.All(inline => inline is WText))
                {
                    var party = new WParty2(lineContents.Cast<WText>());
                    return WLine.Make(line, [party]);
                }

                if (lineContents.Length == 3)
                {
                    // EWHC/Ch/2018/2498
                    if (lineContents[0] is not WText wText1)
                    {
                        return line;
                    }

                    if (lineContents[1] is not WTab)
                    {
                        return line;
                    }

                    if (lineContents[2] is not WText wText3)
                    {
                        return line;
                    }

                    if (!Regex.IsMatch(wText1.Text, @"^\d\.$"))
                    {
                        return line;
                    }

                    var trimmed = wText3.Text.Trim();
                    if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
                    {
                        return line;
                    }

                    if (IsAnd(trimmed))
                    {
                        return line;
                    }

                    var party = new WParty(wText3) { Role = role };
                    return WLine.Make(line,
                    [
                        lineContents[0],
                        lineContents[1],
                        party
                    ]);
                }

                return line;
            }
        );
        return new WCell(cell.Row, cell.Props, contents);
    }

    private static bool IsV(string s)
    {
        char[] trim = { ' ', '-', '–' };
        return s.Trim(trim).ToLower() == "v";
    }

    private static bool IsAnd(string s)
    {
        char[] trim = { ' ', '-', '–' };
        return s.Trim(trim).ToLower() == "and";
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

            if (IsEmptyLine(block))
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

                if (string.IsNullOrWhiteSpace(wText.Text))
                {
                    contents.Add(block);
                    continue;
                }

                var trimmed = wText.Text.Trim();
                if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
                {
                    contents.Add(block);
                    continue;
                }

                if (IsAnd(trimmed))
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

                if (!string.IsNullOrWhiteSpace(wText1.Text))
                {
                    contents.Add(block);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(wText2.Text))
                {
                    contents.Add(block);
                    continue;
                }

                var trimmed = wText2.Text.Trim();
                if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
                {
                    contents.Add(block);
                    continue;
                }

                if (IsAnd(trimmed))
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
        var contents = new List<IBlock>();
        var firstPartyFound = false;
        var emptyAfterFirstFound = false;
        foreach (var block in cell.Contents)
        {
            if (IsEmptyLine(block))
            {
                if (firstPartyFound)
                {
                    emptyAfterFirstFound = true;
                }

                contents.Add(block);
                continue;
            }

            if (block is not WLine line)
            {
                return cell;
            }

            if (emptyAfterFirstFound)
            {
                var role = new WRole { Contents = line.Contents, Role = roles.second };
                var newLine = WLine.Make(line, [role]);
                contents.Add(newLine);
            }
            else
            {
                firstPartyFound = true;
                var role = new WRole { Contents = line.Contents, Role = roles.first };
                var newLine = WLine.Make(line, [role]);
                contents.Add(newLine);
            }
        }

        if (!emptyAfterFirstFound)
        {
            return cell;
        }

        return new WCell(cell.Row, cell.Props, contents);
    }

    private static bool IsInTheMatterOfSomething(WCell cell)
    {
        var cellContents = cell.Contents.ToArray();
        return cellContents.Length == 1 && IsInTheMatterOfSomething(cellContents[0]);
    }

    private WCell EnrichInTheMatterOfSomething(WCell cell)
    {
        var line = MakeDocTitle(cell.Contents.First());
        return new WCell(cell.Row, cell.Props, [line]);
    }

    protected override WLine Enrich(WLine line)
    {
        var lineContents = line.Contents.ToArray();
        if (lineContents.Length != 1)
        {
            return line;
        }

        if (lineContents[0] is not WText text)
        {
            return line;
        }

        if (text.Text.StartsWith("IN THE MATTER OF ", StringComparison.InvariantCultureIgnoreCase))
        {
            return WLine.Make(line, [new WDocTitle(text)]);
        }

        return line;
    }

    /* new methods */

    public static bool TryGetPartyRole(string s, out PartyRole role)
    {
        if (s.Contains('/'))
        {
            var (one, two) = s.Split('/', 2) switch { var x => (x[0], x[1]) };
            if (!string.IsNullOrWhiteSpace(one) &&
                !string.IsNullOrWhiteSpace(two)) // not if at beginning or end of line
            {
                return TryGetPartyRoleForCombinedLabels(one, two, out role);
            }
        }

        if (s.Contains(" and "))
        {
            var (one, two) = s.Split(" and ", 2) switch { var x => (x[0], x[1]) };
            if (!string.IsNullOrWhiteSpace(one) && !string.IsNullOrWhiteSpace(two))
            {
                return TryGetPartyRoleForCombinedLabels(one, two, out role);
            }
        }

        return TryGetPartyRoleForSingleLabel(s, out role);
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

    private static readonly Dictionary<string, PartyRole> SingleLabelPartyRoles = new()
    {
        ["appellant"] = PartyRole.Appellant,
        ["appellants"] = PartyRole.Appellant,
        ["applicant"] = PartyRole.Applicant,
        ["applicants"] = PartyRole.Applicant,
        ["claimant"] = PartyRole.Claimant,
        ["claimants"] = PartyRole.Claimant,
        ["defendant"] = PartyRole.Defendant,
        ["defendants"] = PartyRole.Defendant,
        ["petitioner"] = PartyRole.Petitioner,
        ["petitioners"] = PartyRole.Petitioner,
        ["respondent"] = PartyRole.Respondent,
        ["respondents"] = PartyRole.Respondent,
        ["interested party"] = PartyRole.InterestedParty,
        ["interested parties"] = PartyRole.InterestedParty,
        ["intervener"] = PartyRole.Intervener,
        ["interveners"] = PartyRole.Intervener,
        ["requested person"] = PartyRole.RequestedPerson, // [2022] EWHC 273 (Admin)
        ["requested persons"] = PartyRole.RequestedPerson, // [2022] EWHC 273 (Admin)
        ["requesting state"] = PartyRole.RequestingState
    };

    private static bool TryGetPartyRoleForSingleLabel(string s, out PartyRole role)
    {
        s = Regex.Replace(s, @"\s+", " ").Trim(' ', '/', '(', ')');
        if (s.StartsWith("Part 20 ", StringComparison.InvariantCultureIgnoreCase))
        {
            s = s.Substring(8);
        }

        if (s.Equals("Third Party", StringComparison.InvariantCultureIgnoreCase))
        {
            role = PartyRole.ThirdParty;
            return true;
        }

        if (PrefixesToStrip.Any(prefix => s.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)))
        {
            s = s.Substring(s.IndexOf(' ') + 1);
        }

        return SingleLabelPartyRoles.TryGetValue(s.ToLower(), out role);
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
