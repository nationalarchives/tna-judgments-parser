
using System.IO;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.NationalArchives.AkomaNtoso;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

class Helper {

    public static IXmlDocument Parse(Stream docx, bool simplify = true) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word, simplify);
    }

    public static IXmlDocument Parse(byte[] docx, bool simplify = true) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word, simplify);
    }

    private static IXmlDocument Parse(WordprocessingDocument docx, bool simplify) {
        IDocument doc = ExplanatoryMemoranda.Parser.Parse(docx);
        XmlDocument xml = Builder.Build(doc);
        docx.Dispose();
        if (simplify)
            Simplifier.Simplify(xml);
        return new XmlDocument_ { Document = xml };
    }

    public static byte[] ReadImage(Judgments.IImage image) {
        using var stream = new MemoryStream();
        image.Content().CopyTo(stream);
        return stream.ToArray();
    }

}

}
