
using System.Xml;

namespace UK.Gov.Legislation.Judgments.Parse {

class WMath : IMath {

    internal WMath(XmlElement mathML) {
        MathML = mathML;
    }

    public XmlElement MathML { get; init; }

}

}
