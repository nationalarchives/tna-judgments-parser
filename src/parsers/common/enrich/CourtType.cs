
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse {

abstract class Combo {

    protected bool Match(Regex regex, IBlock block) {
        if (!(block is ILine line))
            return false;
        if (line.Contents.Count() == 0)
            return false;
        IInline first = line.Contents.First();
        if (first is IImageRef) {    // EWHC/Comm/2009/2472
            if (!line.Contents.Skip(1).Where(inline => inline is WText).Any() || !line.Contents.Skip(1).All(inline => inline is WText))
                return false;
        } else {
            if (!line.Contents.All(inline => inline is WText))
                return false;
        }
        string text = line.NormalizedContent();
        return regex.IsMatch(text);
    }

    public Court Court { get; init; }

    protected WLine Transform1(IBlock block) {
        WLine line = (WLine) block;
        if (line.Contents.Count() == 1) {
            WText text = (WText) line.Contents.First();
            WCourtType ct = new WCourtType(text.Text, text.properties) { Code = this.Court.Code };
            return new WLine(line, new List<IInline>(1) { ct });
        } else if (line.Contents.First() is IImageRef) {
            WCourtType2 ct = new WCourtType2() { Code = this.Court.Code, Contents = line.Contents.Skip(1).Cast<WText>() };
            return new WLine(line, new List<IInline>(2) { line.Contents.First(), ct });
        } else {
            WCourtType2 ct = new WCourtType2() { Code = this.Court.Code, Contents = line.Contents.Cast<WText>() };
            return new WLine(line, new List<IInline>(1) { ct });
        }
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
            Re4 = new Regex("^QUEEN'S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re5 = new Regex("^COMMERCIAL COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Commercial
        }
    };

    internal bool Match(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five) {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three) && Match(Re4, four) && Match(Re5, five);
    }

    internal List<ILine> Transform(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five) {
        return new List<ILine>(5) {
            Transform1(one),
            Transform1(two),
            Transform1(three),
            Transform1(four),
            Transform1(five)
        };

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
            Re3 = new Regex("^(THE )?ADMINISTRATIVE COURT$", RegexOptions.IgnoreCase),  // "THE" in EWHC/Admin/2006/1205
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
            Re3 = new Regex("^PLANNING COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Planning
        },
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']?S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^LONDON CIRCUIT COMMERCIAL COURT$", RegexOptions.IgnoreCase),
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
            Court = Courts.EWHC_QBD_General
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^CHANCERY DIVISION", RegexOptions.IgnoreCase),
            Re3 = new Regex("^PATENTS COURT", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Patents
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^CHANCERY DIVISION", RegexOptions.IgnoreCase),
            Re3 = new Regex("^INTELLECTUAL PROPERTY ENTERPRISE COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_IPEC
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES \\(ChD\\)", RegexOptions.IgnoreCase),
            Re3 = new Regex("^BUSINESS LIST", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_BusinessList
        },
        new Combo3 {
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^CHANCERY DIVISION", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^COMPANIES COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_InsolvencyAndCompanies
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES", RegexOptions.IgnoreCase),
            Re3 = new Regex("^CHANCERY APPEALS \\(ChD\\)", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new Combo3 {
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^APPEALS \(CH D\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Appeals
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND AND? WALES", RegexOptions.IgnoreCase),    // missing D in EWHC/QB/2017/2921
            Re3 = new Regex("^COMMERCIAL COURT$", RegexOptions.IgnoreCase),
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
        // new Combo3 {
        //     Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
        //     Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
        //     Re3 = new Regex(@"^TECHNOLOGY AND CONSTRUCTION COURT$", RegexOptions.IgnoreCase),
        //     Court = Courts.EWHC_QBD_TCC
        // },
        new Combo3 {    // EWHC/TCC/2018/751
            Re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^TECHNOLOGY AND CONSTRUCTION COURT \(QBD\)$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        }
    };

    internal bool Match(IBlock one, IBlock two, IBlock three) {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three);
    }

    internal List<ILine> Transform(IBlock one, IBlock two, IBlock three) {
        return new List<ILine>(3) {
            Transform1(one),
            Transform1(two),
            Transform1(three)
        };
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

    internal List<ILine> Transform(IBlock one, IBlock two, IBlock three) {
        return new List<ILine>(3) {
            Transform1(one), (ILine) two, (ILine) three
        };
    }

    internal static List<ILine> MatchAny(IBlock one, IBlock two, IBlock three) {
        foreach (Combo1_2 combo in Combo1_2.combos)
            if (combo.Match(one, two, three))
                return combo.Transform(one, two, three);
        return null;

    }


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
        // new Combo2 {    // EWHC/Admin/2003/2846, EWHC/Admin/2006/1645
        //     Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
        //     Re2 = new Regex("^DIVISIONAL( COURT)?$", RegexOptions.IgnoreCase),
        //     Court = Courts.EWHC_QBD // ???
        // },
        // new Combo2 {    // EWHC/QB/2010/389
        //     Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
        //     Re2 = new Regex("^(IN THE )?QUEEN[’']?S BENCH DIVISION$", RegexOptions.IgnoreCase), // no appostrophe in EWHC/QB/2004/447
        //     Court = Courts.EWHC_QBD
        // }
    };

    internal bool Match(IBlock one, IBlock two) {
        return Match(Re1, one) && Match(Re2, two);
    }

    internal List<ILine> Transform(IBlock one, IBlock two) {
        return new List<ILine>(2) {
            Transform1(one),
            Transform1(two)
        };
    }

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
            Re = new Regex("^(THE FAMILY COURT) SITTING AT [A-Z]+ *$", RegexOptions.IgnoreCase),
            Court = Courts.EWFC
        },
        new Combo1 {
            Re = new Regex("^IN (THE FAMILY COURT) SITTING AT [A-Z]+ *$", RegexOptions.IgnoreCase),
            Court = Courts.EWFC
        },
        new Combo1 {
            Re = new Regex("^IN THE EAST LONDON FAMILY COURT$"),
            Court = Courts.EWFC
        },
        new Combo1 {
            Re = new Regex(@"IN THE COURTS MARTIAL APPEAL COURT"),
            Court = Courts.CoA_Crim // ???
        }
    };

    internal bool Match(IBlock one) {
        return Match(Re, one);
    }

    internal List<ILine> Transform(IBlock one) {
        return new List<ILine>(1) {
            Transform1(one)
        };
    }

}


class CourtType : Enricher {

    private List<ILine> Match5(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five) {
        foreach (Combo5 combo in Combo5.combos)
            if (combo.Match(one, two, three, four, five))
                return combo.Transform(one, two, three, four, five);
        return null;
    }

    private List<ILine> Match3(IBlock one, IBlock two, IBlock three) {
        foreach (Combo3 combo in Combo3.combos)
            if (combo.Match(one, two, three))
                return combo.Transform(one, two, three);
        return null;
    }

    private List<ILine> Match2(IBlock one, IBlock two) {
        foreach (Combo2 combo in Combo2.combos)
            if (combo.Match(one, two))
                return combo.Transform(one, two);
        return null;
    }

    private List<ILine> Match1(IBlock block) {
        foreach (Combo1 combo in Combo1.combos)
            if (combo.Match(block))
                return combo.Transform(block);
        return null;
    }


    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> enriched = new List<IBlock>();
        int i = 0;
        while (i < blocks.Count()) {
            IBlock block1 = blocks.ElementAt(i);
            if (i < blocks.Count() - 4) {
                IBlock block2 = blocks.ElementAt(i + 1);
                IBlock block3 = blocks.ElementAt(i + 2);
                IBlock block4 = blocks.ElementAt(i + 3);
                IBlock block5 = blocks.ElementAt(i + 4);
                List<ILine> five = Match5(block1, block2, block3, block4, block5);
                if (five is not null) {
                    enriched.AddRange(five);
                    i += 5;
                    break;
                }
            }
            if (i < blocks.Count() - 2) {
                IBlock block2 = blocks.ElementAt(i + 1);
                IBlock block3 = blocks.ElementAt(i + 2);
                List<ILine> three = Match3(block1, block2, block3);
                if (three is not null) {
                    enriched.AddRange(three);
                    i += 3;
                    break;
                }
                three = Combo1_2.MatchAny(block1, block2, block3);
                if (three is not null) {
                    enriched.AddRange(three);
                    i += 3;
                    break;
                }
            }
            if (i < blocks.Count() - 1) {
                IBlock block2 = blocks.ElementAt(i + 1);
                List<ILine> two = Match2(block1, block2);
                if (two is not null) {
                    enriched.AddRange(two);
                    i += 2;
                    break;
                }
            }
            List<ILine> one = Match1(block1);
            if (one is not null) {
                enriched.AddRange(one);
                i += 1;
                break;
            }
            enriched.Add(block1);
            i += 1;
        }
        enriched.AddRange(blocks.Skip(i));
        return enriched;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new Exception();
    }

}

}
