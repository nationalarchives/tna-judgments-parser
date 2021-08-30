
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

internal class RunProperties2 : RunProperties {

    private readonly OpenXmlElement element;

    public RunProperties2(OpenXmlElement rPr) : base(rPr.OuterXml) {
        element = rPr;
    }

    new public IEnumerable<T> Ancestors<T> () where T : OpenXmlElement {
        return element.Ancestors<T>();
    }

    public OpenXmlPartRootElement Root() {
        return element.Ancestors<OpenXmlPartRootElement>().First();
    }

}