
using System;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

class Underline2 {

    public static bool Is(Underline underline) {
        EnumValue<UnderlineValues> val = underline.Val;
        if (val == null)
            return false;
        if (val.Equals(UnderlineValues.Single))
            return true;
        if (val.Equals(UnderlineValues.Thick))
            return true;
        if (val.Equals(UnderlineValues.None))
            return false;
        throw new Exception();
    }

    public static bool? Is(RunProperties properties) {
        Underline underline = properties.Underline;
        if (underline is null)
            return null;
        return Is(underline);
    }

}

}
