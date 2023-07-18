
using System.IO;
using System.Xml;
using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation {

class API {

    public static IXmlDocument Parse(Stream docx) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word);
    }

    public static IXmlDocument Parse(byte[] docx) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word);
    }

    private static IXmlDocument Parse(WordprocessingDocument docx) {
        IDocument doc = ExplanatoryMemoranda.Parser.Parse(docx);
        XmlDocument xml = Builder.Build(doc);
        return new XmlDocument_ { Document = xml };
    }

}

}
