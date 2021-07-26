
using System;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

static class Fields {

    internal static bool IsFieldStart(Run run) {
        if (!run.ChildElements.Any(child => child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.Begin)))
            return false;
        if (!run.ChildElements.All(child => child is RunProperties || child is LastRenderedPageBreak || (child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.Begin))))
            throw new Exception();
        return true;
    }

    internal static bool IsFieldSeparater(Run run) {
        if (!run.ChildElements.Any(child => child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.Separate)))
            return false;
        if (!run.ChildElements.All(child => child is RunProperties || (child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.Separate))))
            throw new Exception();
        return true;
    }

    internal static bool IsFieldEnd(Run run) {
        if (!run.ChildElements.Any(child => child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.End)))
            return false;
        if (!run.ChildElements.All(child => child is RunProperties || (child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.End))))
            throw new Exception();
        return true;
    }

    internal static Paragraph CloneAndRemoveFieldCodes(this Paragraph p) {
        Paragraph p2 = (Paragraph) p.CloneNode(true);
        foreach (FieldCode fc in p2.Descendants().Where(e => e.LocalName == "instrText"))
            fc.Remove();
        // bool inside = false;
        // foreach (Run run in p2.ChildElements.OfType<Run>()) {
        //     if (inside) {
        //         run.Remove();
        //         inside = !IsFieldEnd(run);
        //     } else if (IsFieldStart(run)) {
        //         run.Remove();
        //         inside = true;
        //     }
        // }
        return p2;
    }

}

}
