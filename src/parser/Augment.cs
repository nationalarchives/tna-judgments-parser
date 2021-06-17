
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;

namespace UK.Gov.Legislation.Judgments.Parse {

class Augmentation {

    internal static IEnumerable<IBlock> AugmentHeader(IEnumerable<IBlock> header) {
        IEnumerable<IBlock> merged = MergeRuns(header);
        IEnumerable<IBlock> withNeutralCitation = AddNeutralCitation(merged);
        IEnumerable<IBlock> withDocDate = AddDocDate(withNeutralCitation);
        return withDocDate;
    }

    internal static IEnumerable<IDecision> AugmentBody(IEnumerable<IDecision> body) {
        return body.Select(d => new Decision { Author = MergeRuns((WLine) d.Author), Contents = MergeRuns(d.Contents) });
    }
    internal static IEnumerable<IAnnex> AugmentAnnexes(IEnumerable<IAnnex> annexes) {
        return annexes.Select(a => new Annex { Number = a.Number, Contents = MergeRuns(a.Contents) });
    }

    /* merge runs */

    private static IEnumerable<IDivision> MergeRuns(IEnumerable<IDivision> divs) {
        return divs.Select(div => MergeRuns(div));
    }
    private static IDivision MergeRuns(IDivision div) {
        if (div is BigLevel big)
            return new BigLevel() { Number = big.Number, Heading = big.Heading, Children = MergeRuns(big.Children) };
        if (div is CrossHeading xhead)
            return new CrossHeading() { Heading = MergeRuns((WLine) xhead.Heading), Children = MergeRuns(xhead.Children) };
        if (div is GroupOfParagraphs group)
            return new GroupOfParagraphs() { Children = MergeRuns(group.Children) };
        if (div is WNewNumberedParagraph np)
            return new WNewNumberedParagraph(np.Number, MergeRuns(np.Contents));
        if (div is WDummyDivision dummy)
            return new WDummyDivision(MergeRuns(dummy.Contents));
        throw new Exception();
    }

    private static IEnumerable<IBlock> MergeRuns(IEnumerable<IBlock> blocks) {
        return blocks.Select(block => MergeRuns(block));
    }
    private static IBlock MergeRuns(IBlock block) {
        if (block is WOldNumberedParagraph np) {
            // IEnumerable<WLine> merged = np.WContents.Select(line => MergeRuns(line));
            WLine merged = MergeRuns(np);
            return new WOldNumberedParagraph(np.Number, merged);
        }
        if (block is WLine line)
            return MergeRuns(line);
        return block;
    }
    internal static IEnumerable<IInline> MergeRuns(IEnumerable<IInline> unmerged) {
        if (unmerged.Count() <= 1)
            return unmerged;
        List<IInline> merged = new List<IInline>(unmerged.Count());
        IInline last = unmerged.First();
        foreach (IInline next in unmerged.Skip(1)) {
            if (last is WText fText1 && next is WText fText2 && IFormattedText.HaveSameFormatting(fText1, fText2)) {
                last = new WText(fText1.Text + fText2.Text, fText1.properties);
            } else {
                merged.Add(last);
                last = next;
            }
        }
        merged.Add(last);
        return merged;
    }
    private static WLine MergeRuns(WLine line) {
        if (line.Contents.Count() > 1) {
            List<IInline> merged = new List<IInline>(line.Contents.Count());
            IInline last = line.Contents.First();
            foreach (IInline next in line.Contents.Skip(1)) {
                if (last is WText fText1 && next is WText fText2 && IFormattedText.HaveSameFormatting(fText1, fText2)) {
                    last = new WText(fText1.Text + fText2.Text, fText1.properties);
                } else {
                    merged.Add(last);
                    last = next;
                }
            }
            merged.Add(last);
            return new WLine(line, merged);
        }
        return line;
    }

    /* add neutral citation */

    private static readonly string[] patterns = {
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EWCA (Civ|Crim) \d+)",
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EWHC \d+ \((Admin|Ch|Comm|QB)\))"
    };
    private static readonly string[] patterns2 = {
        @"^(\[\d{4}\] EWCA (Civ|Crim) \d+)",
        @"^(\[\d{4}\] EWHC \d+ \((Admin|Ch|Comm|QB)\))"
    };

    private static Group Match(string text) {
        foreach (string pattern in patterns) {
            Match match = Regex.Match(text, pattern);
            if (match.Success)
                return match.Groups[2];
        }
        return null;
    }
    private static Group Match2(string text) {
        foreach (string pattern in patterns2) {
            Match match = Regex.Match(text, pattern);
            if (match.Success)
                return match.Groups[1];
        }
        return null;
    }

    private static IInline[] Replace(WText fText, Group group) {
        string before = fText.Text.Substring(0, group.Index);
        string during = group.Value;
        string after = fText.Text.Substring(group.Index + group.Length);
        IInline[] replacement = {
            new WText(before, fText.properties),
            new WNeutralCitation(during, fText.properties),
            new WText(after, fText.properties)
        };
        return replacement;
    }

    private static IEnumerable<IBlock> AddNeutralCitation(IEnumerable<IBlock> blocks) {
        return blocks.Select(block => AddNeutralCitation(block));
    }

    private static IBlock AddNeutralCitation(IBlock block) {
        if (block is WOldNumberedParagraph np) {
            // IEnumerable<WLine> withNeutralCitation = np.WContents.Select(line => AddNeutralCitation(line));
            WLine withNeutralCitation = AddNeutralCitation(np);
            return new WOldNumberedParagraph(np.Number, withNeutralCitation);
        }
        if (block is WLine line)
            return AddNeutralCitation(line);
        return block;
    }

    private static WLine AddNeutralCitation(WLine line) {
        if (line.Contents.Count() > 0) {
            IInline first = line.Contents.First();
            if (first is WText fText) {
                Group group = Match(fText.Text);
                if (group is not null) {
                    IInline[] replacement = Replace(fText, group);
                    IEnumerable<IInline> rest = line.Contents.Skip(1);
                    IEnumerable<IInline> combined = Enumerable.Concat(replacement, rest);
                    return new WLine(line, combined);
                }
            }
        }
        if (line.Contents.Count() > 1) {
            IInline first = line.Contents.First();
            IInline second = line.Contents.Skip(1).First();
            if (first is WText fText1 && second is WText fText2) {
                if (fText1.Text == "Neutral Citation Number: ") {
                    Group group = Match2(fText2.Text);
                    if (group is not null) {
                        IInline[] replacement = Replace(fText2, group);
                        IEnumerable<IInline> rest = line.Contents.Skip(2);
                        IEnumerable<IInline> combined = Enumerable.Concat(replacement, rest).Prepend(first);
                        return new WLine(line, combined);
                    }
                }
            }
        }
        return line;
    }

    /* add date */

    private static readonly string[] datePatterns = {
        @"^Date: (\d{2}/\d{2}/\d{4})$",
        @"^Date: (\d{1,2} (January|February|March|April|May|June|July|August|September|October|November|December) \d{4})$"
    };
    private static Group MatchDate(string text) {
        foreach (string pattern in datePatterns) {
            Match match = Regex.Match(text, pattern);
            if (match.Success)
                return match.Groups[1];
        }
        return null;
    }

    private static IEnumerable<IBlock> AddDocDate(IEnumerable<IBlock> blocks) {
        // int limit = 15;
        // IEnumerable<IBlock> first = blocks.Take(limit);
        // IEnumerable<IBlock> rest = blocks.Skip(limit);
        // first = first.Select(block => {
        return blocks.Select(block => {
            if (block is WOldNumberedParagraph np) {
                // IEnumerable<WLine> withDocDate = np.WContents.Select(line => AddDocDate(line));
                WLine withDocDate = AddDocDate(np);
                return new WOldNumberedParagraph(np.Number, withDocDate);
            }
            if (block is WLine line)
                return AddDocDate(line);
            return block;
        });
        // return Enumerable.Concat(first, rest);
    }

    private static WLine AddDocDate(WLine line) {
        CultureInfo culture = new CultureInfo("en-GB");
        if (line.Contents.Count() == 1) {
            IInline inline = line.Contents.First();
            if (inline is WText fText) {
                Group group = MatchDate(fText.Text);
                if (group is not null) {
                    WText label = new WText(fText.Text.Substring(0, group.Index), fText.properties);
                    DateTime date = DateTime.Parse(group.Value, culture);
                    WDocDate docDate = new WDocDate(group.Value, fText.properties, date);
                    IInline[] replacement = { label, docDate };
                    return new WLine(line, replacement);
                }
            }
        }
        if (line.Contents.Count() == 3) {
            IInline first = line.Contents.First();
            IInline second = line.Contents.ElementAt(1);
            IInline third = line.Contents.ElementAt(2);
            if (first is WText fText1) {
                if (second is WText fText2) {
                    if (third is WText fText3) {
                        string pattern1 = @"^(Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday), \d{1,2}$";
                        string pattern2 = @"^(st|nd|rd|th)$";
                        string pattern3 = @"^ (January|February|March|April|May|June|July|August|September|October|November|December) \d{4}$";
                        Match match1 = Regex.Match(fText1.Text, pattern1);
                        Match match2 = Regex.Match(fText2.Text, pattern2);
                        Match match3 = Regex.Match(fText1.Text, pattern3);
                        if (match1.Success && match2.Success && match3.Success) {
                            string combined = fText1.Text + fText2.Text + fText3.Text;
                            DateTime date = DateTime.Parse(combined, culture);
                            IInline[] contents = { new WDocDate(line.Contents.Cast<WText>(), date) };
                            return new WLine(line, contents);
                        }
                    }
                }
            }
        }
        return line;
    }

}

}
