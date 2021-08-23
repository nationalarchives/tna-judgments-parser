
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

struct Combo3 {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Regex Re3 { get; init; }
    public Court Court { get; init; }

    internal static Combo3[] combos = new Combo3[] {
        new Combo3 {
            Re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^QUEEN[’']S BENCH DIVISION$", RegexOptions.IgnoreCase),
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
            Re2 = new Regex("^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES \\(ChD\\)", RegexOptions.IgnoreCase),
            Re3 = new Regex("^BUSINESS LIST", RegexOptions.IgnoreCase),
            Court = Courts.HC_Chancery_BusAndProp_BusinessList
        }
    };

}

struct Combo2 {

    public Regex Re1 { get; init; }
    public Regex Re2 { get; init; }
    public Court Court { get; init; }

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
            Re2 = new Regex("CRIMINAL DIVISION"),
            Court = Courts.CoA_Crim
        },
        new Combo2 {
            Re1 = new Regex("IN THE HIGH COURT OF JUSTICE"),
            Re2 = new Regex("CHANCERY DIVISION"),
            Court = Courts.EWHC_QBD_Chancery
        },
        new Combo2 {
            Re1 = new Regex("IN THE HIGH COURT OF JUSTICE"),
            Re2 = new Regex("FAMILY DIVISION"),
            Court = Courts.EWFC
        }
        
    };

}


class CourtType : Enricher {

    // Regex re1 = new Regex("^IN THE (HIGH COURT OF JUSTICE)$", RegexOptions.IgnoreCase);
    // Regex re2 = new Regex("^QUEEN'S BENCH DIVISION$", RegexOptions.IgnoreCase);
    // Regex re3 = new Regex("^ADMINISTRATIVE COURT$", RegexOptions.IgnoreCase);
    // Regex re4 = new Regex("^PLANNING COURT$", RegexOptions.IgnoreCase);

    private bool Match(Regex regex, IBlock block) {
        if (!(block is WLine line))
            return false;
        if (line.Contents.Count() != 1)
            return false;
        if (!(line.Contents.First() is WText text))
            return false;
        return regex.IsMatch(text.Text.Trim());
    }

    private List<IBlock> Match3(IBlock one, IBlock two, IBlock three) {
        foreach (Combo3 combo in Combo3.combos) {
            if (Match(combo.Re1, one) && Match(combo.Re2, two) && Match(combo.Re3, three)) {
                WLine line1 = (WLine) one;
                WText text1 = (WText) line1.Contents.First();
                WCourtType ct1 = new WCourtType(text1.Text, text1.properties) { Code = combo.Court.Code };
                WLine newLine1 = new WLine(line1, new List<IInline>(1) { ct1 });
                WLine line2 = (WLine) two;
                WText text2 = (WText) line2.Contents.First();
                WCourtType ct2 = new WCourtType(text2.Text, text2.properties) { Code = combo.Court.Code };
                WLine newLine2 = new WLine(line2, new List<IInline>(1) { ct2 });
                WLine line3 = (WLine) three;
                WText text3 = (WText) line3.Contents.First();
                WCourtType ct3 = new WCourtType(text3.Text, text3.properties) { Code = combo.Court.Code };
                WLine newLine3 = new WLine(line3, new List<IInline>(1) { ct3 });
                return new List<IBlock>(3) {
                    newLine1, newLine2, newLine3
                };
            }
        }
        return null;
    }

    private List<IBlock> Match2(IBlock one, IBlock two) {
        foreach (Combo2 combo in Combo2.combos) {
            if (Match(combo.Re1, one) && Match(combo.Re2, two)) {
                WLine line1 = (WLine) one;
                WText text1 = (WText) line1.Contents.First();
                WCourtType ct1 = new WCourtType(text1.Text, text1.properties) { Code = combo.Court.Code };
                WLine newLine1 = new WLine(line1, new List<IInline>(1) { ct1 });
                WLine line2 = (WLine) two;
                WText text2 = (WText) line2.Contents.First();
                WCourtType ct2 = new WCourtType(text2.Text, text2.properties) { Code = combo.Court.Code };
                WLine newLine2 = new WLine(line2, new List<IInline>(1) { ct2 });
                return new List<IBlock>(2) {
                    newLine1, newLine2
                };
            }
        }
        return null;
    }

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> enriched = new List<IBlock>();
        int i = 0;
        while (i < blocks.Count() - 2) {
            IBlock block1 = blocks.ElementAt(i);
            IBlock block2 = blocks.ElementAt(i + 1);
            IBlock block3 = blocks.ElementAt(i + 2);
            List<IBlock> three = Match3(block1, block2, block3);
            if (three is not null) {
                enriched.AddRange(three);
                i += 3;
                break;
            }
            List<IBlock> two = Match2(block1, block2);
            if (two is not null) {
                enriched.AddRange(two);
                i += 2;
                break;
            }
            IBlock before = blocks.ElementAt(i);
            IBlock after = Enrich(before);
            enriched.Add(after);
            i += 1;
        }
        while (i < blocks.Count()) {
            IBlock before = blocks.ElementAt(i);
            IBlock after = Enrich(before);
            enriched.Add(after);
            i += 1;
        }
        return enriched;
    }

struct Combo1 {

    public Regex Re { get; init; }
    public Court Court { get; init; }

    internal static Combo1[] combos = new Combo1[] {
        new Combo1 {
            Re = new Regex("^IN THE (COURT OF APPEAL \\(CRIMINAL DIVISION\\)) *$", RegexOptions.IgnoreCase),
            Court = Courts.CoA_Crim
        },
        new Combo1 {
            Re = new Regex("^IN THE (COURT OF APPEAL \\(CIVIL DIVISION\\)) *$", RegexOptions.IgnoreCase),
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
            Re = new Regex("^(THE FAMILY COURT) SITTING AT [A-Z]+ *$", RegexOptions.IgnoreCase),
            Court = Courts.EWFC
        }
        
    };

}

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        return line.SelectMany(inline => {
            if (inline is WText text) {
                foreach (Combo1 combo in Combo1.combos) {
                    Match match = combo.Re.Match(text.Text);
                    if (match.Success) {
                        Group group = match.Groups[1];
                        return CaseNo.Split(text, group, (t, props) => new WCourtType(t, props) { Code = combo.Court.Code });
                    }
                }
            }
            return new List<IInline>(1) { inline };
        });
    }

}

}
