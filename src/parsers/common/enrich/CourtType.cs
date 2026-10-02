using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse;

internal static partial class CourtTypeHeaderRegexes
{
    [GeneratedRegex(@"^IN\sTHE\sHIGH\sCOURTS?\sOF\sJUSTICE$", RegexOptions.IgnoreCase, "en-GB")]
    public static partial Regex InTheHighCourtOfJustice();

    [GeneratedRegex(@"^(IN\s)?(THE\s)?BUSINESS\s(AND|&)\sPROPERTY\sCOURTS?$", RegexOptions.IgnoreCase, "en-GB")]
    public static partial Regex BusinessAndPropertyCourts();
    [GeneratedRegex(@"^(IN\s)?(THE\s)?BUSINESS\s(AND|&)\sPROPERTY?\sCOURTS?\sOF\sENGLAND\s(AND?|&)\sWALES$", RegexOptions.IgnoreCase, "en-GB")]
    public static partial Regex BusinessAndPropertyCourtsOfEnglandAndWales();

    [GeneratedRegex(@"^OF\sENGLAND\s(AND|&)\sWALES$", RegexOptions.IgnoreCase, "en-GB")]
    public static partial Regex OfEnglandAndWales();

    [GeneratedRegex(@"^QUEEN['’]?S\sBENCH\sDIVISION$", RegexOptions.IgnoreCase, "en-GB")]
    public static partial Regex QueensBenchDivision();
    [GeneratedRegex(@"^COMMERCIAL\sCOURT$", RegexOptions.IgnoreCase, "en-GB")]
    public static partial Regex CommercialCourt();
}

internal abstract class Combo
{
    protected bool Match(Regex regex, IBlock block)
    {
        if (block is not WLine line)
        {
            return false;
        }

        if (!line.Contents.Any())
        {
            return false;
        }

        var first = line.Contents.First();
        if (first is IImageRef)
        {
            // EWHC/Comm/2009/2472
            if (!line.Contents.Skip(1).All(inline => inline is WText))
            {
                return false;
            }
        }
        else if (first is ILineBreak)
        {
            if (!line.Contents.Skip(1).All(inline => inline is WText))
            {
                return false;
            }
        }
        else
        {
            if (!line.Contents.All(inline => inline is WText))
            {
                return false;
            }
        }

        var text = line.NormalizedContent;
        return regex.IsMatch(text);
    }

    protected static bool MatchFirstRun(Regex regex, IBlock block)
    {
        if (!(block is WLine line))
        {
            return false;
        }

        if (line.Contents.Count() == 0)
        {
            return false;
        }

        var first = line.Contents.First();
        if (first is WText wText)
        {
            var text = Regex.Replace(wText.Text, @"\s+", " ").Trim();
            return regex.IsMatch(text);
        }

        if (first is WImageRef)
        {
            var second = line.Contents.Skip(1).FirstOrDefault();
            if (second is null)
            {
                return false;
            }

            if (second is not WText wText2)
            {
                return false;
            }

            var text = Regex.Replace(wText2.Text, @"\s+", " ").Trim();
            return regex.IsMatch(text);
        }

        return false;
    }

    protected bool MatchThirdRun(Regex regex, IBlock block)
    {
        if (!(block is WLine line))
        {
            return false;
        }

        if (line.Contents.Count() < 3)
        {
            return false;
        }

        var second = line.Contents.ElementAt(1);
        var third = line.Contents.ElementAt(2);
        if (second is not WLineBreak)
        {
            return false;
        }

        if (third is not WText wText)
        {
            return false;
        }

        var text = Regex.Replace(wText.Text, @"\s+", " ").Trim();
        return regex.IsMatch(text);
    }

    protected bool MatchFirstAndThirdRuns(IBlock block, Regex re1, Regex re2)
    {
        if (!(block is WLine line))
        {
            return false;
        }

        if (line.Contents.Count() < 3)
        {
            return false;
        }

        var first = line.Contents.ElementAt(0);
        var second = line.Contents.ElementAt(1);
        var third = line.Contents.ElementAt(2);
        if (first is not WText wText1)
        {
            return false;
        }

        if (second is not WLineBreak)
        {
            return false;
        }

        if (third is not WText wText2)
        {
            return false;
        }

        var text1 = Regex.Replace(wText1.Text, @"\s+", " ").Trim();
        if (!re1.IsMatch(text1))
        {
            return false;
        }

        var text2 = Regex.Replace(wText2.Text, @"\s+", " ").Trim();
        if (!re2.IsMatch(text2))
        {
            return false;
        }

        return true;
    }

    protected WLine TransformFirstAndThirdRuns(IBlock block)
    {
        var line = (WLine)block;
        var first = (WText)line.Contents.ElementAt(0);
        var second = (WLineBreak)line.Contents.ElementAt(1);
        var third = (WText)line.Contents.ElementAt(2);
        var ct1 = new WCourtType(first.Text, first.properties) { Code = Court.Code };
        var ct3 = new WCourtType(third.Text, third.properties) { Code = Court.Code };
        var contents = line.Contents.Skip(3).Prepend(ct3).Prepend(second).Prepend(ct1);
        return WLine.Make(line, contents);
    }

    public Court Court { get; init; }

    protected WLine Transform1(IBlock block)
    {
        var line = (WLine)block;
        if (line.Contents.Count() == 1)
        {
            var text = (WText)line.Contents.First();
            var ct = new WCourtType(text.Text, text.properties) { Code = Court.Code };
            return WLine.Make(line, new List<IInline>(1) { ct });
        }

        if (line.Contents.First() is IImageRef)
        {
            var ct = new WCourtType2 { Code = Court.Code, Contents = line.Contents.Skip(1).Cast<WText>() };
            return WLine.Make(line, new List<IInline>(2) { line.Contents.First(), ct });
        }

        if (line.Contents.First() is ILineBreak)
        {
            var ct = new WCourtType2 { Code = Court.Code, Contents = line.Contents.Skip(1).Cast<WText>() };
            return WLine.Make(line, new List<IInline>(2) { line.Contents.First(), ct });
        }
        else
        {
            var ct = new WCourtType2 { Code = Court.Code, Contents = line.Contents.Cast<WText>() };
            return WLine.Make(line, new List<IInline>(1) { ct });
        }
    }

    protected WLine TransformFirstRun(IBlock block)
    {
        var line = (WLine)block;
        var first = line.Contents.First();
        if (first is WImageRef)
        {
            var wText2 = (WText)line.Contents.Skip(1).First();
            var ct = new WCourtType(wText2.Text, wText2.properties) { Code = Court.Code };
            var contents = line.Contents.Skip(2).Prepend(ct).Prepend(first);
            return WLine.Make(line, contents);
        }
        else
        {
            var wText1 = (WText)first;
            var ct = new WCourtType(wText1.Text, wText1.properties) { Code = Court.Code };
            var contents = line.Contents.Skip(1).Prepend(ct);
            return WLine.Make(line, contents);
        }
    }

    protected WLine TransformFirstThreeRuns(IBlock block)
    {
        var line = (WLine)block;
        var text = line.Contents.Take(3);
        var ct = new WCourtType2 { Code = Court.Code, Contents = text };
        var contents = line.Contents.Skip(3).Prepend(ct);
        return WLine.Make(line, contents);
    }

    protected static Regex ConvertQueensToKings(Regex re)
    {
        return new Regex(re.ToString().Replace("Queen", "King").Replace("QUEEN", "KING").Replace("QBD", "KBD"),
            re.Options);
    }

    protected static Court ConvertQueensToKings(Court court)
    {
        var code = court.Code.Replace("-QBD", "-KBD");
        return Courts.GetByCode(code);
    }

    protected abstract Combo ConvertQueensToKings();
}

internal class Combo6 : Combo
{
    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }
    public Regex Re4 { get; init; }
    public Regex Re5 { get; init; }
    public Regex Re6 { get; init; }

    internal static Combo6[] combos =
    [
        new()
        {
            // [2022] EWHC 219 (Comm)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourts(),
            Re3 = CourtTypeHeaderRegexes.OfEnglandAndWales(),
            Re4 = CourtTypeHeaderRegexes.CommercialCourt(),
            Re5 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re6 = new Regex("^FINANCIAL LIST$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial_Financial
        }
    ];

    internal bool Match(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five, IBlock six)
    {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three) && Match(Re4, four) && Match(Re5, five)
            && Match(Re6, six);
    }

    internal List<WLine> Transform(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five, IBlock six)
    {
        return
        [
            Transform1(one),
            Transform1(two),
            Transform1(three),
            Transform1(four),
            Transform1(five),
            Transform1(six)
        ];
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five, IBlock six)
    {
        foreach (var combo in combos)
        {
            if (combo.Match(one, two, three, four, five, six))
            {
                return combo.Transform(one, two, three, four, five, six);
            }
        }

        return null;
    }

    protected override Combo6 ConvertQueensToKings()
    {
        if (!Court.Code.Contains("-QBD"))
        {
            throw new Exception();
        }

        return new Combo6
        {
            Re1 = Re1,
            Re2 = Re2,
            Re3 = Re3,
            Re4 = Re4,
            Re5 = ConvertQueensToKings(Re5),
            Re6 = Re6,
            Court = ConvertQueensToKings(Court)
        };
    }

    static Combo6()
    {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = [.. combos, .. kings];
    }
}

internal class Combo5 : Combo
{
    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }
    public Regex Re4 { get; init; }
    public Regex Re5 { get; init; }

    internal static Combo5[] combos =
    [
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourts(),
            Re3 = CourtTypeHeaderRegexes.OfEnglandAndWales(),
            Re4 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re5 = CourtTypeHeaderRegexes.CommercialCourt(),
            Court = Courts.EWHC_QBD_Commercial
        }
    ];

    internal bool Match(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five)
    {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three) && Match(Re4, four) && Match(Re5, five);
    }

    internal List<WLine> Transform(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five)
    {
        return
        [
            Transform1(one),
            Transform1(two),
            Transform1(three),
            Transform1(four),
            Transform1(five)
        ];
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five)
    {
        foreach (var combo in combos)
        {
            if (combo.Match(one, two, three, four, five))
            {
                return combo.Transform(one, two, three, four, five);
            }
        }

        return null;
    }

    protected override Combo5 ConvertQueensToKings()
    {
        if (!Court.Code.Contains("-QBD"))
        {
            throw new Exception();
        }

        return new Combo5
        {
            Re1 = Re1,
            Re2 = Re2,
            Re3 = Re3,
            Re4 = ConvertQueensToKings(Re4),
            Re5 = Re5,
            Court = ConvertQueensToKings(Court)
        };
    }

    static Combo5()
    {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = [.. combos, .. kings];
    }
}

internal class Combo4 : Combo
{
    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }
    public Regex Re4 { get; init; }

    internal static Combo4[] combos =
    [
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^ADMINISTRATIVE COURT", RegexOptions.IgnoreCase),
            Re4 = new Regex("^PLANNING COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Planning
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^LEEDS DISTRICT REGISTRY$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^PLANNING COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Planning
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re4 = CourtTypeHeaderRegexes.CommercialCourt(),
            Court = Courts.EWHC_QBD_Commercial
        },
        new()
        {
            // EWHC/TCC/2018/2802
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = CourtTypeHeaderRegexes.BusinessAndPropertyCourts(),
            Re4 = new Regex("^TECHNOLOGY AND CONSTRUCTION COURT", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^[A-Z]+ DISTRICT REGISTRY$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^TECHNOLOGY AND CONSTRUCTION COURT", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new()
        {
            // EWHC/Ch/2014/1553
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^INTELLECTUAL PROPERTY and$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^COMMUNITY TRADE MARK COURT", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IPEC
        },
        new()
        {
            // [2021] EWHC 3295 (Pat)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^INTELLECTUAL PROPERTY LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^PATENTS COURT", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Patents
        },
        new()
        {
            // [2021] EWHC 3296 (IPEC)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^INTELLECTUAL PROPERTY LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^INTELLECTUAL PROPERTY ENTERPRISE COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IPEC
        },
        new()
        {
            // [2021] EWHC 2842 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourts(),
            Re3 = CourtTypeHeaderRegexes.OfEnglandAndWales(),
            Re4 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
        new()
        {
            // [2022] EWHC 34 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^CHANCERY APPEALS$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourts(),
            Re3 = CourtTypeHeaderRegexes.OfEnglandAndWales(),
            Re4 = new Regex(@"^CHANCERY APPEALS \(ChD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new()
        {
            // [2021] EWHC 2972 (TCC), [2021] EWHC 3595 (TCC)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourts(),
            Re3 = CourtTypeHeaderRegexes.OfEnglandAndWales(),
            Re4 = new Regex(@"^TECHNOLOGY (AND|&) CONSTRUCTION COURT \(QBD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re4 = CourtTypeHeaderRegexes.CommercialCourt(),
            Court = Courts.EWHC_QBD_Commercial
        },
        new()
        {
            // [2022] EWHC 245 (Comm)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.OfEnglandAndWales(),
            Re3 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re4 = CourtTypeHeaderRegexes.CommercialCourt(),
            Court = Courts.EWHC_QBD_Commercial
        },
        new()
        {
            // [2022] EWHC 544 (Comm), [2022] EWHC 586 (Comm)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourts(),
            Re3 = CourtTypeHeaderRegexes.OfEnglandAndWales(),
            Re4 = new Regex(@"^COMMERCIAL COURT \(QBD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial
        },
        new()
        {
            // [2021] EWHC 3432 (CH)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^BUSINESS LIST \(LONDON\)$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_BusinessList
        },
        new()
        {
            // [2021] EWHC 3514 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourts(),
            Re3 = CourtTypeHeaderRegexes.OfEnglandAndWales(),
            Re4 = new Regex(@"^BUSINESS LIST \(ChD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_BusinessList
        },
        new()
        {
            // [2021] EWHC 1988 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourts(),
            Re3 = CourtTypeHeaderRegexes.OfEnglandAndWales(),
            Re4 = new Regex("^INSOLVENCY AND COMPANIES COURT LIST$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        },
        new()
        {
            // [2022] EWHC (Ch) 1104
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^COMPANIES COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        }
    ];

    internal bool Match(IBlock one, IBlock two, IBlock three, IBlock four)
    {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three) && Match(Re4, four);
    }

    internal List<WLine> Transform(IBlock one, IBlock two, IBlock three, IBlock four)
    {
        return
        [
            Transform1(one),
            Transform1(two),
            Transform1(three),
            Transform1(four)
        ];
    }

    internal bool Match2(IBlock one, IBlock two, IBlock three, IBlock four)
    {
        return MatchFirstRun(Re1, one) && MatchFirstRun(Re2, two) && MatchFirstRun(Re3, three)
            && MatchFirstRun(Re4, four);
    }

    internal List<WLine> Transform2(IBlock one, IBlock two, IBlock three, IBlock four)
    {
        return
        [
            TransformFirstRun(one),
            TransformFirstRun(two),
            TransformFirstRun(three),
            TransformFirstRun(four)
        ];
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three, IBlock four)
    {
        foreach (var combo in combos)
        {
            if (combo.Match(one, two, three, four))
            {
                return combo.Transform(one, two, three, four);
            }
        }

        foreach (var combo in combos)
        {
            if (combo.Match2(one, two, three, four))
            {
                return combo.Transform2(one, two, three, four);
            }
        }

        return null;
    }

    protected override Combo4 ConvertQueensToKings()
    {
        if (!Court.Code.Contains("-QBD"))
        {
            return null;
        }

        return new Combo4
        {
            Re1 = Re1,
            Re2 = ConvertQueensToKings(Re2),
            Re3 = ConvertQueensToKings(Re3),
            Re4 = ConvertQueensToKings(Re4),
            Court = ConvertQueensToKings(Court)
        };
    }

    static Combo4()
    {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = [.. combos, .. kings];
    }
}

internal class Combo3_1 : Combo
{
    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }
    public Regex Re4 { get; init; }

    internal static Combo3_1[] combos =
    [
        new()
        {
            // [2021] EWHC 3347 (Ch), [2021] EWHC 3096 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^INTELLECTUAL PROPERTY LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),
            Re4 = new Regex("Rolls Buildings?$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IntellectualProperty
        },
        new()
        {
            // [2021] EWHC 3385 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^INTELLECTUAL PROPERTY LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^Royal Courts of Justice$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IntellectualProperty
        },
        new()
        {
            // [2021] EWHC 3502 (QB)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^ROYAL COURTS OF JUSTICE$", RegexOptions.IgnoreCase),
            Re3 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re4 = new Regex("^Neutral Citation Number", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new()
        {
            // [2022] EWHC 421 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^7 Rolls Buildings$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        }
    ];

    private bool Match(IBlock one, IBlock two, IBlock three, IBlock four)
    {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three) && Match(Re4, four);
    }

    private List<WLine> Transform(IBlock one, IBlock two, IBlock three, IBlock four)
    {
        return
        [
            Transform1(one),
            Transform1(two),
            Transform1(three),
            (WLine)four
        ];
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three, IBlock four)
    {
        foreach (var combo in combos)
        {
            if (combo.Match(one, two, three, four))
            {
                return combo.Transform(one, two, three, four);
            }
        }

        return null;
    }

    protected override Combo3_1 ConvertQueensToKings()
    {
        if (!Court.Code.Contains("-QBD"))
        {
            return null;
        }

        return new Combo3_1
        {
            Re1 = Re1,
            Re2 = Re2,
            Re3 = ConvertQueensToKings(Re3),
            Re4 = ConvertQueensToKings(Re4),
            Court = ConvertQueensToKings(Court)
        };
    }

    static Combo3_1()
    {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = [.. combos, .. kings];
    }
}

internal class Combo3 : Combo
{
    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }

    internal static Combo3[] combos =
    [
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 =
                new Regex("^(THE )?ADMINISTRATIVE COURT",
                    RegexOptions.IgnoreCase), // "THE" in EWHC/Admin/2006/1205, "... AT ..." in EWHC/Admin/2013/733
            Court = Courts.EWHC_QBD_Administrative
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^ADMINSTRATIVE COURT", RegexOptions.IgnoreCase), // spelling mistake in EWHC/Admin/2021/578
            Court = Courts.EWHC_QBD_Administrative
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^ADMIRALTY COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Admiralty
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 =
                new Regex("^PLANNING COURT",
                    RegexOptions.IgnoreCase), // can be followed by city name EWHC/Admin/2018/1753
            Court = Courts.EWHC_QBD_Planning
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^LONDON CIRCUIT COMMERCIAL COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial_Circuit
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^LONDON CIRCUIT COMMERCIAL COURT \(QBD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial_Circuit
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^TECHNOLOGY AND CONSTRUCTION COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^BIRMINGHAM DISTRICT REGISTRY$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new()
        {
            // [2022] EWHC 157 (Comm)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Court = Courts.EWHC_QBD_BusinessAndProperty
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION", RegexOptions.IgnoreCase),
            Re3 = new Regex("^PATENTS COURT", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Patents
        },
        new()
        {
            // EWHC/Patents/2005/1403
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISON$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^\(PATENTS COURT\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Patents
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION", RegexOptions.IgnoreCase),
            Re3 = new Regex("^INTELLECTUAL PROPERTY( ENTERPRISE COURT)?$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IPEC
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex("^INTELLECTUAL PROPERTY ENTERPRISE COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IPEC
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^INTELLECTUAL PROPERTY LIST \(ChD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IntellectualProperty
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 =
                new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES \\(ChD\\)", RegexOptions.IgnoreCase),
            Re3 = new Regex("^BUSINESS LIST", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_BusinessList
        },
        new()
        {
            // [2022] EWHC 48 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^BUSINESS LIST \(Ch ?D\)$", RegexOptions.IgnoreCase), // space in [2023] EWHC 1391 (Ch)
            Court = Courts.EWHC_Chancery_BusinessList
        },
        new()
        {
            // [2023] EWHC 1439 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS IN BIRMINGHAM$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^BUSINESS LIST$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_BusinessList
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION", RegexOptions.IgnoreCase),
            Re3 = new Regex("^COMPANIES COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        },
        new()
        {
            // [2021] EWHC 3199 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^COMPANIES COURT \(ChD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        },
        new()
        {
            // [2022] EWHC 24 (Ch), [2022] EWHC 202 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS", RegexOptions.IgnoreCase), // ... IN LEEDS
            Re3 = new Regex(@"^INSOLVENCY AND COMPANIES LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex("^CHANCERY APPEALS \\(ChD\\)", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new()
        {
            // [2021] EWHC 3416 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex("^CHANCERY APPEALS$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^APPEALS \(CH D\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new()
        {
            // EWHC/Comm/2018/3326
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^COMMERCIAL COURT \(QBD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial
        },
        new()
        {
            // EWHC/Comm/2009/2941, EWHC/Comm/2004/2750
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = CourtTypeHeaderRegexes.CommercialCourt(),
            Court = Courts.EWHC_QBD_Commercial
        },
        new()
        {
            // EWHC/Admin/2004/1441
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^DIVISIONAL COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD // ??? should it be QBD-General?
        },
        new()
        {
            // EWHC/QB/2016/1174, EWHC/QB/2017/1748
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^LONDON MERCANTILE COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial_Circuit
        },
        new()
        {
            // [2021] EWHC 3054 (Comm)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^LONDON CIRCUIT COMMERCIAL COURT \(QBD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial_Circuit
        },
        new()
        {
            // EWHC/TCC/2018/751
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^TECHNOLOGY AND CONSTRUCTION COURT \(QBD?\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new()
        {
            // EWHC/TCC/2011/3070
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^TECHNOLOGY & CONSTRUCTION COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new()
        {
            // EWHC/Costs/2012/90218
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^SENIOR COURTS COSTS OFFICE$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_SeniorCourtsCosts
        },
        new()
        {
            // [2021] EWHC 2950 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^FINANCIAL LIST$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Financial
        },
        new()
        {
            // [2021] EWHC 3306 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^FINANCIAL LIST \(Ch ?D\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Financial
        },
        new()
        {
            // [2022] EHWC 950 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales(),
            Re3 = new Regex(@"^PROPERTY, TRUSTS (AND|&) PROBATE LIST \(ChD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_PropertyTrustsProbate
        },
        new()
        {
            // [2023] EWHC 654 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS IN LEEDS$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^PROPERTY, TRUSTS (AND|&) PROBATE LIST \(ChD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_PropertyTrustsProbate
        }
    ];

    internal virtual bool Match(IBlock one, IBlock two, IBlock three)
    {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three);
    }

    internal virtual List<WLine> Transform(IBlock one, IBlock two, IBlock three)
    {
        return
        [
            Transform1(one),
            Transform1(two),
            Transform1(three)
        ];
    }

    internal virtual bool MatchFirstRun(IBlock one, IBlock two, IBlock three)
    {
        return MatchFirstRun(Re1, one) && Match(Re2, two) && Match(Re3, three);
    }

    internal virtual List<WLine> TransformFirstRun(IBlock one, IBlock two, IBlock three)
    {
        return
        [
            TransformFirstRun(one),
            Transform1(two),
            Transform1(three)
        ];
    }

    internal virtual bool MatchTwoFirstRuns(IBlock one, IBlock two, IBlock three)
    {
        return MatchFirstRun(Re1, one) && MatchFirstRun(Re2, two) && Match(Re3, three);
    }

    internal virtual List<WLine> TransformTwoFirstRuns(IBlock one, IBlock two, IBlock three)
    {
        return
        [
            TransformFirstRun(one),
            TransformFirstRun(two),
            Transform1(three)
        ];
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three)
    {
        foreach (var combo in combos)
        {
            if (combo.Match(one, two, three))
            {
                return combo.Transform(one, two, three);
            }
        }

        foreach (var combo in combos)
        {
            if (combo.MatchFirstRun(one, two, three))
            {
                return combo.TransformFirstRun(one, two, three);
            }
        }

        return null;
    }

    internal static List<WLine> MatchAny2(IBlock one, IBlock two)
    {
        foreach (var combo in combos)
        {
            if (combo.Match(combo.Re1, one) && combo.MatchFirstAndThirdRuns(two, combo.Re2, combo.Re3))
            {
                return [combo.Transform1(one), combo.TransformFirstAndThirdRuns(two)];
            }
        }

        return null;
    }

    protected override Combo3 ConvertQueensToKings()
    {
        if (!Court.Code.Contains("-QBD"))
        {
            return null;
        }

        return new Combo3
        {
            Re1 = Re1,
            Re2 = ConvertQueensToKings(Re2),
            Re3 = ConvertQueensToKings(Re3),
            Court = ConvertQueensToKings(Court)
        };
    }

    static Combo3()
    {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = [.. combos, .. kings];
    }
}

internal class Combo2_1 : Combo
{
    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }

    internal static Combo2_1[] combos =
    [
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^(The )?Royal Courts of Justice", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^ON APPEAL FROM", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new()
        {
            // EWHC/QB/2011/3104
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^[A-Z]+ DISTRICT REGISTRY$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new()
        {
            // EWHC/QB/2013/2997
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^Strand$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new()
        {
            // EWHC/QB/2011/3068
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^IN THE MATTER OF"),
            Court = Courts.EWHC_QBD
        },
        new()
        {
            // EWHC/QB/2014/1972
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex("^The Combined Court Centre"),
            Court = Courts.EWHC_QBD
        },
        new()
        {
            // EWHC/Ch/2016/4063
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^Royal Courts of Justice", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
        new()
        {
            // EWHC/Ch/2015/274
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^[A-Z][a-z]+ Building, Royal Courts of Justice$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
        new()
        {
            // EWHC/Ch/2008/1893
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^Before", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
        new()
        {
            // EWHC/Ch/2013/200
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^Date", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
        new()
        {
            // EWHC/Ch/2013/160, EWHC/Ch/2012/616
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^[A-Z][a-z]+ Building"),
            Court = Courts.EWHC_Chancery
        },
        new()
        {
            // EWHC/Ch/2018/2783
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^\d+ [A-Z][a-z]+ Building"),
            Court = Courts.EWHC_Chancery
        },
        new()
        {
            // EWHC/Ch/2014/1048
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("Building$"),
            Court = Courts.EWHC_Chancery
        },
        new()
        {
            // EWHC/Ch/2016/1996, ewhc/ch/2011/3553
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex(@"^\(?CHANCERY DIVISION\)?$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^[A-Z]+ DISTRICT REGISTRY$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        },
        new()
        {
            // EWHC/Ch/2016/243
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^IN THE MATTER OF"),
            Court = Courts.EWHC_Chancery
        },
        new()
        {
            // EWHC/Ch/2018/106
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^Bristol Civil Justice Centre$"),
            Court = Courts.EWHC_Chancery
        },
        new()
        {
            // EWHC/Ch/2013/3098
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^IN AN APPEAL FROM", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new()
        {
            // EWHC/Ch/2017/541
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^ON APPEAL FROM", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new()
        {
            // EWHC/Ch/2003/2985
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^Appeal against the decision of", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new()
        {
            // [2021] EWHC 3418 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES \(ChD\)$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^ON APPEAL FROM", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new()
        {
            // [2021] EWHC 3247 (QB)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.QueensBenchDivision(),
            Re3 = new Regex(@"^\[\d{4}\] EWHC \d+ \([A-Z]+[a-z]*\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
        new()
        {
            // [2021] EWHC 3260 (Ch)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^\[\d{4}\] EWHC \d+ \(Ch\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery
        }
    ];

    private bool Matchish(Regex regex, IBlock block)
    {
        if (!(block is WLine line))
        {
            return false;
        }

        if (line.Contents.Count() == 0)
        {
            return false;
        }

        var text = line.NormalizedContent;
        return regex.IsMatch(text);
    }

    internal bool Match(IBlock one, IBlock two, IBlock three)
    {
        return Match(Re1, one) && Match(Re2, two) && Matchish(Re3, three);
    }

    private bool Match2(IBlock one, IBlock two, IBlock three)
    {
        return MatchFirstRun(Re1, one) && Match(Re2, two) && Matchish(Re3, three);
    }

    internal List<WLine> Transform(IBlock one, IBlock two, IBlock three)
    {
        return
        [
            Transform1(one),
            Transform1(two),
            (WLine)three
        ];
    }

    private List<WLine> Transform2(IBlock one, IBlock two, IBlock three)
    {
        return
        [
            TransformFirstRun(one),
            Transform1(two),
            (WLine)three
        ];
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three)
    {
        foreach (var combo in combos)
        {
            if (combo.Match(one, two, three))
            {
                return combo.Transform(one, two, three);
            }

            if (combo.Match2(one, two, three))
            {
                return combo.Transform2(one, two, three);
            }
        }

        return null;
    }

    protected override Combo2_1 ConvertQueensToKings()
    {
        if (!Court.Code.Contains("-QBD"))
        {
            return null;
        }

        return new Combo2_1
        {
            Re1 = Re1,
            Re2 = ConvertQueensToKings(Re2),
            Re3 = Re3,
            Court = ConvertQueensToKings(Court)
        };
    }

    static Combo2_1()
    {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = [.. combos, .. kings];
    }
}

internal class Combo1_2 : Combo
{
    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }

    internal static Combo1_2[] combos =
    [
        new()
        {
            Re1 = new Regex("^IN THE COURT OF APPEAL$", RegexOptions.IgnoreCase),
            Re2 =
                new Regex("^ON APPEAL FROM THE HIGH COURT OF JUSTICE$",
                    RegexOptions.IgnoreCase), // no apostrophe in EWHC/Admin/2009/573
            Re3 = new Regex("^CHANCERY DIVISION$", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Civil
        }
    ];

    internal bool Match(IBlock one, IBlock two, IBlock three)
    {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three);
    }

    internal List<WLine> Transform(IBlock one, IBlock two, IBlock three)
    {
        return
        [
            Transform1(one),
            (WLine)two,
            (WLine)three
        ];
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two, IBlock three)
    {
        foreach (var combo in combos)
        {
            if (combo.Match(one, two, three))
            {
                return combo.Transform(one, two, three);
            }
        }

        return null;
    }

    protected override Combo1_2 ConvertQueensToKings()
    {
        if (Court.Code.Contains("-QBD"))
        {
            throw new Exception();
        }

        return null;
    }
}

internal class Combo2 : Combo
{
    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }

    internal static Combo2[] combos =
    [
        new()
        {
            Re1 = new Regex("^IN THE HIGH COURT \\(DIVISIONAL\\) COURT &$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^COURT OF APPEAL \\(CIVIL DIVISION\\)", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Civil
        },
        new()
        {
            Re1 = new Regex("IN THE SUPREME COURT OF JUDICATURE"),
            Re2 = new Regex("COURT OF APPEAL \\(CIVIL DIVISION\\)"),
            Court = Courts.CoA_Civil
        },
        new()
        {
            Re1 = new Regex("IN THE SUPREME COURT OF JUDICATURE"),
            Re2 = new Regex("COURT OF APPEAL \\(CRIMINAL DIVISION\\)"),
            Court = Courts.CoA_Crim
        },
        new()
        {
            Re1 = new Regex("IN THE COURT OF APPEAL"),
            Re2 = new Regex("CRIMINAL +DIVISION"),
            Court = Courts.CoA_Crim
        },
        new()
        {
            // EWHC/Ch/2008/2029
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex(@"\(Chancery Division\)"),
            Court = Courts.EWHC_Chancery
        },
        new()
        {
            // [2023] EWHC 1593 (KB)
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("KING['’]S BENCH DIVISION"),
            Court = Courts.EWHC_KBD
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("FAMILY DIVISION"),
            Court = Courts.EWHC_Family
        },
        new()
        {
            // EWHC/Admin/2008/2214
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("ADMINISTRATIVE DIVISION"),
            Court = Courts.EWHC_QBD_Administrative
        },
        new()
        {
            // EWHC/Comm/2009/2472
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = CourtTypeHeaderRegexes.CommercialCourt(),
            Court = Courts.EWHC_QBD_Commercial
        },
        new()
        {
            //EWHC/TCC/2012/780
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^QUEEN'S BENCH DIVISION TECHNOLOGY AND CONSTRUCTION COURT$"),
            Court = Courts.EWHC_QBD_TCC
        },
        new()
        {
            // EWHC/Admin/2003/2846, EWHC/Admin/2006/1645, EWHC/Admin/2009/995
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^DIVISIONAL( COURT)?$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD // this is risky
        },
        new()
        {
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^SENIOR COURTS COSTS OFFICE$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_SeniorCourtsCosts
        }
    ];

    internal bool Match(IBlock one, IBlock two)
    {
        return Match(Re1, one) && Match(Re2, two);
    }

    internal List<WLine> Transform(IBlock one, IBlock two)
    {
        return [Transform1(one), Transform1(two)];
    }

    internal bool MatchFirstRun(IBlock one, IBlock two)
    {
        return MatchFirstRun(Re1, one) && Match(Re2, two);
    }

    internal List<WLine> TransformFirstRun(IBlock one, IBlock two)
    {
        return new List<WLine>(3) { TransformFirstRun(one), Transform1(two) };
    }

    internal bool MatchTwoFirstRuns(IBlock one, IBlock two)
    {
        return MatchFirstRun(Re1, one) && MatchFirstRun(Re2, two);
    }

    internal List<WLine> TransformTwoFirstRuns(IBlock one, IBlock two)
    {
        return new List<WLine>(3) { TransformFirstRun(one), TransformFirstRun(two) };
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two)
    {
        foreach (var combo in combos)
        {
            if (combo.Match(one, two))
            {
                return combo.Transform(one, two);
            }
        }

        foreach (var combo in combos)
        {
            if (MatchFirstRun(combo.Re1, one) && MatchFirstRun(combo.Re2, two))
            {
                return [combo.TransformFirstRun(one), combo.TransformFirstRun(two)];
            }
        }

        return null;
    }

    internal bool Match1(IBlock block)
    {
        return MatchFirstRun(Re1, block) && MatchThirdRun(Re2, block);
    }

    internal static List<WLine> MatchAny1(IBlock block)
    {
        foreach (var combo in combos)
        {
            if (combo.Match1(block))
            {
                return [combo.TransformFirstThreeRuns(block)];
            }
        }

        return null;
    }

    protected override Combo2 ConvertQueensToKings()
    {
        if (!Court.Code.Contains("-QBD"))
        {
            return null;
        }

        return new Combo2
        {
            Re1 = Re1,
            Re2 = ConvertQueensToKings(Re2),
            Court = ConvertQueensToKings(Court)
        };
    }

    static Combo2()
    {
        var kings = combos.Select(c => c.ConvertQueensToKings()).Where(c => c is not null);
        combos = [.. combos, .. kings];
    }
}

internal class Combo1_1 : Combo
{
    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }

    internal static Combo1_1[] combos =
    [
        new()
        {
            // EWHC/Admin/2014/3257
            Re1 = CourtTypeHeaderRegexes.InTheHighCourtOfJustice(),
            Re2 = new Regex("^Royal Courts of Justice"),
            Court = Courts.EWHC
        }
    ];

    internal bool Match(IBlock one, IBlock two)
    {
        return Match(Re1, one) && Match(Re2, two);
    }

    internal List<WLine> Transform(IBlock one, IBlock two)
    {
        return [Transform1(one), (WLine)two];
    }

    internal static List<WLine> MatchAny(IBlock one, IBlock two)
    {
        foreach (var combo in combos)
        {
            if (combo.Match(one, two))
            {
                return combo.Transform(one, two);
            }
        }

        return null;
    }

    protected override Combo1_1 ConvertQueensToKings()
    {
        if (Court.Code.Contains("-QBD"))
        {
            throw new Exception();
        }

        return null;
    }
}

internal class Combo1 : Combo
{
    public Regex Re { get; init; }

    internal static Combo1[] combos =
    [
        new()
        {
            Re = new Regex("^IN THE (COURT OF APPEAL \\(CRIMINAL DIVISION\\)) *$", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Crim
        },
        new()
        {
            Re = new Regex("^(COURT OF APPEAL \\(CRIMINAL DIVISION\\)) *$", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Crim
        },
        new()
        {
            Re = new Regex("^IN THE (COURT OF APPEAL \\(CIVIL DIVISION ?\\)) *$", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Civil
        },
        new()
        {
            Re = new Regex("^(COURT OF APPEAL \\(CIVIL DIVISION\\)) *$", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Civil
        },
        new()
        {
            Re = new Regex(@"^IN THE HIGHCOURT OF APPEAL \(CIVIL DIVISION\)$",
                RegexOptions.IgnoreCase), // EWCA/Civ/2010/393
            Court = Courts.CoA_Civil
        },
        new()
        {
            Re = new Regex("^IN THE (COURT OF PROTECTION) *$", RegexOptions.IgnoreCase), Court = Courts.EWCOP
        },
        new() { Re = new Regex("^(COURT OF PROTECTION) *$", RegexOptions.IgnoreCase), Court = Courts.EWCOP },
        new() { Re = new Regex("^IN (THE FAMILY COURT) *$", RegexOptions.IgnoreCase), Court = Courts.EWFC },
        new() { Re = new Regex("^IN THE CENTRAL FAMILY COURT$", RegexOptions.IgnoreCase), Court = Courts.EWFC },
        new() { Re = new Regex("^IN THE FAMILY COURT AT [A-Z-]+$", RegexOptions.IgnoreCase), Court = Courts.EWFC },
        new()
        {
            Re = new Regex("^(THE FAMILY COURT) SITTING AT [A-Z-]+ *$", RegexOptions.IgnoreCase),
            Court = Courts.EWFC
        },
        new()
        {
            Re = new Regex("^IN (THE FAMILY COURT) SITTING AT [A-Z-]+ *$", RegexOptions.IgnoreCase),
            Court = Courts.EWFC
        },
        new() { Re = new Regex("^IN THE EAST LONDON FAMILY COURT$"), Court = Courts.EWFC },
        new() { Re = new Regex("IN THE COURTS MARTIAL APPEAL COURT"), Court = Courts.CoA_Crim },
        new() { Re = new Regex("IN THE SUPREME COURT COSTS OFFICE$"), Court = Courts.EWHC_SeniorCourtsCosts },
        new()
        {
            // EWHC/Ch/2013/2818
            Re = new Regex("^IN THE HIGH COURT OF JUSTICE CHANCERY DIVISION COMPANIES COURT$"),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        },
        new()
        {
            // [2021] EWHC 3411 (Fam)
            Re = new Regex("^IN THE HIGH COURT OF JUSTICE FAMILY DIVISION$"), Court = Courts.EWHC_Family
        },
        new() { Re = new Regex("^IN THE COUNTY COURT AT [A-Z-]+$"), Court = Courts.EWCC },
        new() { Re = new Regex("^IN THE COUNTY COURT AT [A-Z]+ [A-Z]+$"), Court = Courts.EWCC },
        new() { Re = new Regex("^(IN THE )?CROWN COURT AT [A-Z-]+$"), Court = Courts.EWCR },
        new() { Re = new Regex("^[A-Z]+ CROWN COURT$"), Court = Courts.EWCR },
        new() { Re = new Regex("^EMPLOYMENT APPEAL TRIBUNAL$"), Court = Courts.EmploymentAppealTribunal },
        new() { Re = new Regex("^IN THE INVESTIGATORY POWERS TRIBUNAL$"), Court = Courts.InvestigatoryPowersTribunal }
    ];

    internal bool Match(IBlock one)
    {
        return Match(Re, one);
    }

    internal List<WLine> Transform(IBlock one)
    {
        return [Transform1(one)];
    }

    internal static List<WLine> MatchAny(IBlock one)
    {
        foreach (var combo in combos)
        {
            if (combo.Match(one))
            {
                return combo.Transform(one);
            }
        }

        return null;
    }

    protected override Combo1 ConvertQueensToKings()
    {
        if (Court.Code.Contains("-QBD"))
        {
            throw new Exception();
        }

        return null;
    }
}

internal class Combo1bis
{
    private Combo2 Two { get; init; }

    internal static IEnumerable<Combo1bis> combos = Combo2.combos.Select(two => new Combo1bis { Two = two });

    internal bool Match(IBlock one)
    {
        return one is WLine line
            && line.Contents.ToArray() is [WText text1, WLineBreak, WText text2]
            && Two.Re1.IsMatch(text1.Text.Trim())
            && Two.Re2.IsMatch(text2.Text.Trim());
    }

    internal List<WLine> Transform(IBlock one)
    {
        var line = (WLine)one;
        var text1 = (WText)line.Contents.ElementAt(0);
        var lineBreak = (WLineBreak)line.Contents.ElementAt(1);
        var text2 = (WText)line.Contents.ElementAt(2);
        IEnumerable<IInline> contents = new List<IInline>(3)
        {
            new WCourtType(text1.Text, text1.properties) { Code = Two.Court.Code },
            lineBreak,
            new WCourtType(text2.Text, text2.properties) { Code = Two.Court.Code }
        };
        return [WLine.Make(line, contents)];
    }

    internal static List<WLine> MatchAny(IBlock one)
    {
        foreach (var combo in combos)
        {
            if (combo.Match(one))
            {
                return combo.Transform(one);
            }
        }

        return null;
    }
}

internal class CourtType : Enricher2
{
    private List<WLine> Match6(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five, IBlock six)
    {
        return Combo6.MatchAny(one, two, three, four, five, six);
    }

    private List<WLine> Match5(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five)
    {
        return Combo5.MatchAny(one, two, three, four, five);
    }

    protected virtual List<WLine> Match4(IBlock one, IBlock two, IBlock three, IBlock four)
    {
        return Combo4.MatchAny(one, two, three, four) ?? Combo3_1.MatchAny(one, two, three, four);
    }

    private List<WLine> Match3(IBlock one, IBlock two, IBlock three)
    {
        return Combo3.MatchAny(one, two, three)
            ?? Combo2_1.MatchAny(one, two, three) ?? Combo1_2.MatchAny(one, two, three);
    }

    protected virtual List<WLine> Match2(IBlock one, IBlock two)
    {
        return Combo2.MatchAny(one, two) ?? Combo1_1.MatchAny(one, two) ?? Combo3.MatchAny2(one, two);
    }

    protected virtual List<WLine> Match1(IBlock block)
    {
        return Combo1.MatchAny(block) ?? Combo1bis.MatchAny(block) ?? Combo2.MatchAny1(block);
    }

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        const int limit = 10;
        var i = 0;
        while (i < blocks.Count() && i < limit)
        {
            var block1 = blocks.ElementAt(i);
            if (i < blocks.Count() - 5)
            {
                var block2 = blocks.ElementAt(i + 1);
                var block3 = blocks.ElementAt(i + 2);
                var block4 = blocks.ElementAt(i + 3);
                var block5 = blocks.ElementAt(i + 4);
                var block6 = blocks.ElementAt(i + 5);
                var six = Match6(block1, block2, block3, block4, block5, block6);
                if (six is not null)
                {
                    var before = blocks.Take(i);
                    var after = blocks.Skip(i + 6);
                    return before.Concat(six).Concat(after);
                }
            }

            if (i < blocks.Count() - 4)
            {
                var block2 = blocks.ElementAt(i + 1);
                var block3 = blocks.ElementAt(i + 2);
                var block4 = blocks.ElementAt(i + 3);
                var block5 = blocks.ElementAt(i + 4);
                var five = Match5(block1, block2, block3, block4, block5);
                if (five is not null)
                {
                    var before = blocks.Take(i);
                    var after = blocks.Skip(i + 5);
                    return before.Concat(five).Concat(after);
                }
            }

            if (i < blocks.Count() - 3)
            {
                var block2 = blocks.ElementAt(i + 1);
                var block3 = blocks.ElementAt(i + 2);
                var block4 = blocks.ElementAt(i + 3);
                var four = Match4(block1, block2, block3, block4);
                if (four is not null)
                {
                    var before = blocks.Take(i);
                    var after = blocks.Skip(i + 4);
                    return before.Concat(four).Concat(after);
                }
            }

            if (i < blocks.Count() - 2)
            {
                var block2 = blocks.ElementAt(i + 1);
                var block3 = blocks.ElementAt(i + 2);
                var three = Match3(block1, block2, block3);
                if (three is not null)
                {
                    var before = blocks.Take(i);
                    var after = blocks.Skip(i + 3);
                    return before.Concat(three).Concat(after);
                }
            }

            if (i < blocks.Count() - 1)
            {
                var block2 = blocks.ElementAt(i + 1);
                var two = Match2(block1, block2);
                if (two is not null)
                {
                    var before = blocks.Take(i);
                    var after = blocks.Skip(i + 2);
                    return before.Concat(two).Concat(after);
                }
            }

            var one = Match1(block1);
            if (one is not null)
            {
                var before = blocks.Take(i);
                var after = blocks.Skip(i + 1);
                return before.Concat(one).Concat(after);
            }

            if (block1 is WTable table)
            {
                var enriched = EnrichTable(table);
                if (!ReferenceEquals(table, enriched))
                {
                    var before = blocks.Take(i);
                    var after = blocks.Skip(i + 1);
                    return before.Append(enriched).Concat(after);
                }
            }

            i += 1;
        }

        return CourtType2.Enrich(blocks);
    }

    protected override WCell EnrichCell(WCell cell)
    {
        var contents = Enrich(cell.Contents);
        if (ReferenceEquals(contents, cell.Contents))
        {
            return cell;
        }

        return new WCell(cell.Row, cell.Props, contents);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        throw new NotImplementedException();
    }
}
