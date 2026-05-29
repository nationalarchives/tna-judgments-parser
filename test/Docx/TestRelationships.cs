#nullable enable

using System;
using System.Collections.Generic;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using test.Mocks;

using UK.Gov.Legislation.Judgments.DOCX;

using Xunit;

namespace test.Docx;

public class TestRelationships
{
    public static IEnumerable<object[]> RootParts =
    [
        [new GetDocumentArea<MainDocumentPart>(d => (d.Body, d.MainDocumentPart))],
        [new GetDocumentArea<HeaderPart>(d => (d.Header, d.HeaderPart))],
        [new GetDocumentArea<FootnotesPart>(d => (d.Footnotes, d.FootnotesPart))],
        [new GetDocumentArea<EndnotesPart>(d => (d.Endnotes, d.EndnotesPart))]
    ];

    public delegate (OpenXmlCompositeElement rootElement, T part)
        GetDocumentArea<T>(MockWordprocessingDocument document)
        where T : OpenXmlPart, ISupportedRelationship<ImagePart>;

    [Theory]
    [MemberData(nameof(RootParts))]
    public void GetUriForHyperlink_should_return_correct_uri<T>(GetDocumentArea<T> getDocumentArea)
        where T : OpenXmlPart, ISupportedRelationship<ImagePart>
    {
        using var mockWordprocessingDocument = new MockWordprocessingDocument();

        var docUri = new Uri("https://example.com/some-link");
        var (rootElement, part) = getDocumentArea(mockWordprocessingDocument);

        var docHyperlink = rootElement.AppendChild(new Paragraph())
                                      .AddHyperlink(part, docUri);

        Assert.Equal(docUri, Relationships.GetUriForHyperlink(docHyperlink));
    }

    [Theory]
    [MemberData(nameof(RootParts))]
    public void GetUriForImage_should_return_uri_for_internal_image<T>(GetDocumentArea<T> getDocumentArea)
        where T : OpenXmlPart, ISupportedRelationship<ImagePart>
    {
        using var mockWordprocessingDocument = new MockWordprocessingDocument();
        var (rootElement, part) = getDocumentArea(mockWordprocessingDocument);

        var drawing = rootElement.AppendChild(new Paragraph())
                                 .AddImage(part, out var imagePart, out var relationshipId);
        var relId = new StringValue(relationshipId);

        Assert.Equal(imagePart.Uri, Relationships.GetUriForImage(relId, drawing));
    }

    [Theory]
    [MemberData(nameof(RootParts))]
    public void GetUriForImage_should_return_uri_for_external_image<T>(GetDocumentArea<T> getDocumentArea)
        where T : OpenXmlPart, ISupportedRelationship<ImagePart>
    {
        using var mockWordprocessingDocument = new MockWordprocessingDocument();

        var (rootElement, part) = getDocumentArea(mockWordprocessingDocument);
        var uri = new Uri("https://example.com/external-image.png");

        var drawing = rootElement.AppendChild(new Paragraph())
                                 .AddExternalImageToDocument(part, uri, out var relationshipId);
        var relId = new StringValue(relationshipId);

        Assert.Equal(uri, Relationships.GetUriForImage(relId, drawing));
    }
}
