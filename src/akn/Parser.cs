
using System;
using System.IO;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public class Parser {

    private static ILogger logger = Logging.Factory.CreateLogger<UK.Gov.Legislation.Judgments.AkomaNtoso.Parser>();

    internal delegate IJudgment Helper(WordprocessingDocument doc);

    private static ILazyBundle Parse(Stream docx, Helper parse) {
        MemoryStream ms = new MemoryStream();
        docx.CopyTo(ms);
        byte[] docx2 = ms.ToArray();
        MemoryStream stream2 = new MemoryStream();
        stream2.Write(docx2, 0, docx2.Length);
        WordprocessingDocument doc;
        try {
            doc = WordprocessingDocument.Open(stream2, false);
        } catch (OpenXmlPackageException) {
            stream2 = new MemoryStream();
            stream2.Write(docx2, 0, docx2.Length);
            var settings = new OpenSettings() {
                RelationshipErrorHandlerFactory = RelationshipErrorHandler.CreateRewriterFactory(DOCX.Relationships.MalformedUriRewriter)
            };
            doc = WordprocessingDocument.Open(stream2, true, settings);
        }
        IJudgment judgment = parse(doc);
        return new Bundle(doc, judgment);
    }

    public static ILazyBundle ParseSupremeCourtJudgment(Stream docx) {
        Helper parser = UK.Gov.Legislation.Judgments.Parse.SupremeCourtParser.Parse;
        return Parse(docx, parser);
    }

    public static ILazyBundle ParseCourtOfAppealJudgment(Stream docx) {
        Helper parser = UK.Gov.Legislation.Judgments.Parse.CourtOfAppealParser.Parse;
        return Parse(docx, parser);
    }

    public static ILazyBundle ParseEmploymentTribunalJudgment(Stream docx) {
        Helper parser = UK.Gov.Legislation.Judgments.Parse.EmploymentTribunalParser.Parse;
        return Parse(docx, parser);
    }

    internal static Func<Stream, ILazyBundle> MakeParser(Helper raw) {
        return (Stream docx) => Parse(docx, raw);
    }

}

}
