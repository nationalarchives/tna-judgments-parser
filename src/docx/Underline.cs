
using System;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

class Underline2 {

    public static UnderlineValues2? Get(Underline underline) {
        if (underline is null)
            return null;
        EnumValue<UnderlineValues> val = underline.Val;
        if (val == null)
            return null;
        if (val == UnderlineValues.Single)
            return UnderlineValues2.Solid;
        if (val ==UnderlineValues.Double)
            return UnderlineValues2.Double;
        if (val == UnderlineValues.Dotted)
            return UnderlineValues2.Dotted;
        if (val == UnderlineValues.Dash)
            return UnderlineValues2.Dashed;
        if (val == UnderlineValues.Wave)
            return UnderlineValues2.Wavy;
        if (val == UnderlineValues.None)
            return UnderlineValues2.None;
        return UnderlineValues2.Solid;
        // throw new Exception();
    }

    public static bool Is(Underline underline) {
        EnumValue<UnderlineValues> val = underline.Val;
        if (val == null)
            return false;
        if (val.Equals(UnderlineValues.None))
            return false;
        return true;
    }

    public static bool? Is(RunProperties properties) {
        Underline underline = properties.Underline;
        if (underline is null)
            return null;
        return Is(underline);
    }

}

}
