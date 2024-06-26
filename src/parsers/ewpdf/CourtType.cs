
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse {

class CourtTypePDF : CourtType {

    private static Combo4[] combo4s = new Combo4[] {
        new Combo4 {
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^TECHNOLOGY AND CONSTRUCTION COURT \(QBD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        }
    };

    override protected List<WLine> Match4(IBlock one, IBlock two, IBlock three, IBlock four) {
        foreach (Combo4 combo in combo4s)
            if (combo.Match(one, two, three, four))
                return combo.Transform(one, two, three, four);
        return base.Match4(one, two, three, four);
    }

    /* two */

    override protected List<WLine> Match2(IBlock block1, IBlock block2) {
        List<WLine> value = base.Match2(block1, block2);
        if (value is not null)
            return value;
        if (block1 is not WLine line1)
            return null;
        if (block2 is not WLine line2)
            return null;
        IEnumerable<IInline> filtered1 = line1.Contents.Where(inline => inline is IFormattedText text && !string.IsNullOrWhiteSpace(text.Text));
        IEnumerable<IInline> filtered2 = line2.Contents.Where(inline => inline is IFormattedText text && !string.IsNullOrWhiteSpace(text.Text));
        if (filtered1.Count() == 1 && filtered2.Count() == 2) {
            if (filtered1.First() is not WText one)
                return null;
            if (filtered2.First() is not WText two)
                return null;
            if (filtered2.ElementAt(1) is not WText three)
                return null;
            foreach (Combo3 combo in Combo3.combos) {
                if (!combo.Re1.IsMatch(one.Text.Trim()))
                    continue;
                if (!combo.Re2.IsMatch(two.Text.Trim()))
                    continue;
                if (!combo.Re3.IsMatch(three.Text.Trim()))
                    continue;
                return new List<WLine>(3) {
                    WLine.Make(line1, new List<IInline>(1) { new WCourtType(one, combo.Court) }),
                    WLine.Make(line2, new List<IInline>(1) { new WCourtType(two, combo.Court) }),
                    WLine.Make(line2, new List<IInline>(1) { new WCourtType(three, combo.Court) })
                };
            }
        }
        return null;
    }

    /* one */

    override protected List<WLine> Match1(IBlock block) {
        List<WLine> value = base.Match1(block);
        if (value is not null)
            return value;
        return Split(block);
    }

    private List<WLine> Split(IBlock block) {
        if (block is not WLine line)
            return null;
        IEnumerable<IInline> filtered = line.Contents.Where(inline => inline is IFormattedText text ? !string.IsNullOrWhiteSpace(text.Text) : true);
        if (filtered.Count() == 1)
            return Split1(filtered, line);
        if (filtered.Count() == 3)
            return Split3(filtered, line);
        if (filtered.Count() > 3)
            return SplitMoreThan3(filtered, line);
        return null;
    }

    IDictionary<string, Court> threes = new Dictionary<string, Court>() {
        { @"^(IN THE HIGH COURT OF JUSTICE) (QUEEN[’']?S BENCH DIVISION) (ADMINISTRATIVE COURT)$", Courts.EWHC_QBD_Administrative },
        { @"^(IN THE HIGH COURT OF JUSTICE) (KING[’']?S BENCH DIVISION) (ADMINISTRATIVE COURT)$", Courts.EWHC_KBD_Administrative }
    };

    private List<WLine> Split1(IEnumerable<IInline> filtered, WLine prototype) {
        if (filtered.First() is not WText one)
            return null;
        foreach (var item in threes) {
            Match match = Regex.Match(one.Text.Trim(), item.Key);
            if (match.Success) {
                return new List<WLine>(3) {
                    WLine.Make(prototype, new List<IInline>(1) { new WCourtType(match.Groups[1].Value, one.properties) { Code = item.Value.Code } }),
                    WLine.Make(prototype, new List<IInline>(1) { new WCourtType(match.Groups[2].Value, one.properties) { Code = item.Value.Code } }),
                    WLine.Make(prototype, new List<IInline>(1) { new WCourtType(match.Groups[3].Value, one.properties) { Code = item.Value.Code } })
                };
            }
        }
        return null;
    }

    private List<WLine> Split3(IEnumerable<IInline> filtered, WLine prototype) {
        if (filtered.First() is not WText one)
            return null;
        if (filtered.ElementAt(1) is not WText two)
            return null;
        if (filtered.ElementAt(2) is not WText three)
            return null;
        foreach (Combo3 combo in Combo3.combos) {
            if (!combo.Re1.IsMatch(one.Text.Trim()))
                continue;
            if (!combo.Re2.IsMatch(two.Text.Trim()))
                continue;
            if (!combo.Re3.IsMatch(three.Text.Trim()))
                continue;
            return new List<WLine>(3) {
                WLine.Make(prototype, new List<IInline>(1) { new WCourtType(one, combo.Court) }),
                WLine.Make(prototype, new List<IInline>(1) { new WCourtType(two, combo.Court) }),
                WLine.Make(prototype, new List<IInline>(1) { new WCourtType(three, combo.Court) })
            };
        }
        return null;
    }

    private List<WLine> SplitMoreThan3(IEnumerable<IInline> filtered, WLine line) {
        List<WLine> firstTry = SplitMoreThan3Beginning(filtered, line);
        if (firstTry is not null)
            return firstTry;
        return SplitMoreThan3End(filtered, line);
    }

    private List<WLine> SplitMoreThan3Beginning(IEnumerable<IInline> filtered, WLine line) {
        if (filtered.First() is not WText one)
            return null;
        if (filtered.ElementAt(1) is not WText two)
            return null;
        if (filtered.ElementAt(2) is not WText three)
            return null;
        foreach (Combo3 combo in Combo3.combos) {
            if (!combo.Re1.IsMatch(one.Text.Trim()))
                continue;
            if (!combo.Re2.IsMatch(two.Text.Trim()))
                continue;
            if (!combo.Re3.IsMatch(three.Text.Trim()))
                continue;
            List<IInline> after = new List<IInline>();
            var enumerator = line.Contents.GetEnumerator();
            while (enumerator.MoveNext() && !object.ReferenceEquals(enumerator.Current, three))
                ;
            // skip the next if it is whitespace
            if (enumerator.MoveNext()) {
                if (enumerator.Current is not IFormattedText text || !string.IsNullOrWhiteSpace(text.Text))
                    after.Add(enumerator.Current);
            }
            while (enumerator.MoveNext())
                after.Add(enumerator.Current);
            return new List<WLine>(4) {
                WLine.Make(line, new List<IInline>(1) { new WCourtType(one, combo.Court) }),
                WLine.Make(line, new List<IInline>(1) { new WCourtType(two, combo.Court) }),
                WLine.Make(line, new List<IInline>(1) { new WCourtType(three, combo.Court) }),
                WLine.Make(line, after),
            };
        }
        return null;
    }
    private List<WLine> SplitMoreThan3End(IEnumerable<IInline> filtered, WLine line) {
        if (filtered.SkipLast(2).Last() is not WText one)
            return null;
        if (filtered.SkipLast(1).Last() is not WText two)
            return null;
        if (filtered.Last() is not WText three)
            return null;
        foreach (Combo3 combo in Combo3.combos) {
            if (!combo.Re1.IsMatch(one.Text.Trim()))
                continue;
            if (!combo.Re2.IsMatch(two.Text.Trim()))
                continue;
            if (!combo.Re3.IsMatch(three.Text.Trim()))
                continue;
            List<IInline> before = new List<IInline>();
            var enumerator = line.Contents.GetEnumerator();
            while (enumerator.MoveNext() && !object.ReferenceEquals(enumerator.Current, one))
                before.Add(enumerator.Current);
            return new List<WLine>(4) {
                WLine.Make(line, before),
                WLine.Make(line, new List<IInline>(1) { new WCourtType(one, combo.Court) }),
                WLine.Make(line, new List<IInline>(1) { new WCourtType(two, combo.Court) }),
                WLine.Make(line, new List<IInline>(1) { new WCourtType(three, combo.Court) })
            };
        }
        return null;
    }

}

}
