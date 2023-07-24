
using System.Collections.Generic;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.NationalArchives.CaseLaw;
using UK.Gov.NationalArchives.CaseLaw.PressSummaries;

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

internal class PSBundle : ILazyBundle {

    private readonly WordprocessingDocument doc;
    private readonly PressSummary2 PS;

    internal PSBundle(WordprocessingDocument doc, PressSummary2 ps) {
        this.doc = doc;
        this.PS = ps;
        ImageConverter.ConvertImages(PS);
    }

    public string ShortUriComponent { get => PS.Metadata.ShortUriComponent; }

    private XmlDocument _xml;
    public XmlDocument Judgment {
        get {
            if (_xml is null)
                _xml = DocBuilder.Build(PS);
            return _xml;
        }
    }
    public XmlDocument Xml => Judgment;

    public IEnumerable<IImage> Images { get => PS.Images; }

    public void Close() {
        doc.Close();
    }

}

}
