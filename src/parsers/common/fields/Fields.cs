
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.Judgments.Parse {

class Fields {

    internal static ILogger logger = Logging.Factory.CreateLogger<Parse.Fields>();

    internal static bool IsFieldStart(OpenXmlElement e) {
        if (e is not Run run)
            return false;
        return DOCX.Fields.IsFieldStart(run);
    }

    internal static bool IsFieldSeparater(OpenXmlElement e) {
        if (e is not Run run)
            return false;
        return DOCX.Fields.IsFieldSeparater(run);
    }

    internal static bool IsFieldEnd(OpenXmlElement e) {
        if (e is not Run run)
            return false;
        return DOCX.Fields.IsFieldEnd(run);
    }

    internal static bool IsFieldCode(OpenXmlElement e) {
        if (e is not Run)
            return false;
        if (!e.ChildElements.Any(child => child is FieldCode || child.LocalName == "instrText"))
            return false;
        if (!e.ChildElements.All(child => child is RunProperties || child is FieldCode || child.LocalName == "instrText" || child is TabChar))  // tab in EWHC/Ch/2015/448
            throw new Exception();
        return true;
    }

    internal static string GetFieldCode(OpenXmlElement e) {
        IEnumerable<FieldCode> fieldCodes = e.ChildElements.OfType<FieldCode>();
        if (fieldCodes.Count() != 1)
            throw new Exception();
        return fieldCodes.First().InnerText;
    }

    private static int CountPrecedingListNumLegalDefault(OpenXmlElement fc) {
        int count = 0;
        Paragraph previous = fc.Ancestors<Paragraph>().First().PreviousSibling<Paragraph>();
        while (previous != null) {
            FieldCode listNum = previous.Descendants<FieldCode>().Where(fc => fc.InnerText.Trim() == "LISTNUM LegalDefault").FirstOrDefault();
            if (listNum is not null)
                count += 1;
            previous = previous.PreviousSibling<Paragraph>();
        }
        return count;
    }

    internal static INumber RemoveListNum(WLine line) {
        if (line.Contents.Count() == 0)
            return null;
        IInline first = line.Contents.First();
        if (first is INumber firstNumber) {
            line.Contents = line.Contents.Skip(1);
            if (line.Contents.First() is WTab)
                line.Contents = line.Contents.Skip(1);
            return firstNumber;
        }
        return null;
    }

    /* AUTONUM */
    /* incomplete: will not format correctly */
    internal static INumber Autonum(MainDocumentPart main, Run run) {  // EWHC/Ch/2005/2793
        Paragraph paragraph = run.Ancestors<Paragraph>().First();
        int n = 1;
        Paragraph preceding = paragraph.PreviousSibling<Paragraph>();
        while (preceding is not null) {
            if (preceding.Descendants().Where(e => e is FieldCode || e.LocalName == "instrText").Where(e => e.InnerText.Trim() == "AUTONUM").Any())
                n += 1;
            preceding = preceding.PreviousSibling<Paragraph>();
        }
        return new DOCX.WNumber2(n.ToString() + ".", run.RunProperties, main, paragraph.ParagraphProperties);
    }
    private static INumber Autonum(MainDocumentPart main, Run run, string regex) {  // EWHC/Ch/2007/2841
        Paragraph paragraph = run.Ancestors<Paragraph>().First();
        int n = 1;
        Paragraph preceding = paragraph.PreviousSibling<Paragraph>();
        while (preceding is not null) {
            if (preceding.Descendants().Where(e => e is FieldCode || e.LocalName == "instrText").Where(e => Regex.IsMatch(e.InnerText, regex)).Any())
                n += 1;
            preceding = preceding.PreviousSibling<Paragraph>();
        }
        return new DOCX.WNumber2(n.ToString() + ".", run.RunProperties, main, paragraph.ParagraphProperties);
    }

    internal static IEnumerable<IInline> ParseSimple(MainDocumentPart main, SimpleField fldSimple) {
        logger.LogWarning("simple field: {}", fldSimple.Instruction);
        if (!fldSimple.ChildElements.Any())
            logger.LogError("simple field has no child content");
        return Inline2.ParseRuns(main, fldSimple.ChildElements);
    }

}

}
