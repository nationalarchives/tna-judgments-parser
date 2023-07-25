
using DocumentFormat.OpenXml.Packaging;
using UK.Gov.Legislation.Judgments.AkomaNtoso;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.NationalArchives.CaseLaw.PressSummaries {

public class Helper {

    public static string Parse(byte[] docx) {
        WordprocessingDocument doc = AkN.Parser.Read(docx);
        PressSummary ps = Parser.Parse(doc);
        using PSBundle bundle = new PSBundle(doc, ps);
        return UK.Gov.NationalArchives.Judgments.Api.Parser.SerializeXml(bundle.Xml);
    }

}

}
