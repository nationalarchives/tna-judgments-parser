using System;
using System.Collections.Generic;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.NationalArchives.CaseLaw;
using UK.Gov.NationalArchives.CaseLaw.PressSummaries;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso;

public interface ILazyBundle : IDisposable
{
    string ShortUriComponent { get; }

    XmlDocument Judgment { get; }

    IEnumerable<IImage> Images { get; }
}

internal class Bundle : ILazyBundle
{
    private readonly WordprocessingDocument doc;
    private readonly IJudgment judgment;
    private XmlDocument xml;

    internal Bundle(WordprocessingDocument doc, IJudgment judgment)
    {
        this.doc = doc;
        this.judgment = judgment;
        ImageConverter.ConvertImages(judgment);
    }

    public string ShortUriComponent => judgment.Metadata.ShortUriComponent;

    public XmlDocument Judgment
    {
        get
        {
            if (xml is null)
            {
                xml = JudgmentBuilder.Build(judgment);
            }

            return xml;
        }
    }

    public IEnumerable<IImage> Images => judgment.Images;

    public void Dispose()
    {
        doc.Dispose();
    }
}

internal class PSBundle : ILazyBundle
{
    private readonly WordprocessingDocument doc;
    private readonly PressSummary PS;

    internal PSBundle(WordprocessingDocument doc, PressSummary ps)
    {
        this.doc = doc;
        PS = ps;
        ImageConverter.ConvertImages(PS);
    }

    public string ShortUriComponent => PS.InternalMetadata.ShortUriComponent;

    private XmlDocument _xml;

    public XmlDocument Judgment
    {
        get
        {
            if (_xml is null)
            {
                _xml = DocBuilder.Build(PS);
            }

            return _xml;
        }
    }

    public XmlDocument Xml => Judgment;

    public IEnumerable<IImage> Images => PS.Images;

    public void Dispose()
    {
        doc.Dispose();
    }
}
