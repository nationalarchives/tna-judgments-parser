
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw {

public class CourtTypeTest {

    public static Court? Test(string[] lines) {
        if (lines.Length == 4) {
            foreach (Combo4 combo in Combo4.combos) {
                if (combo is null)
                    continue;
                if (!combo.Re1.IsMatch(lines[0]))
                    continue;
                if (!combo.Re2.IsMatch(lines[1]))
                    continue;
                if (!combo.Re3.IsMatch(lines[2]))
                    continue;
                if (!combo.Re4.IsMatch(lines[3]))
                    continue;
                return combo.Court;
            }
        }
        if (lines.Length == 3) {
            foreach (Combo3 combo in Combo3.combos) {
                if (combo is null)
                    continue;
                if (!combo.Re1.IsMatch(lines[0]))
                    continue;
                if (!combo.Re2.IsMatch(lines[1]))
                    continue;
                if (!combo.Re3.IsMatch(lines[2]))
                    continue;
                return combo.Court;
            }
            foreach (Combo2_1 combo in Combo2_1.combos) {
                if (combo is null)
                    continue;
                if (!combo.Re1.IsMatch(lines[0]))
                    continue;
                if (!combo.Re2.IsMatch(lines[1]))
                    continue;
                if (!combo.Re3.IsMatch(lines[2]))
                    continue;
                return combo.Court;
            }
            foreach (Combo1_2 combo in Combo1_2.combos) {
                if (combo is null)
                    continue;
                if (!combo.Re1.IsMatch(lines[0]))
                    continue;
                if (!combo.Re2.IsMatch(lines[1]))
                    continue;
                if (!combo.Re3.IsMatch(lines[2]))
                    continue;
                return combo.Court;
            }
        }
        return null;
    }

}

}
