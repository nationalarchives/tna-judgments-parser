
using System.IO;

using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public class Parser {

    private delegate IJudgment Helper(WordprocessingDocument doc);

    private static ILazyBundle Parse(Stream docx, Helper parse) {
        WordprocessingDocument doc = WordprocessingDocument.Open(docx, false);
        IJudgment judgment = parse(doc);
        return new Bundle(doc, judgment);
    }

    public static ILazyBundle ParseCourtOfAppealJudgment(Stream docx) {
        Helper parser = UK.Gov.Legislation.Judgments.Parse.CourtOfAppealParser.Parse;
        return Parse(docx, parser);
    }

    public static ILazyBundle ParseEmploymentTribunalJudgment(Stream docx) {
        Helper parser = UK.Gov.Legislation.Judgments.Parse.EmploymentTribunalParser.Parse;
        return Parse(docx, parser);
    }

}

}
