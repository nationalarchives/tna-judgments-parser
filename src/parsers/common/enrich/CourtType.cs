
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

abstract class Combo {

    protected bool Match(Regex regex, IBlock block) {
        if (!(block is WLine line))
            return false;
        if (line.Contents.Count() != 1)
            return false;
        if (!(line.Contents.First() is WText text))
            return false;
        return regex.IsMatch(text.Text.Trim());
    }

    public Court Court { get; init; }

    protected WLine Transform1(IBlock block) {
        WLine line = (WLine) block;
        WText text = (WText) line.Contents.First();
        WCourtType ct = new WCourtType(text.Text, text.properties) { Code = this.Court.Code };
        return new WLine(line, new List<IInline>(1) { ct });
    }

}

class Combo5 : Combo {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }
    public Regex Re4 { get; init; }
    public Regex Re5 { get; init; }
    // public Court Court { get; init; }

    internal static Combo5[] combos = new Combo5[] {
        new Combo5 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^OF ENGLAND AND WALES$", RegexOptions.IgnoreCase),
            Re4 = new Regex("^QUEEN'S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re5 = new Regex("^COMMERCIAL COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Commercial
        }
    };

    internal bool Match(IBlock one, IBlock two, IBlock three, IBlock four, IBlock five) {
        return Match(Re1, one) && Match(Re2, two) && Match(Re3, three) && Match(Re4, four) && Match(Re5, five);
    }

    // private WLine Transform1(IBlock block) {
    //     WLine line = (WLine) block;
    //     WText text = (WText) line.Contents.First();
    //     WCourtType ct = new WCourtType(text.Text, text.properties) { Code = this.Court.Code };
    //     return new WLine(line, new List<IInline>(1) { ct });
    // }

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
    // public Court Court { get; init; }

    internal static Combo3[] combos = new Combo3[] {
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']?S BENCH DIVISION$", RegexOptions.IgnoreCase),   // no apostrophe in EWHC/Admin/2009/573
            Re3 = new Regex("^ADMINISTRATIVE COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Admin
        },
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^PLANNING COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Admin_Planning
        },
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^LONDON CIRCUIT COMMERCIAL COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Circuit_Commercial_Court
        },
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Re3 = new Regex("^TECHNOLOGY AND CONSTRUCTION COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_TCC
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^CHANCERY DIVISION", RegexOptions.IgnoreCase),
            Re3 = new Regex("^PATENTS COURT", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Chancery_Patents
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES \\(ChD\\)", RegexOptions.IgnoreCase),
            Re3 = new Regex("^BUSINESS LIST", RegexOptions.IgnoreCase),
            Court = Courts.HC_Chancery_BusAndProp_BusinessList
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES", RegexOptions.IgnoreCase),
            Re3 = new Regex("^CHANCERY APPEALS \\(ChD\\)", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD_Chancery
        },
        new Combo3 {
            Re1 = new Regex("^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND AND? WALES", RegexOptions.IgnoreCase),    // missing D in EWHC/QB/2017/2921
            Re3 = new Regex("^COMMERCIAL COURT$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_Commercial
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

class Combo2 : Combo {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    // public Court Court { get; init; }

    internal static Combo2[] combos = new Combo2[] {
        new Combo2 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^(IN THE )?QUEEN[’']S BENCH DIVISION$", RegexOptions.IgnoreCase),
            Court = Courts.EWHC_QBD
        },
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
        new Combo2 {
            Re1 = new Regex("IN THE HIGH COURTS? OF JUSTICE"),
            Re2 = new Regex("CHANCERY DIVISION"),
            Court = Courts.EWHC_QBD_Chancery
        },
        new Combo2 {
            Re1 = new Regex("IN THE HIGH COURT OF JUSTICE"),
            Re2 = new Regex("CHANCERY DIVISION (PROBATE)"),
            Court = Courts.EWHC_QBD_Chancery
        },
        new Combo2 {
            Re1 = new Regex("IN THE HIGH COURT OF JUSTICE"),
            Re2 = new Regex("FAMILY DIVISION"),
            Court = Courts.EWFC
        },
        new Combo2 {    // EWHC/Admin/2008/2214
            Re1 = new Regex("IN THE HIGH COURT OF JUSTICE"),
            Re2 = new Regex("ADMINISTRATIVE DIVISION"),
            Court = Courts.EWHC_QBD_Admin
        }
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
    // public Court Court { get; init; }

    // internal bool IsMatch(IBlock block) {
    //     return Combo5.Match(Re, block);
    // }

    // internal WLine Transform(IBlock block) {
    //     WLine line = (WLine) block;
    //     WText text = (WText) line.Contents.First();
    //     WCourtType ct = new WCourtType(text.Text, text.properties) { Code = this.Court.Code };
    //     return new WLine(line, new List<IInline>(1) { ct });
    // }

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

    // this should probably be removed
    // private bool Match(Regex regex, IBlock block) {
    //     if (!(block is WLine line))
    //         return false;
    //     if (line.Contents.Count() != 1)
    //         return false;
    //     if (!(line.Contents.First() is WText text))
    //         return false;
    //     return regex.IsMatch(text.Text.Trim());
    // }
    // private bool MatchFirstOfMany(Regex regex, IBlock block) {
    //     if (!(block is WLine line))
    //         return false;
    //     if (line.Contents.Count() < 1)
    //         return false;
    //     IInline first = line.Contents.First();
    //     if (first is not WText text)
    //         return false;
    //     return regex.IsMatch(text.Text.Trim());
    // }

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
        // foreach (Combo3 combo in Combo3.combos) {
        //     if (Match(combo.Re1, one) && Match(combo.Re2, two) && Match(combo.Re3, three)) {
        //         WLine line1 = (WLine) one;
        //         WText text1 = (WText) line1.Contents.First();
        //         WCourtType ct1 = new WCourtType(text1.Text, text1.properties) { Code = combo.Court.Code };
        //         WLine newLine1 = new WLine(line1, new List<IInline>(1) { ct1 });
        //         WLine line2 = (WLine) two;
        //         WText text2 = (WText) line2.Contents.First();
        //         WCourtType ct2 = new WCourtType(text2.Text, text2.properties) { Code = combo.Court.Code };
        //         WLine newLine2 = new WLine(line2, new List<IInline>(1) { ct2 });
        //         WLine line3 = (WLine) three;
        //         WText text3 = (WText) line3.Contents.First();
        //         WCourtType ct3 = new WCourtType(text3.Text, text3.properties) { Code = combo.Court.Code };
        //         WLine newLine3 = new WLine(line3, new List<IInline>(1) { ct3 });
        //         return new List<IBlock>(3) {
        //             newLine1, newLine2, newLine3
        //         };
        //     }
        // }
        return null;
    }

    private List<ILine> Match2(IBlock one, IBlock two) {
        foreach (Combo2 combo in Combo2.combos)
            if (combo.Match(one, two))
                return combo.Transform(one, two);
        // foreach (Combo2 combo in Combo2.combos) {
        //     if (MatchFirstOfMany(combo.Re1, one) && MatchFirstOfMany(combo.Re2, two)) {
        //         WLine line1 = (WLine) one;
        //         WText text1 = (WText) line1.Contents.First();
        //         WCourtType ct1 = new WCourtType(text1.Text, text1.properties) { Code = combo.Court.Code };
        //         IEnumerable<IInline> rest1 = line1.Contents.Skip(1);
        //         List<IInline> contents1 = new List<IInline>(line1.Contents.Count());
        //         contents1.Add(ct1);
        //         contents1.AddRange(rest1);
        //         WLine newLine1 = new WLine(line1, contents1);
        //         WLine line2 = (WLine) two;
        //         WText text2 = (WText) line2.Contents.First();
        //         WCourtType ct2 = new WCourtType(text2.Text, text2.properties) { Code = combo.Court.Code };
        //         IEnumerable<IInline> rest2 = line2.Contents.Skip(1);
        //         List<IInline> contents2 = new List<IInline>(line2.Contents.Count());
        //         contents2.Add(ct2);
        //         contents2.AddRange(rest2);
        //         WLine newLine2 = new WLine(line2, contents2);
        //         return new List<IBlock>(2) { newLine1, newLine2 };
        //     }
        // }
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
            // IBlock before = blocks.ElementAt(i);
            // IBlock after = Enrich(before);
            // enriched.Add(after);
            enriched.Add(block1);
            i += 1;
        }
        enriched.AddRange(blocks.Skip(i));
        return enriched;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new Exception();
        // return line.SelectMany(inline => {
        //     if (inline is WText text) {
        //         foreach (Combo1 combo in Combo1.combos) {
        //             Match match = combo.Re.Match(text.Text);
        //             if (match.Success) {
        //                 Group group = match.Groups[1];
        //                 return CaseNo.Split(text, group, (t, props) => new WCourtType(t, props) { Code = combo.Court.Code });
        //             }
        //         }
        //     }
        //     return new List<IInline>(1) { inline };
        // });
    }

}

}
