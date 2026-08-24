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
            var found = EnrichMultiLinePartyBlockOrNull(rest);
            found ??= EnrichMultiLinePartyBlockWithInlineRolesOrNull(rest);
            found ??= EnrichMultiLinePartyBlockWithTwoGroupsBeforeVOrNull(rest);
            if (found is not null)
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

    private static List<IBlock> EnrichMultiLinePartyBlockOrNull(IBlock[] rest, bool successive = false)
    {
        if (rest.Length == 0)
        {
            return null;
        }

        var i = 0;
        var line = rest[i];
        if (!IsBeforePartyMarker(line) && !IsBeforePartyMarker2(line) && !(successive && IsBeforePartyMarker3(line)))
        {
            return null;
        }

        List<IBlock> enriched = [line];
        i += 1;
        if (i == rest.Length)
        {
            return null;
        }

        line = rest[i];
        if (line is WLine inPrivate && inPrivate.NormalizedContent == "IN PRIVATE")
        {
            // EWHC/Admin/2012/2822
            enriched.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return null;
            }

            line = rest[i];
        }

        if (IsBeforePartyMarker2(line))
        {
            enriched.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return null;
            }

            _ = rest[i];
        }

        var firstGroupOfParites = EnrichFirstPartyGroupOrNull(rest[i..]);
        if (firstGroupOfParites is null)
        {
            return null;
        }

        enriched.AddRange(firstGroupOfParites);
        i += firstGroupOfParites.Count;
        if (i == rest.Length)
        {
            return null;
        }

        line = rest[i];
        /* no "v" or "and" in EWHC/Comm/2013/3920 */
        if (IsBetweenPartyMarker(line) || IsBetweenPartyMarker2(line))
        {
            enriched.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return null;
            }

            _ = rest[i];
        }

        var secondGroupOfParites = EnrichSecondPartyGroupOrNull(rest[i..]);
        if (secondGroupOfParites is null)
        {
            return null;
        }

        enriched.AddRange(secondGroupOfParites);
        i += secondGroupOfParites.Count;
        if (i == rest.Length)
        {
            return null;
        }

        line = rest[i];

        if (IsBetweenPartyMarker2(line))
        {
            enriched.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return null;
            }

            _ = rest[i];
        }

        var thirdGroupOfParites = EnrichSecondPartyGroupOrNull(rest[i..]);
        if (thirdGroupOfParites is not null)
        {
            enriched.AddRange(thirdGroupOfParites);
            i += thirdGroupOfParites.Count;
        }

        var fourthGroupOfParites = EnrichSecondPartyGroupOrNull(rest[i..]);
        if (fourthGroupOfParites is not null)
        {
            enriched.AddRange(fourthGroupOfParites);
            i += fourthGroupOfParites.Count;
        }

        if (i == rest.Length)
        {
            return null;
        }

        line = rest[i];
        if (IsAfterPartyMarker(line))
        {
            enriched.Add(line);
            return enriched;
        }

        var another = EnrichMultiLinePartyBlockOrNull(rest[i..], true);
        if (another is not null)
        {
            enriched.AddRange(another);
            return enriched;
        }

        return enriched;
    }

    private static List<IBlock> EnrichMultiLinePartyBlockWithInlineRolesOrNull(IBlock[] rest)
    {
        // EWHC/Admin/2018/3311
        if (rest.Length == 0)
        {
            return null;
        }

        var i = 0;
        var line = rest[i];
        if (!IsBeforePartyMarker(line) && !IsBeforePartyMarker2(line))
        {
            return null;
        }

        List<IBlock> enriched = [line];
        i += 1;
        if (i == rest.Length)
        {
            return null;
        }

        line = rest[i];
        if (IsBeforePartyMarker2(line))
        {
            // perhaps do this only if first line isn't marker 2
            enriched.Add(line);
            i += 1;
            if (i == rest.Length)
            {
                return null;
            }

            line = rest[i];
        }

        if (!IsPartyNameAndRole(line))
        {
            return null;
        }

        var party1 = MakePartyAndRole(line);
        enriched.Add(party1);
        i += 1;
        if (i == rest.Length)
        {
            return null;
        }

        line = rest[i];
        if (IsBetweenPartyMarker(line) || IsBetweenPartyMarker2(line))
        {
            enriched.Add(line);
            i += 1;
        }
        else
        {
            return null;
        }

        if (i == rest.Length)
        {
            return null;
        }

        line = rest[i];
        if (!IsPartyNameAndRole(line))
        {
            return null;
        }

        var party2 = MakePartyAndRole(line);
        enriched.Add(party2);
        i += 1;
        if (i == rest.Length)
        {
            return null;
        }

        line = rest[i];
        if (!IsAfterPartyMarker(line))
        {
            return null;
        }

        enriched.Add(line);
        return enriched;
    }

    /* this one has two types of parties before the v */
    private static List<IBlock> EnrichMultiLinePartyBlockWithTwoGroupsBeforeVOrNull(IBlock[] rest)
    {
        // EWHC/Admin/2015/897
        if (rest.Length == 0)
        {
            return null;
        }

        var i = 0;
        var line = rest[i];
        if (!IsBeforePartyMarker(line))
        {
            return null;
        }

        List<IBlock> enriched = [line];
        i += 1;
        if (i == rest.Length)
        {
            return null;
        }

        line = rest[i];
        /* between */
        if (!IsBeforePartyMarker2(line))
        {
            return null;
        }

        enriched.Add(line);
        i += 1;
        var firstGroupOfParites = EnrichFirstPartyGroupOrNull(rest[i..]);
        if (firstGroupOfParites is null)
        {
            return null;
        }

        enriched.AddRange(firstGroupOfParites);
        i += firstGroupOfParites.Count;
        if (i == rest.Length)
        {
            return null;
        }

        line = rest[i];
        /* and */
        if (!IsBetweenPartyMarker2(line))
        {
            return null;
        }

        enriched.Add(line);
        i += 1;
        if (i == rest.Length)
        {
            return null;
        }

        _ = rest[i];
        var secondGroupOfParites = EnrichFirstPartyGroupOrNull(rest[i..]);
        if (secondGroupOfParites is null)
        {
            return null;
        }

        enriched.AddRange(secondGroupOfParites);
        i += secondGroupOfParites.Count;
        if (i == rest.Length)
        {
            return null;
        }

        line = rest[i];
        /* v */
        if (!IsBetweenPartyMarker(line))
        {
            return null;
        }

        enriched.Add(line);
        i += 1;
        if (i == rest.Length)
        {
            return null;
        }

        _ = rest[i];
        var thirdGroupOfParites = EnrichSecondPartyGroupOrNull(rest[i..]);
        if (thirdGroupOfParites is null)
        {
            return null;
        }

        enriched.AddRange(thirdGroupOfParites);
        i += thirdGroupOfParites.Count;
        if (i == rest.Length)
        {
            return null;
        }

        line = rest[i];
        if (IsAfterPartyMarker(line))
        {
            enriched.Add(line);
            return enriched;
        }

        return null;
    }

    private static List<IBlock> EnrichFirstPartyGroupOrNull(IBlock[] rest)
    {
        return EnrichPartyNamesWithRoleLabelOrNull(rest, IsFirstPartyType, GetFirstPartyRole);
    }

    private static List<IBlock> EnrichSecondPartyGroupOrNull(IBlock[] rest)
    {
        return EnrichPartyNamesWithRoleLabelOrNull(rest, IsSecondPartyType, GetSecondPartyRole);
    }

    private static List<IBlock> EnrichPartyNamesWithRoleLabelOrNull(IBlock[] rest, Func<IBlock, bool> test, Func<IBlock, PartyRole> construct)
    {
        var i = 0;
        if (i == rest.Length)
        {
            return null;
        }

        var line = rest[i];
        if (!IsPartyName(line))
        {
            return null;
        }

        List<IBlock> stack = [line];
        i += 1;
        while (true)
        {
            if (i == rest.Length)
            {
                return null;
            }

            line = rest[i];
            if (test(line))
            {
                var role1 = construct(line);
                return
                [
                    .. stack.Select(block => MakeParty(block, role1)),
                    MakeRole(line, role1)
                ];
            }

            if (IsPartyName(line))
            {
                stack.Add(line);
                i += 1;
            }
            else
            {
                return null;
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

        return GetPartyRole(s) is not null;
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
                return GetPartyRole(s).Value;
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

        return GetPartyRole(s) is not null;
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
                return GetPartyRole(s).Value;
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
        if (table.TypedRows.Count == 3)
        {
            rows = EnrichThreeRowsWithNoRolesOrNull(table.TypedRows);
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

        var role = GetPartyRole(third);
        if (role is not null)
        {
            second = EnrichCell(second, role.Value);
            third = EnrichCellWithPartyRole(third, (PartyRole)role);
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

        var twoRoles = GetTwoDifferentRoles(third);
        if (twoRoles is not null)
        {
            second = EnrichPartyNamesWithTwoRoles(second, twoRoles.Value);
            third = EnrichPartyTypesWithTwoRoles(third, twoRoles.Value);
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

        var role = GetPartyRole(second);
        if (role is null)
        {
            return row;
        }

        first = EnrichCell(first, role.Value);
        second = EnrichCellWithPartyRole(second, role.Value);
        return new WRow(row.Table, row.TablePropertyExceptions, row.Properties, [first, second]);
    }

    private WRow[] EnrichThreeRowsWithNoRolesOrNull(List<WRow> rows)
    {
        // EWCA/Crim/2007/854, EWCA/Crim/2014/465
        var firstRowCells = rows[0].Cells.ToArray();
        if (firstRowCells.Length != 3)
        {
            return null;
        }

        var secondRowCells = rows[1].Cells.ToArray();
        if (secondRowCells.Length != 3)
        {
            return null;
        }

        var thirdRowCells = rows[2].Cells.ToArray();
        if (thirdRowCells.Length != 3)
        {
            return null;
        }

        if (!IsEmptyCell(firstRowCells[0]))
        {
            return null;
        }

        if (!IsEmptyCell(firstRowCells[^1]))
        {
            return null;
        }

        if (!IsEmptyCell(secondRowCells[0]))
        {
            return null;
        }

        if (!IsEmptyCell(secondRowCells[^1]))
        {
            return null;
        }

        if (!IsEmptyCell(thirdRowCells[0]))
        {
            return null;
        }

        if (!IsEmptyCell(thirdRowCells[^1]))
        {
            return null;
        }

        var middle1 = rows[0].TypedCells[1];
        var middle2 = rows[1].TypedCells[1];
        var middle3 = rows[2].TypedCells[1];
        if (!middle1.Contents.All(block => block is WLine line &&
                                           (IsEmptyLine(line) || (IsPartyName(line) && !IsFirstPartyType(line)))))
        {
            return null;
        }

        if (!middle2.Contents.All(block => block is WLine line &&
                                           (IsEmptyLine(line) || IsBetweenPartyMarker(line) ||
                                            IsBetweenPartyMarker2(line))))
        {
            return null;
        }

        if (middle2.Contents.Count(block => !IsEmptyLine(block)) != 1)
        {
            return null;
        }

        if (!middle3.Contents.All(block => block is WLine line &&
                                           (IsEmptyLine(line) || (IsPartyName(line) && !IsSecondPartyType(line)))))
        {
            return null;
        }

        var newMiddle1 = new WCell(middle1.Row, middle1.Props,
            middle1.Contents.Cast<WLine>()
                   .Select(line => IsEmptyLine(line) ? line : MakeParty(line, PartyRole.BeforeTheV)));
        var newMiddle3 = new WCell(middle3.Row, middle3.Props,
            middle3.Contents.Cast<WLine>()
                   .Select(line => IsEmptyLine(line) ? line : MakeParty(line, PartyRole.AfterTheV)));
        return
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

        var role = GetPartyRole(roleCell);
        if (role is not null)
        {
            partyCell = EnrichCell(partyCell, role.Value);
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                    before,
                    partyCell,
                    after
            ]);
        }

        return row;
    }

    public static PartyRole? GetPartyRole(WCell cell)
    {
        var role = GetOneLinePartyRole(cell);
        if (role is not null)
        {
            return role;
        }

        role = GetTwoLinePartyRole(cell);
        if (role is not null)
        {
            return role;
        }

        role = GetNLinePartyRole(cell);
        if (role is not null)
        {
            return role;
        }

        return null;
    }

    private static (PartyRole first, PartyRole second)? GetTwoDifferentRoles(WCell cell)
    {
        var lines = cell.Contents.Where(block => !IsEmptyLine(block)).ToArray();
        if (lines.Length != 2)
        {
            return null;
        }

        var role1 = GetOneLinePartyRole(lines[0]);
        if (role1 is null)
        {
            return null;
        }

        var role2 = GetOneLinePartyRole(lines[1]);
        if (role2 is null)
        {
            return null;
        }

        if (role1 == role2)
        {
            return null;
        }

        return (role1.Value, role2.Value);
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

    private static PartyRole? GetOneLinePartyRole(WCell cell)
    {
        var lines = cell.Contents.Where(block => !IsEmptyLine(block)).ToArray();
        return lines.Length == 1 ? GetOneLinePartyRole(lines[0]) : null;
    }

    private static PartyRole? GetOneLinePartyRole(IBlock block)
    {
        if (block is not WLine line)
        {
            return null;
        }

        var normalized = line.NormalizedContent;
        ISet<string> types = new HashSet<string>
        {
            "Appellant",
            "APPELLANT",
            "Appellants",
            "Defendant/Appellant",
            "Defendant/ Appellant",
            "Defendants/Appellants",
            "Appellants / Defendants",
            "Defendants/Appellants/",
            "Appellant/ Defendant",
            "Appellants/Claimants",
            "Appellants/ Claimants",
            "Claimant/Appellant",
            "Claimant/ Appellant",
            "Claimant / Appellant",
            "Appellant / Claimant",
            "Appellant / Third Defendant",
            "1st Appellant",
            "Respondent/Appellant",
            "Defendants/ Appellants",
            "Appellant/ Respondent", // [2021] EWCA Civ 1792
            "Claimants/ Appellants",
            "Claimants/Appellants", // [2021] EWCA Civ 1799
            "Appellant/Applicant" // [2021] EWCA Crim 1877
        };
        if (types.Contains(normalized))
        {
            return PartyRole.Appellant;
        }

        types = new HashSet<string>
        {
            "Claimant",
            "Claimants",
            "Claimant/Part 20 Defendant",
            "Claimant/part 20 Defendant",
            "Claimant / Defendant to Counterclaim",
            "Additional Claimant"
        };
        if (types.Contains(normalized))
        {
            return PartyRole.Claimant;
        }

        types = new HashSet<string>
        {
            "Applicant",
            "Applicants",
            "Respondent/Applicant",
            "Applicants/Claimants",
            "Applicant/ Claimant",
            "Claimant/Applicant",
            "Defendant/ Applicant",
            "1st Applicant",
            "2nd Applicant"
        };
        if (types.Contains(normalized))
        {
            return PartyRole.Applicant;
        }

        types = new HashSet<string>
        {
            "Defendant",
            "Defendants",
            "Defendant/Part 20 Claimant",
            "First Defendant",
            "Second Defendant",
            "Third Defendant",
            "Defendant / Counterclaimant"
        };
        if (types.Contains(normalized))
        {
            return PartyRole.Defendant;
        }

        types = new HashSet<string>
        {
            "Respondent",
            "RESPONDENT",
            "Respondents",
            "Claimant/Respondent",
            "Claimant/ Respondent",
            "Claimant / Respondent",
            "Clamaints/ Respondents",
            "Respondent/Claimant",
            "Respondent/ Claimant",
            "Defendant/Respondent",
            "Defendant/ Respondent",
            "Defendant / Respondent",
            "Defendants/Respondents",
            "Defendants/ Respondents",
            "Petitioner/Respondent",
            "First Respondent",
            "Second Respondent",
            "Third Respondent",
            "Fourth Respondent",
            "1st Respondent",
            "2nd Respondent",
            "3rd Respondent", // EWCA/Civ/2012/378
            "Respondents/Defendants",
            "Respond-ents/ Defendants",
            "Respondents/ Defendants", // EWCA/Civ/2015/377, EWHC/QB/2006/582
            "Respondent/Defendants",
            "Respondent / Defendant",
            "Respondents Second and Third/ Defendants", // EWCA/Civ/2004/1249
            "Respondent/Petitioner", // [2021] EWCA Civ 1792
            "Respondents/Claimants",
            "Respondents / Claimants",
            "Respondent/ First Defendant"
        };
        if (types.Contains(normalized))
        {
            return PartyRole.Respondent;
        }

        types = new HashSet<string> { "Petitioner" };
        if (types.Contains(normalized))
        {
            return PartyRole.Petitioner;
        }

        types = new HashSet<string> { "Interested Party", "Interested parties" };
        if (types.Contains(normalized))
        {
            return PartyRole.InterestedParty;
        }

        return GetPartyRole(normalized);
    }

    private static PartyRole? GetTwoLinePartyRole(WCell cell)
    {
        var lines = cell.Contents.Where(l => !IsEmptyLine(l)).ToArray();

        if (lines is [WLine line1, WLine line2])
        {
            return GetTwoLinePartyRole(line1.NormalizedContent, line2.NormalizedContent);
        }

        return null;
    }

    private static PartyRole? GetTwoLinePartyRole(string one, string two)
    {
        if (one == "Defendant/" && two.EndsWith("Claimant")) // EWHC/Ch/2008/2079
        {
            return PartyRole.Defendant;
        }

        if (one == "Defendant/" && two == "Applicant") // EWHC/Ch/2017/916
        {
            return PartyRole.Applicant;
        }

        if (one == "Defendant/" && two == "Appellant") // EWCA/Civ/2011/1383
        {
            return PartyRole.Appellant;
        }

        if (one == "Claimant/" && two == "Respondent") // EWCA/Civ/2008/183
        {
            return PartyRole.Respondent;
        }

        if (one == "Claimants/" && two == "Respondents") // EWHC/Ch/2017/916
        {
            return PartyRole.Respondent;
        }

        if (one == "Claimant/" && two.EndsWith("Defendant")) // EWHC/Ch/2008/2079
        {
            return PartyRole.Claimant;
        }

        if (one == "Claimant/" && two == "Appellant") // EWCA/Civ/2011/1277
        {
            return PartyRole.Appellant;
        }

        if (one == "Respondent/" && two == "Claimant") // EWCA/Civ/2017/97
        {
            return PartyRole.Respondent;
        }

        if (one == "Appellants/" && two == "Defendants")
        {
            return PartyRole.Appellant;
        }

        if (one == "Appellants/" && two == "Defendants & Counterclaimants") // EWCA/Civ/2017/97
        {
            return PartyRole.Appellant;
        }

        if (one == "Appellants/" && two == "Claimants") // EWCA/Civ/2015/377
        {
            return PartyRole.Appellant;
        }

        if (one == "Appellants" && two == "Claimants") // EWCA/Civ/2018/601
        {
            return PartyRole.Appellant;
        }

        if (one == "Respondent" && two == "Intervener") // EWCA/Civ/2016/176
        {
            return PartyRole.Respondent;
        }

        if (one == "Respondents" && two.StartsWith("Respondent")) // EWHC/Fam/2013/1956
        {
            return PartyRole.Respondent;
        }

        if (one == "Appellant/" && two == "Claimant") // EWHC/Ch/2017/541
        {
            return PartyRole.Appellant;
        }

        if (one == "Appellant" && two == "/Claimant") // [2021] EWHC 3453 (QB)
        {
            return PartyRole.Appellant;
        }

        if (one == "Appellant/" && two == "Defendant") // EWHC/QB/2013/196
        {
            return PartyRole.Appellant;
        }

        if (one == "Respondent/" && two == "Defendant") // EWHC/Ch/2017/541
        {
            return PartyRole.Respondent;
        }

        if (one == "Respondent" && two == "Defendant") // EWCA/Civ/2018/601
        {
            return PartyRole.Respondent;
        }

        if (one == "1st Respondent" && two == "/Defendant") // [2021] EWHC 3453 (QB)
        {
            return PartyRole.Respondent;
        }

        if (one == "Claimants/" && two == "Appellants") // EWHC/Admin/2016/321
        {
            return PartyRole.Appellant;
        }

        if (one == "Defendant/" && two == "Respondent") // EWHC/Admin/2015/1639
        {
            return PartyRole.Respondent;
        }

        if (one == "Defendants/" && two == "Respondents") // EWHC/Admin/2016/321
        {
            return PartyRole.Respondent;
        }

        if (one == "Defendants/" && two == "Appellants") // EWCA/Civ/2004/277
        {
            return PartyRole.Appellant;
        }

        if (one == "Defendants/" && two == "Applicants") // [2021] EWHC 2684 (Comm)
        {
            return PartyRole.Applicant;
        }

        if (one == "1st Respondent" && two == "2nd Respondent") // EWHC/Fam/2017/364, EWHC/Fam/2013/1864?
        {
            return PartyRole.Respondent;
        }

        if (one == "1st Respondent" && two == "2ndRespondent") // EWCA/Civ/2011/1253
        {
            return PartyRole.Respondent;
        }

        if (one == "First Defendant" && two == "Second Defendant") // EWHC/Admin/2010/2
        {
            return PartyRole.Defendant;
        }

        if (one == "Defendant/Part 20 Claimant" && two == "Part 20 Claimant") // EWHC/Ch/2003/812
        {
            return PartyRole.Defendant;
        }

        if (one == "1st Defendant/Part 20 Claimant" && two == "2nd Defendant/Part 20 Defendant") // EWHC/QB/2004/1260
        {
            return PartyRole.Defendant;
        }

        if (one == "Defendant/" && two == "Cross appellant") // EWHC/QB/2013/652
        {
            return PartyRole.Defendant; // ??? other role is Appellant
        }

        if (one == "Applicant/" && two == "Respondent") // [2021] EWCA Civ 1725
        {
            return PartyRole.Respondent;
        }

        if (one == "Appellant/" && two == "Respondent") // [2020] EWHC 3409 (QB)
        {
            return PartyRole.Respondent;
        }

        if (one == "Respondent/" && two == "Appellant") // [2021] EWCA Civ 1961
        {
            return PartyRole.Appellant;
        }

        return GetPartyRoleForCombinedLabels(one, two);
    }

    private static PartyRole? GetNLinePartyRole(WCell cell)
    {
        var blocks = cell.Contents.Where(block => !IsEmptyLine(block)).ToArray();
        if (blocks.Length < 2)
        {
            return null;
        }

        if (!blocks.All(block => block is WLine))
        {
            return null;
        }

        if (blocks.Length == 3)
        {
            var one = ((WLine)blocks[0]).NormalizedContent;
            var two = ((WLine)blocks[1]).NormalizedContent;
            var three = ((WLine)blocks[2]).NormalizedContent;
            if (one == "Defendants" && two == "Part 20 Claimant/" && three == "Appellant")
            {
                return PartyRole.Appellant;
            }

            if (one == "Respondents" && two == "Appellant" && three == "Respondent") // EWCA/Civ/2010/180
            {
                return PartyRole.Respondent;
            }
        }

        Func<WLine, bool> defendant = line =>
        {
            var normalized = line.NormalizedContent;
            return Regex.IsMatch(normalized, @"^\d(st|nd|rd|th)? Defendant$");
        };
        if (blocks.Cast<WLine>().All(defendant))
        {
            return PartyRole.Defendant;
        }

        Func<WLine, bool> defendant2 = line =>
        {
            // EWHC/Fam/2003/365
            var normalized = line.NormalizedContent;
            return Regex.IsMatch(normalized, @"^(First|Second|Third|Fourth) Defendant$", RegexOptions.IgnoreCase);
        };
        if (blocks.Cast<WLine>().All(defendant2))
        {
            return PartyRole.Defendant;
        }

        Func<WLine, bool> appellant = line =>
        {
            var normalized = line.NormalizedContent;
            return Regex.IsMatch(normalized, @"^\d(st|nd|rd|th)? Appellant$", RegexOptions.IgnoreCase);
        };
        if (blocks.Cast<WLine>().All(appellant))
        {
            return PartyRole.Appellant;
        }

        Func<WLine, bool> respondent = line =>
        {
            var normalized = line.NormalizedContent;
            return Regex.IsMatch(normalized, @"^(\d(st|nd|rd|th)? ?)?Respondent$",
                RegexOptions.IgnoreCase); // EWFC/HCJ/2014/34, no space in EWHC/Fam/2013/1864
        };
        if (blocks.Cast<WLine>().All(respondent))
        {
            return PartyRole.Respondent;
        }

        Func<WLine, bool> respondent2 = line =>
        {
            var normalized = line.NormalizedContent;
            return Regex.IsMatch(normalized, @"^(First|Second|Third|Fourth) Respondent$", RegexOptions.IgnoreCase);
        };
        if (blocks.Cast<WLine>().All(respondent2))
        {
            return PartyRole.Respondent;
        }

        return null;
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

    public static PartyRole? GetPartyRole(string s)
    {
        if (s.Contains('/'))
        {
            var (one, two) = s.Split('/', 2) switch { var x => (x[0], x[1]) };
            if (!string.IsNullOrWhiteSpace(one) &&
                !string.IsNullOrWhiteSpace(two)) // not if at beginning or end of line
            {
                return GetPartyRoleForCombinedLabels(one, two);
            }
        }

        if (s.Contains(" and "))
        {
            var (one, two) = s.Split(" and ", 2) switch { var x => (x[0], x[1]) };
            if (!string.IsNullOrWhiteSpace(one) && !string.IsNullOrWhiteSpace(two))
            {
                return GetPartyRoleForCombinedLabels(one, two);
            }
        }

        return GetPartyRoleForSingleLabel(s);
    }

    private static PartyRole? GetPartyRoleForSingleLabel(string s)
    {
        s = Regex.Replace(s, @"\s+", " ").Trim(' ', '/', '(', ')');
        if (s.StartsWith("Part 20 ", StringComparison.InvariantCultureIgnoreCase))
        {
            s = s.Substring(8);
        }

        if (s.Equals("Third Party", StringComparison.InvariantCultureIgnoreCase))
        {
            return PartyRole.ThirdParty;
        }

        ISet<string> starts = new HashSet<string>
        {
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
        };
        foreach (var start in starts)
        {
            if (s.StartsWith(start, StringComparison.InvariantCultureIgnoreCase))
            {
                s = s.Substring(s.IndexOf(' ') + 1);
                break;
            }
        }

        s = s.ToLower();
        if (s == "appellant" || s == "appellants")
        {
            return PartyRole.Appellant;
        }

        if (s == "applicant" || s == "applicants")
        {
            return PartyRole.Applicant;
        }

        if (s == "claimant" || s == "claimants")
        {
            return PartyRole.Claimant;
        }

        if (s == "defendant" || s == "defendants")
        {
            return PartyRole.Defendant;
        }

        if (s == "petitioner" || s == "petitioners")
        {
            return PartyRole.Petitioner;
        }

        if (s == "respondent" || s == "respondents")
        {
            return PartyRole.Respondent;
        }

        if (s == "interested party" || s == "interested parties")
        {
            return PartyRole.InterestedParty;
        }

        if (s == "intervener" || s == "interveners")
        {
            return PartyRole.Intervener;
        }

        if (s == "requested person" || s == "requested persons") // [2022] EWHC 273 (Admin)
        {
            return PartyRole.RequestedPerson;
        }

        if (s == "requesting state")
        {
            return PartyRole.RequestingState;
        }

        return null;
    }

    private static PartyRole? GetPartyRoleForCombinedLabels(string s1, string s2)
    {
        var role1 = GetPartyRole(s1);
        var role2 = GetPartyRole(s2);
        if (role1 is null || role2 is null)
        {
            return null;
        }

        if (role1 == PartyRole.Appellant || role2 == PartyRole.Appellant)
        {
            return PartyRole.Appellant;
        }

        if (role1 == PartyRole.Respondent || role2 == PartyRole.Respondent)
        {
            return PartyRole.Respondent;
        }

        if (role1 == PartyRole.Claimant && role2 == PartyRole.Defendant) // [2022] EWCA Civ 102
        {
            return PartyRole.Claimant;
        }

        if (role1 == PartyRole.Defendant && role2 == PartyRole.Applicant) // [2019] EWHC 3963 (QB)
        {
            return PartyRole.Applicant;
        }

        return null;
    }
}
