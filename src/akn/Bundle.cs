
using System.Collections.Generic;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public interface ILazyBundle {

    string URI { get; }

    XmlDocument Judgment { get; }

    IEnumerable<IImage> Images { get; }

    void Close();

}




internal class Bundle : ILazyBundle {

    private readonly WordprocessingDocument doc;
    private readonly IJudgment judgment;
    private XmlDocument xml;
    private IEnumerable<IImage> images;

    internal Bundle(WordprocessingDocument doc, IJudgment judgment) {
        this.doc = doc;
        this.judgment = judgment;
    }

    public string URI { get => judgment.Metadata.DocumentId(); }

    public XmlDocument Judgment {
        get {
            if (xml is null)
                xml = Builder.Build(judgment);
            return xml;
        }
    }

    public IEnumerable<IImage> Images { get => judgment.Images; }

    public void Close() {
        doc.Close();
    }

}

}
