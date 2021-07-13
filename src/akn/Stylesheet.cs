
using System.Xml;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

class Stylesheet {

    public static void AddStylesheet(XmlDocument doc, string path) {
        XmlProcessingInstruction stylesheet = doc.CreateProcessingInstruction("xml-stylesheet", "href='" + path + "' type='text/xsl'");
        doc.InsertBefore(stylesheet, doc.FirstChild);
    }

}

}
