
using System.Xml;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

class Stylesheet {

    public static XmlDocument AddStylesheet(XmlDocument doc, string path) {
        XmlDocument doc2 = (XmlDocument) doc.Clone();
        XmlProcessingInstruction stylesheet = doc2.CreateProcessingInstruction("xml-stylesheet", "href='" + path + "' type='text/xsl'");
        doc2.InsertBefore(stylesheet, doc2.FirstChild);
        return doc2;
    }

}

}
