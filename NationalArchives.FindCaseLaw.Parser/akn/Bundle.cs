using System;
using System.Collections.Generic;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

using NationalArchives.FindCaseLaw.Utils.Metadata;

using UK.Gov.NationalArchives.CaseLaw;
using UK.Gov.NationalArchives.CaseLaw.PressSummaries;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso;

public interface ILazyBundle : IDisposable
{
    XmlDocument Judgment { get; }

    IEnumerable<IImage> Images { get; }

    IMetadataField[] StructuredMetadataFields { get; }
}

internal class Bundle : ILazyBundle
{
    private readonly WordprocessingDocument doc;
    private readonly IJudgment judgment;

    internal Bundle(WordprocessingDocument doc, IJudgment judgment)
    {
        this.doc = doc;
        this.judgment = judgment;
        ImageConverter.ConvertImages(judgment);
        StructuredMetadataFields = judgment.StructuredMetadataFields;
    }

    public XmlDocument Judgment
    {
        get
        {
            field ??= JudgmentBuilder.Build(judgment);

            return field;
        }
    }

    public IEnumerable<IImage> Images => judgment.Images;

    public IMetadataField[] StructuredMetadataFields { get; }

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

    public XmlDocument Judgment
    {
        get
        {
            field ??= DocBuilder.Build(PS);

            return field;
        }
    }

    public IEnumerable<IImage> Images => PS.Images;

    public IMetadataField[] StructuredMetadataFields { get; } = [];

    public void Dispose()
    {
        doc.Dispose();
    }
}
