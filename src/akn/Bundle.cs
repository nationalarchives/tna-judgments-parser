
using System.Collections.Generic;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;
using UK.Gov.NationalArchives.CaseLaw;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public interface ILazyBundle {

    string ShortUriComponent { get; }

    XmlDocument Judgment { get; }

    IEnumerable<IImage> Images { get; }

    void Close();

}


internal class Bundle : ILazyBundle {

    private readonly WordprocessingDocument doc;
    private readonly IJudgment judgment;
    private XmlDocument xml;

    internal Bundle(WordprocessingDocument doc, IJudgment judgment) {
        this.doc = doc;
        this.judgment = judgment;
        ImageConverter.ConvertImages(judgment);
    }

    public string ShortUriComponent { get => judgment.Metadata.ShortUriComponent; }

    public XmlDocument Judgment {
        get {
            if (xml is null)
                xml = JudgmentBuilder.Build(judgment);
            return xml;
        }
    }

    public IEnumerable<IImage> Images { get => judgment.Images; }

    public void Close() {
        doc.Close();
    }

}

}
