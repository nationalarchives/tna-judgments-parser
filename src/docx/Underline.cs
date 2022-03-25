
using System;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

// enum UnderlineValues2 {
//     NONE,
//     SOLID,
//     DOUBLE,
//     DOTTED,
//     DASHED,
//     WAVY
//     // SOLID_THICK
// }

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
        // if (val == UnderlineValues.Words)
        //     return UnderlineValues2.SOLID;
        if (val == UnderlineValues.None)
            return UnderlineValues2.None;
        return UnderlineValues2.Solid;
        // throw new Exception();
    }

    public static bool Is(Underline underline) {
        EnumValue<UnderlineValues> val = underline.Val;
        if (val == null)
            return false;
        // if (val.Equals(UnderlineValues.Single))
        //     return true;
        // if (val.Equals(UnderlineValues.Double))
        //     return true;
        // if (val.Equals(UnderlineValues.Dotted))
        //     return true;
        // if (val.Equals(UnderlineValues.Dash))
        //     return true;
        // if (val.Equals(UnderlineValues.Wave))
        //     return true;
        // if (val.Equals(UnderlineValues.Thick))
        //     return true;
        // if (val == UnderlineValues.Words)
        //     return true;
        if (val.Equals(UnderlineValues.None))
            return false;
        return true;
        // throw new Exception();
    }

    public static bool? Is(RunProperties properties) {
        Underline underline = properties.Underline;
        if (underline is null)
            return null;
        return Is(underline);
    }

}

}
