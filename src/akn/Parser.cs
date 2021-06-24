using System.Collections.Generic;
using System.IO;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

using Parser1 = UK.Gov.Legislation.Judgments.Parse.Parser;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public class Parser {

    public static IBundle Parse(Stream docx) {
        return new Bundle(docx);
    }

    public static IBundle Parse3(Stream docx) {
        WordprocessingDocument doc = WordprocessingDocument.Open(docx, false);
        IJudgment judgment = UK.Gov.Legislation.Judgments.Parse.EmploymentTribunalParser.Parse(doc);
        return new Bundle2(docx, doc, judgment);
    }

    private class Bundle2 : IBundle {

        private readonly Stream stream;
        private readonly WordprocessingDocument doc;
        private readonly IJudgment judgment;
        private XmlDocument xml;
        private IEnumerable<IImage> images;

        internal Bundle2(Stream docx, WordprocessingDocument doc, IJudgment judgment) {
            this.stream = docx;
            this.doc = doc;
            this.judgment = judgment;
        }

        public XmlDocument Judgment {
            get {
                if (xml is null)
                    xml = Builder.Build(judgment);
                return xml;
            }
        }

        public IEnumerable<IImage> Images {
            get {
                if (images is null)
                    images = UK.Gov.Legislation.Judgments.Parse.WImage.Get(doc);
                return images;
            }
        }

        public void Close() {
            doc.Close();
            stream.Close();
        }

    }

}

class Bundle : IBundle {

    private readonly Stream stream;
    private readonly WordprocessingDocument doc;
    private XmlDocument _xml;
    private IEnumerable<IImage> _images;

    internal Bundle(Stream docx) {
        this.stream = docx;
        this.doc = WordprocessingDocument.Open(docx, false);
    }

    public XmlDocument Judgment {
        get {
            if (_xml is null) {
                IJudgment judgment = Parser1.Parse(doc);
                _xml = Builder.Build(judgment);
            }
            return _xml;
        }
    }

    public IEnumerable<IImage> Images {
        get {
            if (_images is null)
                _images = UK.Gov.Legislation.Judgments.Parse.WImage.Get(doc);
            return _images;
        }
    }

    public void Close() {
        doc.Close();
        stream.Close();
    }

}

}
