
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse {

abstract class Combo {

    protected bool Match(Regex regex, IBlock block) {
        if (block is not WLine line)
            return false;
        if (!line.Contents.Any())
            return false;
        IInline first = line.Contents.First();
        if (first is IImageRef) {    // EWHC/Comm/2009/2472
            if (!line.Contents.Skip(1).All(inline => inline is WText))
                return false;
        } else if (first is ILineBreak) {
            if (!line.Contents.Skip(1).All(inline => inline is WText))
                return false;
        } else {
            if (!line.Contents.All(inline => inline is WText))
                return false;
        }
        string text = line.NormalizedContent;
        return regex.IsMatch(text);
    }

    protected static bool MatchFirstRun(Regex regex, IBlock block) {
        if (!(block is WLine line))
            return false;
        if (line.Contents.Count() == 0)
            return false;
        IInline first = line.Contents.First();
        if (first is WText wText) {
            string text = Regex.Replace(wText.Text, @"\s+", " ").Trim();
            return regex.IsMatch(text);
        } else if (first is WImageRef) {
            IInline second = line.Contents.Skip(1).FirstOrDefault();
            if (second is null)
                return false;
            if (second is not WText wText2)
                return false;
            string text = Regex.Replace(wText2.Text, @"\s+", " ").Trim();
            return regex.IsMatch(text);
        }
        return false;
    }

    protected bool MatchThirdRun(Regex regex, IBlock block) {
        if (!(block is WLine line))
            return false;
        if (line.Contents.Count() < 3)
            return false;
        IInline second = line.Contents.ElementAt(1);
        IInline third = line.Contents.ElementAt(2);
        if (second is not WLineBreak)
            return false;
        if (third is not WText wText)
            return false;
        string text = Regex.Replace(wText.Text, @"\s+", " ").Trim();
        return regex.IsMatch(text);
    }

    protected bool MatchFirstAndThirdRuns(IBlock block, Regex re1, Regex re2) {
        if (!(block is WLine line))
            return false;
        if (line.Contents.Count() < 3)
            return false;
        IInline first = line.Contents.ElementAt(0);
        IInline second = line.Contents.ElementAt(1);
        IInline third = line.Contents.ElementAt(2);
        if (first is not WText wText1)
            return false;
        if (second is not WLineBreak)
            return false;
        if (third is not WText wText2)
            return false;
        string text1 = Regex.Replace(wText1.Text, @"\s+", " ").Trim();
        if (!re1.IsMatch(text1))
            return false;
        string text2 = Regex.Replace(wText2.Text, @"\s+", " ").Trim();
        if (!re2.IsMatch(text2))
            return false;
        return true;
    }
    protected WLine TransformFirstAndThirdRuns(IBlock block) {
        WLine line = (WLine) block;
        WText first = (WText) line.Contents.ElementAt(0);
        WLineBreak second = (WLineBreak) line.Contents.ElementAt(1);
        WText third = (WText) line.Contents.ElementAt(2);
        WCourtType ct1 = new WCourtType(first.Text, first.properties) { Code = this.Court.Code };
        WCourtType ct3 = new WCourtType(third.Text, third.properties) { Code = this.Court.Code };
        IEnumerable<IInline> contents = line.Contents.Skip(3).Prepend(ct3).Prepend(second).Prepend(ct1);
        return WLine.Make(line, contents);
    }

    public Court Court { get; init; }

    protected WLine Transform1(IBlock block) {
        WLine line = (WLine) block;
        if (line.Contents.Count() == 1) {
            WText text = (WText) line.Contents.First();
            WCourtType ct = new WCourtType(text.Text, text.properties) { Code = this.Court.Code };
            return WLine.Make(line, new List<IInline>(1) { ct });
        } else if (line.Contents.First() is IImageRef) {
            WCourtType2 ct = new WCourtType2() { Code = this.Court.Code, Contents = line.Contents.Skip(1).Cast<WText>() };
            return WLine.Make(line, new List<IInline>(2) { line.Contents.First(), ct });
        } else if (line.Contents.First() is ILineBreak) {
            WCourtType2 ct = new WCourtType2() { Code = this.Court.Code, Contents = line.Contents.Skip(1).Cast<WText>() };
            return WLine.Make(line, new List<IInline>(2) { line.Contents.First(), ct });
        } else {
            WCourtType2 ct = new WCourtType2() { Code = this.Court.Code, Contents = line.Contents.Cast<WText>() };
            return WLine.Make(line, new List<IInline>(1) { ct });
        }
    }

    protected WLine TransformFirstRun(IBlock block) {
        WLine line = (WLine) block;
        IInline first = line.Contents.First();
        if (first is WImageRef) {
            WText wText2 = (WText) line.Contents.Skip(1).First();
            WCourtType ct = new WCourtType(wText2.Text, wText2.properties) { Code = this.Court.Code };
            IEnumerable<IInline> contents = line.Contents.Skip(2).Prepend(ct).Prepend(first);
            return WLine.Make(line, contents);
        } else {
            WText wText1 = (WText) first;
            WCourtType ct = new WCourtType(wText1.Text, wText1.properties) { Code = this.Court.Code };
            IEnumerable<IInline> contents = line.Contents.Skip(1).Prepend(ct);
            return WLine.Make(line, contents);
        }
    }

    protected WLine TransformFirstThreeRuns(IBlock block) {
        WLine line = (WLine) block;
        IEnumerable<IInline> text = line.Contents.Take(3);
        WCourtType2 ct = new WCourtType2() { Code = this.Court.Code, Contents = text };
        IEnumerable<IInline> contents = line.Contents.Skip(3).Prepend(ct);
        return WLine.Make(line, contents);
    }

    protected static Regex ConvertQueensToKings(Regex re) {
        return new Regex(re.ToString().Replace("Queen", "King").Replace("QUEEN", "KING").Replace("QBD", "KBD"), re.Options);
    }
    protected static Court ConvertQueensToKings(Court court) {
        string code = court.Code.Replace("-QBD", "-KBD");
        return Courts.GetByCode(code);
    }

    protected abstract Combo ConvertQueensToKings();

}

class Combo6 : Combo {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }
    public Regex Re4 { get; init; }
    public Regex Re5 { get; init; }
    public Regex Re6 { get; init; }

    internal static Combo6[] combos = new Combo6[] {
        new Combo6 {    // [2022] EWHC 219 (Comm)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^COMMERCIAL COURT$", RegexOptions.IgnoreCase),
            Re5 = new Regex(@"^QUEEN['’]S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re6 = new Regex(@"^FINANCIAL LIST$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial_Financial
        }
    };

    internal bool Match(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five, IBlock six) {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three) && Match(Re4, four) && Match(Re5, five) && Match(Re6, six);
    }

    internal List<WLine> Transform(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five, IBlock six) {
        return new List<WLine>(6) {
            Transform1(one),
            Transform1(two),
            Transform1(three),
            Transform1(four),
            Transform1(five),
            Transform1(six)
        };
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five, IBlock six) {
        foreach (Combo6 combo in combos)
            if (combo.Match(one, two, three, four, five, six))
                return combo.Transform(one, two, three, four, five, six);
        return null;
    }

    override protected Combo6 ConvertQueensToKings() {
        if (!this.Court.Code.Contains("-QBD"))
            throw new Exception();
        return new Combo6 {
            Re1 = this.Re1,
            Re2 = this.Re2,
            Re3 = this.Re3,
            Re4 = this.Re4,
            Re5 = ConvertQueensToKings(this.Re5),
            Re6 = this.Re6,
            Court = ConvertQueensToKings(this.Court)
        };
    }
    static Combo6() {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = combos.Concat(kings).ToArray();
    }

}

class Combo5 : Combo {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }
    public Regex Re4 { get; init; }
    public Regex Re5 { get; init; }

    internal static Combo5[] combos = new Combo5[] {
        new Combo5 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^QUEEN['’]S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re5 = new Regex("^COMMERCIAL COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial
        }
    };

    internal bool Match(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five) {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three) && Match(Re4, four) && Match(Re5, five);
    }

    internal List<WLine> Transform(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five) {
        return new List<WLine>(5) {
            Transform1(one),
            Transform1(two),
            Transform1(three),
            Transform1(four),
            Transform1(five)
        };
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five) {
        foreach (Combo5 combo in Combo5.combos)
            if (combo.Match(one, two, three, four, five))
                return combo.Transform(one, two, three, four, five);
        return null;
    }

    override protected Combo5 ConvertQueensToKings() {
        if (!this.Court.Code.Contains("-QBD"))
            throw new Exception();
        return new Combo5 {
            Re1 = this.Re1,
            Re2 = this.Re2,
            Re3 = this.Re3,
            Re4 = ConvertQueensToKings(this.Re4),
            Re5 = this.Re5,
            Court = ConvertQueensToKings(this.Court)
        };
    }
    static Combo5() {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = combos.Concat(kings).ToArray();
    }

}

class Combo4 : Combo {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }
    public Regex Re4 { get; init; }

    internal static Combo4[] combos = new Combo4[] {
        new Combo4 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN'S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^ADMINISTRATIVE COURT", RegexOptions.IgnoreCase),
            Re4 = new Regex("^PLANNING COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Planning
        },
        new Combo4 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN'S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^LEEDS DISTRICT REGISTRY$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^PLANNING COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Planning
        },
        new Combo4 {    // EWHC/TCC/2018/2802
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN['’]S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^BUSINESS AND PROPERTY COURTS$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^TECHNOLOGY AND CONSTRUCTION COURT", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new Combo4 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN['’]S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^[A-Z]+ DISTRICT REGISTRY$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^TECHNOLOGY AND CONSTRUCTION COURT", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new Combo4 {    // EWHC/Ch/2014/1553
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^INTELLECTUAL PROPERTY and$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^COMMUNITY TRADE MARK COURT", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IPEC
        },
        new Combo4 {    // [2021] EWHC 3295 (Pat)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^INTELLECTUAL PROPERTY LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^PATENTS COURT", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Patents
        },
        new Combo4 {    // [2021] EWHC 3296 (IPEC)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^INTELLECTUAL PROPERTY LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^INTELLECTUAL PROPERTY ENTERPRISE COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IPEC
        },
        new Combo4 {    // [2021] EWHC 2842 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
        new Combo4 {    // [2022] EWHC 34 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^CHANCERY APPEALS$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new Combo4 {
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^CHANCERY APPEALS \(ChD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new Combo4 {    // [2021] EWHC 2972 (TCC), [2021] EWHC 3595 (TCC)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS?$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^OF ENGLAND (AND|&) WALES$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^TECHNOLOGY (AND|&) CONSTRUCTION COURT \(QBD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new Combo4 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^(THE )?BUSINESS AND PROPERTY COURTS OF ENGLAND (AND|&) WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^QUEEN['’]S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^COMMERCIAL COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial
        },
        new Combo4 {    // [2022] EWHC 245 (Comm)
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^QUEEN['’]S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^COMMERCIAL COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial
        },
        new Combo4 {    // [2022] EWHC 544 (Comm), [2022] EWHC 586 (Comm)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS?$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^OF ENGLAND (AND|&) WALES$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^COMMERCIAL COURT \(QBD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial
        },
        new Combo4 {    // [2021] EWHC 3432 (CH)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^BUSINESS LIST \(LONDON\)$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_BusinessList
        },
        new Combo4 {    // [2021] EWHC 3514 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^BUSINESS LIST \(ChD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_BusinessList
        },
        new Combo4 {    // [2021] EWHC 1988 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS (AND|&) PROPERTY COURTS$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^OF ENGLAND (AND|&) WALES$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^INSOLVENCY AND COMPANIES COURT LIST$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        },
        new Combo4 {    // [2022] EWHC (Ch) 1104
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS (AND|&) PROPERTY COURTS OF ENGLAND (AND|&) WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^COMPANIES COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        }
    };

    internal bool Match(IBlock one, IBlock two, IBlock three, IBlock four) {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three) && Match(Re4, four);
    }
    internal List<WLine> Transform(IBlock one, IBlock two, IBlock three, IBlock four) {
        return new List<WLine>(4) {
            Transform1(one),
            Transform1(two),
            Transform1(three),
            Transform1(four)
        };
    }

    internal bool Match2(IBlock one, IBlock two, IBlock three, IBlock four) {
        return MatchFirstRun(Re1, one) && MatchFirstRun(Re2, two) && MatchFirstRun(Re3, three) && MatchFirstRun(Re4, four);
    }
    internal List<WLine> Transform2(IBlock one, IBlock two, IBlock three, IBlock four) {
        return new List<WLine>(4) {
            TransformFirstRun(one), TransformFirstRun(two), TransformFirstRun(three), TransformFirstRun(four)
        };
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three, IBlock four) {
        foreach (Combo4 combo in Combo4.combos)
            if (combo.Match(one, two, three, four))
                return combo.Transform(one, two, three, four);
        foreach (Combo4 combo in Combo4.combos)
            if (combo.Match2(one, two, three, four))
                return combo.Transform2(one, two, three, four);
        return null;
    }

    override protected Combo4 ConvertQueensToKings() {
        if (!this.Court.Code.Contains("-QBD"))
            return null;
        return new Combo4 {
            Re1 = this.Re1,
            Re2 = ConvertQueensToKings(this.Re2),
            Re3 = ConvertQueensToKings(this.Re3),
            Re4 = ConvertQueensToKings(this.Re4),
            Court = ConvertQueensToKings(this.Court)
        };
    }
    static Combo4() {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = combos.Concat(kings).ToArray();
    }

}

class Combo3_1 : Combo {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }
    public Regex Re4 { get; init; }

    internal static Combo3_1[] combos = new Combo3_1[] {
        new Combo3_1 {   // [2021] EWHC 3347 (Ch), [2021] EWHC 3096 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND (AND|&) WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^INTELLECTUAL PROPERTY LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"Rolls Buildings?$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IntellectualProperty
        },
        new Combo3_1 {   // [2021] EWHC 3385 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY? COURTS OF ENGLAND (AND|&) WALES$", RegexOptions.IgnoreCase),  // no "Y" in [2021] EWHC 3385 (Ch)
            Re3 = new Regex(@"^INTELLECTUAL PROPERTY LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^Royal Courts of Justice$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IntellectualProperty
        },
        new Combo3_1 {   // [2021] EWHC 3502 (QB)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^ROYAL COURTS OF JUSTICE$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^Queen['’]s Bench Division$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^Neutral Citation Number", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new Combo3_1 {   // [2022] EWHC 421 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re4 = new Regex(@"^7 Rolls Buildings$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
    };

    private bool Match(IBlock one, IBlock two, IBlock three, IBlock four) {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three) && Match(Re4, four);
    }

    private List<WLine> Transform(IBlock one, IBlock two, IBlock three, IBlock four) {
        return new List<WLine>(4) {
            Transform1(one), Transform1(two), Transform1(three), (WLine) four
        };
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three, IBlock four) {
        foreach (Combo3_1 combo in Combo3_1.combos)
            if (combo.Match(one, two, three, four))
                return combo.Transform(one, two, three, four);
        return null;
    }

    override protected Combo3_1 ConvertQueensToKings() {
        if (!this.Court.Code.Contains("-QBD"))
            return null;
        return new Combo3_1 {
            Re1 = this.Re1,
            Re2 = this.Re2,
            Re3 = ConvertQueensToKings(this.Re3),
            Re4 = ConvertQueensToKings(this.Re4),
            Court = ConvertQueensToKings(this.Court)
        };
    }
    static Combo3_1() {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = combos.Concat(kings).ToArray();
    }

}

class Combo3 : Combo {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }

    internal static Combo3[] combos = new Combo3[] {
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']?S BENCH DIVISION$", RegexOptions.IgnoreCase),   // no apostrophe in EWHC/Admin/2009/573
            Re3 = new Regex("^(THE )?ADMINISTRATIVE COURT", RegexOptions.IgnoreCase),  // "THE" in EWHC/Admin/2006/1205, "... AT ..." in EWHC/Admin/2013/733
            Court = Courts.EWHC_QBD_Administrative
        },
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']?S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^ADMINSTRATIVE COURT", RegexOptions.IgnoreCase),  // spelling mistake in EWHC/Admin/2021/578
            Court = Courts.EWHC_QBD_Administrative
        },
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']?S BENCH DIVISION$", RegexOptions.IgnoreCase),   // no apostrophe in EWHC/Admin/2009/573
            Re3 = new Regex("^ADMIRALTY COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Admiralty
        },
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']?S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^PLANNING COURT", RegexOptions.IgnoreCase),    // can be followed by city name EWHC/Admin/2018/1753
            Court = Courts.EWHC_QBD_Planning
        },
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']?S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^LONDON CIRCUIT COMMERCIAL COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial_Circuit
        },
        new Combo3 {
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS (AND|&) PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^LONDON CIRCUIT COMMERCIAL COURT \(QBD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial_Circuit
        },
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']?S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^TECHNOLOGY AND CONSTRUCTION COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']?S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^BIRMINGHAM DISTRICT REGISTRY$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new Combo3 {    // [2022] EWHC 157 (Comm)
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']?S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND & WALES$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_BusinessAndProperty
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^CHANCERY DIVISION", RegexOptions.IgnoreCase),
            Re3 = new Regex("^PATENTS COURT", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Patents
        },
        new Combo3 {    // EWHC/Patents/2005/1403
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISON$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^\(PATENTS COURT\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Patents
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^CHANCERY DIVISION", RegexOptions.IgnoreCase),
            Re3 = new Regex("^INTELLECTUAL PROPERTY( ENTERPRISE COURT)?$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IPEC
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^INTELLECTUAL PROPERTY ENTERPRISE COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IPEC
        },
        new Combo3 {
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS (AND|&) PROPERTY COURTS OF ENGLAND & WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^INTELLECTUAL PROPERTY LIST \(ChD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IntellectualProperty
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES \\(ChD\\)", RegexOptions.IgnoreCase),
            Re3 = new Regex("^BUSINESS LIST", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_BusinessList
        },
        new Combo3 {    // [2022] EWHC 48 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^BUSINESS LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),  // space in [2023] EWHC 1391 (Ch)
            Court = Courts.EWHC_Chancery_BusinessList
        },
        new Combo3 {    // [2023] EWHC 1439 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS IN BIRMINGHAM$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^BUSINESS LIST$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_BusinessList
        },
        new Combo3 {
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^COMPANIES COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        },
        new Combo3 {    // [2021] EWHC 3199 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS (AND|&) PROPERTY COURTS OF ENGLAND (AND|&) WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^COMPANIES COURT \(ChD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        },
        new Combo3 {    // [2022] EWHC 24 (Ch), [2022] EWHC 202 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS", RegexOptions.IgnoreCase), // ... IN LEEDS
            Re3 = new Regex(@"^INSOLVENCY AND COMPANIES LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES", RegexOptions.IgnoreCase),
            Re3 = new Regex("^CHANCERY APPEALS \\(ChD\\)", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new Combo3 {    // [2021] EWHC 3416 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS & PROPERTY COURTS OF ENGLAND & WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^CHANCERY APPEALS$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new Combo3 {
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^APPEALS \(CH D\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        // no distinction between KB and QB
        // new Combo3 {
        //     Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
        //     Re2 = new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND AND? WALES", RegexOptions.IgnoreCase),    // missing D in EWHC/QB/2017/2921
        //     Re3 = new Regex("^COMMERCIAL COURT$", RegexOptions.IgnoreCase),
        //     Court = Courts.EWHC_QBD_Commercial
        // },
        new Combo3 {    // EWHC/Comm/2018/3326
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES", RegexOptions.IgnoreCase),    // missing D in EWHC/QB/2017/2921
            Re3 = new Regex(@"^COMMERCIAL COURT \(QBD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial
        },
        new Combo3 {    // EWHC/Comm/2009/2941, EWHC/Comm/2004/2750
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN['’]?S BENCH DIVISION$", RegexOptions.IgnoreCase),   // no appostrophe in EWHC/Comm/2003/3161
            Re3 = new Regex("^COMMERCIAL COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial
        },
        new Combo3 {    // EWHC/Admin/2004/1441
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN['’]S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^DIVISIONAL COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD // ??? should it be QBD-General?
        },
        new Combo3 {    // EWHC/QB/2016/1174, EWHC/QB/2017/1748
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^QUEEN['’]?S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^LONDON MERCANTILE COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial_Circuit
        },
        new Combo3 {    // [2021] EWHC 3054 (Comm)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND & WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^LONDON CIRCUIT COMMERCIAL COURT \(QBD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial_Circuit
        },
        // new Combo3 {
        //     Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
        //     Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
        //     Re3 = new Regex(@"^TECHNOLOGY AND CONSTRUCTION COURT$", RegexOptions.IgnoreCase),
        //     Court = Courts.EWHC_QBD_TCC
        // },
        new Combo3 {    // EWHC/TCC/2018/751
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS? OF ENGLAND (AND|&) WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^TECHNOLOGY AND CONSTRUCTION COURT \(QBD?\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new Combo3 {    // EWHC/TCC/2011/3070
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^QUEEN['’]?S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^TECHNOLOGY & CONSTRUCTION COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new Combo3 {    // EWHC/Costs/2012/90218
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^QUEEN['’]?S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^SENIOR COURTS COSTS OFFICE$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_SeniorCourtsCosts
        },
        new Combo3 {    // [2021] EWHC 2950 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^FINANCIAL LIST$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Financial
        },
        new Combo3 {    // [2021] EWHC 3306 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^IN THE BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^FINANCIAL LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Financial
        },
        new Combo3 {    // [2022] EHWC 950 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^(IN THE )?BUSINESS (AND|&) PROPERTY COURTS OF ENGLAND (AND|&) WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^PROPERTY, TRUSTS (AND|&) PROBATE LIST \(ChD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_PropertyTrustsProbate
        },
        new Combo3 {  // [2023] EWHC 654 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS IN LEEDS$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^PROPERTY, TRUSTS (AND|&) PROBATE LIST \(ChD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_PropertyTrustsProbate
        }
    };

    virtual internal bool Match(IBlock one, IBlock two, IBlock three) {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three);
    }

    virtual internal List<WLine> Transform(IBlock one, IBlock two, IBlock three) {
        return new List<WLine>(3) { Transform1(one), Transform1(two), Transform1(three) };
    }

    virtual internal bool MatchFirstRun(IBlock one, IBlock two, IBlock three) {
        return MatchFirstRun(Re1, one) && Match(Re2, two) && Match(Re3, three);
    }
    virtual internal List<WLine> TransformFirstRun(IBlock one, IBlock two, IBlock three) {
        return new List<WLine>(3) { TransformFirstRun(one), Transform1(two), Transform1(three) };
    }

    virtual internal bool MatchTwoFirstRuns(IBlock one, IBlock two, IBlock three) {
        return MatchFirstRun(Re1, one) && MatchFirstRun(Re2, two) && Match(Re3, three);
    }
    virtual internal List<WLine> TransformTwoFirstRuns(IBlock one, IBlock two, IBlock three) {
        return new List<WLine>(3) { TransformFirstRun(one), TransformFirstRun(two), Transform1(three) };
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three) {
        foreach (Combo3 combo in Combo3.combos)
            if (combo.Match(one, two, three))
                return combo.Transform(one, two, three);
        foreach (Combo3 combo in Combo3.combos)
            if (combo.MatchFirstRun(one, two, three))
                return combo.TransformFirstRun(one, two, three);
        return null;
    }
    internal static List<WLine> MatchAny2(IBlock one, IBlock two) {
        foreach (Combo3 combo in Combo3.combos)
            if (combo.Match(combo.Re1, one) && combo.MatchFirstAndThirdRuns(two, combo.Re2, combo.Re3))
                return new List<WLine>(2) { combo.Transform1(one), combo.TransformFirstAndThirdRuns(two) };
        return null;
    }

    override protected Combo3 ConvertQueensToKings() {
        if (!this.Court.Code.Contains("-QBD"))
            return null;
        return new Combo3 {
            Re1 = this.Re1,
            Re2 = ConvertQueensToKings(this.Re2),
            Re3 = ConvertQueensToKings(this.Re3),
            Court = ConvertQueensToKings(this.Court)
        };
    }
    static Combo3() {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = combos.Concat(kings).ToArray();
    }

}

class Combo2_1 : Combo {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }

    internal static Combo2_1[] combos = new Combo2_1[] {
        new Combo2_1 {
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^QUEEN['’]?S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^(The )?Royal Courts of Justice", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new Combo2_1 {
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^QUEEN'’]S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^ON APPEAL FROM", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new Combo2_1 {  // EWHC/QB/2011/3104
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^QUEEN['’]S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^[A-Z]+ DISTRICT REGISTRY$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new Combo2_1 {  // EWHC/QB/2013/2997
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^QUEEN['’]S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^Strand$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new Combo2_1 {  // EWHC/QB/2011/3068
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^QUEEN['’]S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^IN THE MATTER OF"),
            Court = Courts.EWHC_QBD
        },
        new Combo2_1 {  // EWHC/QB/2014/1972
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^QUEEN['’]S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^The Combined Court Centre"),
            Court = Courts.EWHC_QBD
        },
        new Combo2_1 {  // EWHC/Ch/2016/4063
            Re1 = new Regex(@"^IN THE HIGH COURTS? OF JUSTICE$", RegexOptions.IgnoreCase),  // S in EWHC/Ch/2009/2692
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^Royal Courts of Justice", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
        new Combo2_1 {  // EWHC/Ch/2015/274
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^[A-Z][a-z]+ Building, Royal Courts of Justice$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
        new Combo2_1 {  // EWHC/Ch/2008/1893
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^Before", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
        new Combo2_1 {  // EWHC/Ch/2013/200
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^Date", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
        new Combo2_1 {  // EWHC/Ch/2013/160, EWHC/Ch/2012/616
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^[A-Z][a-z]+ Building"),
            Court = Courts.EWHC_Chancery
        },
        new Combo2_1 {  // EWHC/Ch/2018/2783
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^\d+ [A-Z][a-z]+ Building"),
            Court = Courts.EWHC_Chancery
        },
        new Combo2_1 {  // EWHC/Ch/2014/1048
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"Building$"),
            Court = Courts.EWHC_Chancery
        },
        new Combo2_1 {  // EWHC/Ch/2016/1996, ewhc/ch/2011/3553
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^\(?CHANCERY DIVISION\)?$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^[A-Z]+ DISTRICT REGISTRY$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
        new Combo2_1 {  // EWHC/Ch/2016/243
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^IN THE MATTER OF"),
            Court = Courts.EWHC_Chancery
        },
        new Combo2_1 {  // EWHC/Ch/2018/106
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^Bristol Civil Justice Centre$"),
            Court = Courts.EWHC_Chancery
        },
        new Combo2_1 {  // EWHC/Ch/2013/3098
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^IN AN APPEAL FROM", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new Combo2_1 {  // EWHC/Ch/2017/541
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^ON APPEAL FROM", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new Combo2_1 {  // EWHC/Ch/2003/2985
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^Appeal against the decision of", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new Combo2_1 {  // [2021] EWHC 3418 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES \(ChD\)$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^ON APPEAL FROM", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new Combo2_1 {  // [2021] EWHC 3247 (QB)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^QUEEN’S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^\[\d{4}\] EWHC \d+ \([A-Z]+[a-z]*\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new Combo2_1 {  // [2021] EWHC 3260 (Ch)
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^\[\d{4}\] EWHC \d+ \(Ch\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        }
    };

    private bool Matchish(Regex regex, IBlock block) {
        if (!(block is WLine line))
            return false;
        if (line.Contents.Count() == 0)
            return false;
        string text = line.NormalizedContent;
        return regex.IsMatch(text);
    }

    internal bool Match(IBlock one, IBlock two, IBlock three) {
        return Match(Re1, one) && Match(Re2, two) && Matchish(Re3, three);
    }
    private bool Match2(IBlock one, IBlock two, IBlock three) {
        return MatchFirstRun(Re1, one) && Match(Re2, two) && Matchish(Re3, three);
    }

    internal List<WLine> Transform(IBlock one, IBlock two, IBlock three) {
        return new List<WLine>(3) {
            Transform1(one), Transform1(two), (WLine) three
        };
    }
    private List<WLine> Transform2(IBlock one, IBlock two, IBlock three) {
        return new List<WLine>(3) {
            TransformFirstRun(one), Transform1(two), (WLine) three
        };
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three) {
        foreach (Combo2_1 combo in Combo2_1.combos) {
            if (combo.Match(one, two, three))
                return combo.Transform(one, two, three);
            if (combo.Match2(one, two, three))
                return combo.Transform2(one, two, three);
        }
        return null;

    }

    override protected Combo2_1 ConvertQueensToKings() {
        if (!this.Court.Code.Contains("-QBD"))
            return null;
        return new Combo2_1 {
            Re1 = this.Re1,
            Re2 = ConvertQueensToKings(this.Re2),
            Re3 = this.Re3,
            Court = ConvertQueensToKings(this.Court)
        };
    }
    static Combo2_1() {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = combos.Concat(kings).ToArray();
    }

}

class Combo1_2 : Combo {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }

    internal static Combo1_2[] combos = new Combo1_2[] {
        new Combo1_2 {
            Re1 = new Regex(@"^IN THE COURT OF APPEAL$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^ON APPEAL FROM THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),   // no apostrophe in EWHC/Admin/2009/573
            Re3 = new Regex(@"^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Civil
        }
    };

    internal bool Match(IBlock one, IBlock two, IBlock three) {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three);
    }

    internal List<WLine> Transform(IBlock one, IBlock two, IBlock three) {
        return new List<WLine>(3) {
            Transform1(one), (WLine) two, (WLine) three
        };
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three) {
        foreach (Combo1_2 combo in Combo1_2.combos)
            if (combo.Match(one, two, three))
                return combo.Transform(one, two, three);
        return null;
    }

    override protected Combo1_2 ConvertQueensToKings() {
        if (this.Court.Code.Contains("-QBD"))
            throw new Exception();
        return null;
    }
    // static Combo1_2() {
    //     var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
    //     combos = combos.Concat(kings).ToArray();
    // }

}

class Combo2 : Combo {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }

    internal static Combo2[] combos = new Combo2[] {
        new Combo2 {
            Re1 = new Regex("^IN THE HIGH COURT \\(DIVISIONAL\\) COURT &$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^COURT OF APPEAL \\(CIVIL DIVISION\\)", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Civil
        },
        new Combo2 {
            Re1 = new Regex("IN THE SUPREME COURT OF JUDICATURE"),
            Re2 = new Regex("COURT OF APPEAL \\(CIVIL DIVISION\\)"),
            Court = Courts.CoA_Civil
        },
        new Combo2 {
            Re1 = new Regex("IN THE SUPREME COURT OF JUDICATURE"),
            Re2 = new Regex("COURT OF APPEAL \\(CRIMINAL DIVISION\\)"),
            Court = Courts.CoA_Crim
        },
        new Combo2 {
            Re1 = new Regex("IN THE COURT OF APPEAL"),
            Re2 = new Regex("CRIMINAL +DIVISION"),
            Court = Courts.CoA_Crim
        },
        new Combo2 {    // EWHC/Ch/2008/2029
            Re1 = new Regex("IN THE HIGH COURT OF JUSTICE"),
            Re2 = new Regex(@"\(Chancery Division\)"),
            Court = Courts.EWHC_Chancery
        },
        new Combo2 {    // [2023] EWHC 1593 (KB)
            Re1 = new Regex("IN THE HIGH COURT OF JUSTICE"),
            Re2 = new Regex("KING['’]S BENCH DIVISION"),
            Court = Courts.EWHC_KBD
        },
        // new Combo2 {
        //     Re1 = new Regex("IN THE HIGH COURTS? OF JUSTICE"),
        //     Re2 = new Regex("CHANCERY DIVISION"),
        //     Court = Courts.EWHC_Chancery
        // },
        // new Combo2 {
        //     Re1 = new Regex("IN THE HIGH COURT OF JUSTICE"),
        //     Re2 = new Regex("CHANCERY DIVISION (PROBATE)"),
        //     Court = Courts.EWHC_Chancery
        // },
        new Combo2 {
            Re1 = new Regex("IN THE HIGH COURT OF JUSTICE"),
            Re2 = new Regex("FAMILY DIVISION"),
            Court = Courts.EWHC_Family
        },
        new Combo2 {    // EWHC/Admin/2008/2214
            Re1 = new Regex("IN THE HIGH COURT OF JUSTICE"),
            Re2 = new Regex("ADMINISTRATIVE DIVISION"),
            Court = Courts.EWHC_QBD_Administrative
        },
        new Combo2 {    // EWHC/Comm/2009/2472
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$"),
            Re2 = new Regex(@"^COMMERCIAL COURT$"),
            Court = Courts.EWHC_QBD_Commercial
        },
        new Combo2 {    //EWHC/TCC/2012/780
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$"),
            Re2 = new Regex(@"^QUEEN'S BENCH DIVISION TECHNOLOGY AND CONSTRUCTION COURT$"),
            Court = Courts.EWHC_QBD_TCC
        },
        new Combo2 {    // EWHC/Admin/2003/2846, EWHC/Admin/2006/1645, EWHC/Admin/2009/995
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^DIVISIONAL( COURT)?$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD // this is risky
        },
        new Combo2 {
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^SENIOR COURTS COSTS OFFICE$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_SeniorCourtsCosts
        },
        // new Combo2 {    // EWHC/QB/2010/389
        //     Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
        //     Re2 = new Regex("^(IN THE )?QUEEN[’']?S BENCH DIVISION$", RegexOptions.IgnoreCase), // no appostrophe in EWHC/QB/2004/447
        //     Court = Courts.EWHC_QBD
        // }
    };

    internal bool Match(IBlock one, IBlock two) {
        return Match(Re1, one) && Match(Re2, two);
    }
    internal List<WLine> Transform(IBlock one, IBlock two) {
        return new List<WLine>(2) { Transform1(one), Transform1(two) };
    }

    internal bool MatchFirstRun(IBlock one, IBlock two) {
        return MatchFirstRun(Re1, one) && Match(Re2, two);
    }
    internal List<WLine> TransformFirstRun(IBlock one, IBlock two) {
        return new List<WLine>(3) { TransformFirstRun(one), Transform1(two) };
    }

    internal bool MatchTwoFirstRuns(IBlock one, IBlock two) {
        return MatchFirstRun(Re1, one) && MatchFirstRun(Re2, two);
    }
    internal List<WLine> TransformTwoFirstRuns(IBlock one, IBlock two) {
        return new List<WLine>(3) { TransformFirstRun(one), TransformFirstRun(two) };
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two) {
        foreach (Combo2 combo in Combo2.combos)
            if (combo.Match(one, two))
                return combo.Transform(one, two);
        foreach (Combo2 combo in Combo2.combos)
            if (MatchFirstRun(combo.Re1, one) && MatchFirstRun(combo.Re2, two))
                return new List<WLine>(2) { combo.TransformFirstRun(one), combo.TransformFirstRun(two) };
        return null;
    }

    internal bool Match1(IBlock block) {
        return MatchFirstRun(Re1, block) && MatchThirdRun(Re2, block);
    }
    internal static List<WLine> MatchAny1(IBlock block) {
        foreach (Combo2 combo in Combo2.combos)
            if (combo.Match1(block))
                return new List<WLine>(1) { combo.TransformFirstThreeRuns(block) };
        return null;
    }

    override protected Combo2 ConvertQueensToKings() {
        if (!this.Court.Code.Contains("-QBD"))
            return null;
        return new Combo2 {
            Re1 = this.Re1,
            Re2 = ConvertQueensToKings(this.Re2),
            Court = ConvertQueensToKings(this.Court)
        };
    }
    static Combo2() {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = combos.Concat(kings).ToArray();
    }

}

class Combo1_1 : Combo {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }

    internal static Combo1_1[] combos = new Combo1_1[] {
        new Combo1_1 {    // EWHC/Admin/2014/3257
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$"),
            Re2 = new Regex(@"^Royal Courts of Justice"),
            Court = Courts.EWHC
        }
    };

    internal bool Match(IBlock one, IBlock two) {
        return Match(Re1, one) && Match(Re2, two);
    }

    internal List<WLine> Transform(IBlock one, IBlock two) {
        return new List<WLine>(2) { Transform1(one), (WLine) two };
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two) {
        foreach (Combo1_1 combo in Combo1_1.combos)
            if (combo.Match(one, two))
                return combo.Transform(one, two);
        return null;
    }

    override protected Combo1_1 ConvertQueensToKings() {
        if (this.Court.Code.Contains("-QBD"))
            throw new Exception();
        return null;
    }
    // static Combo1_1() {
    //     var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
    //     combos = combos.Concat(kings).ToArray();
    // }

}

class Combo1 : Combo {

    public Regex Re { get; init; }

    internal static Combo1[] combos = new Combo1[] {
        new Combo1 {
            Re = new Regex("^IN THE (COURT OF APPEAL \\(CRIMINAL DIVISION\\)) *$", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Crim
        },
        new Combo1 {
            Re = new Regex("^(COURT OF APPEAL \\(CRIMINAL DIVISION\\)) *$", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Crim
        },
        new Combo1 {
            Re = new Regex("^IN THE (COURT OF APPEAL \\(CIVIL DIVISION ?\\)) *$", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Civil
        },
        new Combo1 {
            Re = new Regex("^(COURT OF APPEAL \\(CIVIL DIVISION\\)) *$", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Civil
        },
        new Combo1 {
            Re = new Regex(@"^IN THE HIGHCOURT OF APPEAL \(CIVIL DIVISION\)$", RegexOptions.IgnoreCase),    // EWCA/Civ/2010/393
            Court = Courts.CoA_Civil
        },
        new Combo1 {
            Re = new Regex("^IN THE (COURT OF PROTECTION) *$", RegexOptions.IgnoreCase),
            Court = Courts.EWCOP
        },
        new Combo1 {
            Re = new Regex("^(COURT OF PROTECTION) *$", RegexOptions.IgnoreCase),
            Court = Courts.EWCOP
        },
        new Combo1 {
            Re = new Regex("^IN (THE FAMILY COURT) *$", RegexOptions.IgnoreCase),
            Court = Courts.EWFC
        },
        new Combo1 {
            Re = new Regex("^IN THE CENTRAL FAMILY COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWFC
        },
        new Combo1 {
            Re = new Regex("^IN THE FAMILY COURT AT [A-Z-]+$", RegexOptions.IgnoreCase),
            Court = Courts.EWFC
        },
        new Combo1 {
            Re = new Regex("^(THE FAMILY COURT) SITTING AT [A-Z-]+ *$", RegexOptions.IgnoreCase),
            Court = Courts.EWFC
        },
        new Combo1 {
            Re = new Regex("^IN (THE FAMILY COURT) SITTING AT [A-Z-]+ *$", RegexOptions.IgnoreCase),
            Court = Courts.EWFC
        },
        new Combo1 {
            Re = new Regex("^IN THE EAST LONDON FAMILY COURT$"),
            Court = Courts.EWFC
        },
        new Combo1 {
            Re = new Regex(@"IN THE COURTS MARTIAL APPEAL COURT"),
            Court = Courts.CoA_Crim // ???
        },
        new Combo1 {
            Re = new Regex(@"IN THE SUPREME COURT COSTS OFFICE$"),
            Court = Courts.EWHC_SeniorCourtsCosts   // ???
        },
        new Combo1 {    // EWHC/Ch/2013/2818
            Re = new Regex(@"^IN THE HIGH COURT OF JUSTICE CHANCERY DIVISION COMPANIES COURT$"),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        },
        new Combo1 {    // [2021] EWHC 3411 (Fam)
            Re = new Regex(@"^IN THE HIGH COURT OF JUSTICE FAMILY DIVISION$"),
            Court = Courts.EWHC_Family
        },
        new() {
            Re = new Regex("^IN THE COUNTY COURT AT [A-Z-]+$"),
            Court = Courts.EWCC
        },
        new() {
            Re = new Regex("^IN THE COUNTY COURT AT [A-Z]+ [A-Z]+$"),
            Court = Courts.EWCC
        },
        new() {
            Re = new Regex("^(IN THE )?CROWN COURT AT [A-Z-]+$"),
            Court = Courts.EWCR
        },
        new() {
            Re = new Regex("^[A-Z]+ CROWN COURT$"),
            Court = Courts.EWCR
        },
        new Combo1 {
            Re = new Regex(@"^EMPLOYMENT APPEAL TRIBUNAL$"),
            Court = Courts.EmploymentAppealTribunal
        },
        new() {
            Re = new Regex(@"^IN THE INVESTIGATORY POWERS TRIBUNAL$"),
            Court = Courts.InvestigatoryPowersTribunal
        }
    };

    internal bool Match(IBlock one) {
        return Match(Re, one);
    }

    internal List<WLine> Transform(IBlock one) {
        return new List<WLine>(1) { Transform1(one) };
    }

    internal static List<WLine> MatchAny(IBlock one) {
        foreach (Combo1 combo in Combo1.combos)
            if (combo.Match(one))
                return combo.Transform(one);
        return null;
    }

    override protected Combo1 ConvertQueensToKings() {
        if (this.Court.Code.Contains("-QBD"))
            throw new Exception();
        return null;
    }
    // static Combo1() {
    //     var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
    //     combos = combos.Concat(kings).ToArray();
    // }

}

class Combo1bis {

    private Combo2 Two { get; init; }

    internal static IEnumerable<Combo1bis> combos = Combo2.combos.Select(two => new Combo1bis() { Two = two });

    internal bool Match(IBlock one) {
        if (one is not WLine line)
            return false;
        if (line.Contents.Count() != 3)
            return false;
        if (line.Contents.ElementAt(0) is not WText text1)
            return false;
        if (line.Contents.ElementAt(1) is not WLineBreak)
            return false;
        if (line.Contents.ElementAt(2) is not WText text2)
            return false;
        if (!Two.Re1.IsMatch(text1.Text.Trim()))
            return false;
        if (!Two.Re2.IsMatch(text2.Text.Trim()))
            return false;
        return true;
    }

    internal List<WLine> Transform(IBlock one) {
        WLine line = (WLine) one;
        WText text1 = (WText) line.Contents.ElementAt(0);
        WLineBreak lineBreak = (WLineBreak) line.Contents.ElementAt(1);
        WText text2 = (WText) line.Contents.ElementAt(2);
        IEnumerable<IInline> contents = new List<IInline>(3) {
            new WCourtType(text1.Text, text1.properties) { Code = Two.Court.Code },
            lineBreak,
            new WCourtType(text2.Text, text2.properties) { Code = Two.Court.Code }
        };
        return new List<WLine>(1) {
            WLine.Make(line, contents)
        };
    }

    internal static List<WLine> MatchAny(IBlock one) {
        foreach (Combo1bis combo in Combo1bis.combos)
            if (combo.Match(one))
                return combo.Transform(one);
        return null;
    }

}

class CourtType : Enricher2 {

    private List<WLine> Match6(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five, IBlock six) {
        return Combo6.MatchAny(one, two, three, four, five, six);
    }

    private List<WLine> Match5(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five) {
        return Combo5.MatchAny(one, two, three, four, five);
    }

    virtual protected List<WLine> Match4(IBlock one, IBlock two, IBlock three, IBlock four) {
        return Combo4.MatchAny(one, two, three, four) ?? Combo3_1.MatchAny(one, two, three, four);
    }

    private List<WLine> Match3(IBlock one, IBlock two, IBlock three) {
        return Combo3.MatchAny(one, two, three) ?? Combo2_1.MatchAny(one, two, three) ?? Combo1_2.MatchAny(one, two, three);
    }

    protected virtual List<WLine> Match2(IBlock one, IBlock two) {
        return Combo2.MatchAny(one, two) ?? Combo1_1.MatchAny(one, two) ?? Combo3.MatchAny2(one, two);
    }

    protected virtual List<WLine> Match1(IBlock block) {
        return Combo1.MatchAny(block) ?? Combo1bis.MatchAny(block) ?? Combo2.MatchAny1(block);
    }

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        const int limit = 10;
        int i = 0;
        while (i < blocks.Count() && i < limit) {
            IBlock block1 = blocks.ElementAt(i);
            if (i < blocks.Count() - 5) {
                IBlock block2 = blocks.ElementAt(i + 1);
                IBlock block3 = blocks.ElementAt(i + 2);
                IBlock block4 = blocks.ElementAt(i + 3);
                IBlock block5 = blocks.ElementAt(i + 4);
                IBlock block6 = blocks.ElementAt(i + 5);
                List<WLine> six = Match6(block1, block2, block3, block4, block5, block6);
                if (six is not null) {
                    IEnumerable<IBlock> before = blocks.Take(i);
                    IEnumerable<IBlock> after = blocks.Skip(i + 6);
                    return Enumerable.Concat(Enumerable.Concat(before, six), after);
                }
            }
            if (i < blocks.Count() - 4) {
                IBlock block2 = blocks.ElementAt(i + 1);
                IBlock block3 = blocks.ElementAt(i + 2);
                IBlock block4 = blocks.ElementAt(i + 3);
                IBlock block5 = blocks.ElementAt(i + 4);
                List<WLine> five = Match5(block1, block2, block3, block4, block5);
                if (five is not null) {
                    IEnumerable<IBlock> before = blocks.Take(i);
                    IEnumerable<IBlock> after = blocks.Skip(i + 5);
                    return Enumerable.Concat(Enumerable.Concat(before, five), after);
                }
            }
            if (i < blocks.Count() - 3) {
                IBlock block2 = blocks.ElementAt(i + 1);
                IBlock block3 = blocks.ElementAt(i + 2);
                IBlock block4 = blocks.ElementAt(i + 3);
                List<WLine> four = Match4(block1, block2, block3, block4);
                if (four is not null) {
                    IEnumerable<IBlock> before = blocks.Take(i);
                    IEnumerable<IBlock> after = blocks.Skip(i + 4);
                    return Enumerable.Concat(Enumerable.Concat(before, four), after);
                }
            }
            if (i < blocks.Count() - 2) {
                IBlock block2 = blocks.ElementAt(i + 1);
                IBlock block3 = blocks.ElementAt(i + 2);
                List<WLine> three = Match3(block1, block2, block3);
                if (three is not null) {
                    IEnumerable<IBlock> before = blocks.Take(i);
                    IEnumerable<IBlock> after = blocks.Skip(i + 3);
                    return Enumerable.Concat(Enumerable.Concat(before, three), after);
                }
            }
            if (i < blocks.Count() - 1) {
                IBlock block2 = blocks.ElementAt(i + 1);
                List<WLine> two = Match2(block1, block2);
                if (two is not null) {
                    IEnumerable<IBlock> before = blocks.Take(i);
                    IEnumerable<IBlock> after = blocks.Skip(i + 2);
                    return Enumerable.Concat(Enumerable.Concat(before, two), after);
                }
            }
            List<WLine> one = Match1(block1);
            if (one is not null) {
                IEnumerable<IBlock> before = blocks.Take(i);
                IEnumerable<IBlock> after = blocks.Skip(i + 1);
                return Enumerable.Concat(Enumerable.Concat(before, one), after);
            }
            if (block1 is WTable table) {
                WTable enriched = EnrichTable(table);
                if (!object.ReferenceEquals(table, enriched)) {
                    IEnumerable<IBlock> before = blocks.Take(i);
                    IEnumerable<IBlock> after = blocks.Skip(i + 1);
                return Enumerable.Concat(before.Append(enriched), after);
                }
            }
            i += 1;
        }
        return CourtType2.Enrich(blocks);
    }

    protected override WCell EnrichCell(WCell cell) {
        IEnumerable<IBlock> contents = Enrich(cell.Contents);
        if (object.ReferenceEquals(contents, cell.Contents))
            return cell;
        return new WCell(cell.Row, cell.Props, contents);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new NotImplementedException();
    }

}

}
