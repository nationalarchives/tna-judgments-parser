
using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;

using DocumentFormat.OpenXml.Packaging;

using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;
using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace UK.Gov.NationalArchives.CaseLaw.PressSummaries {

public class Helper {

    public static string Parse(byte[] docx) {
        WordprocessingDocument doc = AkN.Parser.Read(docx);
        PressSummary ps = Parser.Parse(doc);
        using var bundle = new AkN.PSBundle(doc, ps);
        return UK.Gov.NationalArchives.Judgments.Api.Parser.SerializeXml(bundle.Xml);
    }

    public static string ParseAndValidate(byte[] docx) {
        WordprocessingDocument doc = AkN.Parser.Read(docx);
        PressSummary ps = Parser.Parse(doc);
        using var bundle = new AkN.PSBundle(doc, ps);

        AkN.Validator validator = new AkN.Validator();
        List<ValidationEventArgs> errors = validator.Validate(bundle.Xml);
        if (errors.Any())
            throw new Api.InvalidAkNException(errors.First());

        return UK.Gov.NationalArchives.Judgments.Api.Parser.SerializeXml(bundle.Xml);
    }

}

}
