#nullable enable

using System;
using System.IO;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Drawing = DocumentFormat.OpenXml.Drawing;
using DrawingWordProcessing = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DrawingPictures = DocumentFormat.OpenXml.Drawing.Pictures;

namespace test.Mocks;

public sealed class MockWordprocessingDocument : IDisposable
{
    private readonly WordprocessingDocument wordDocument;
    public MainDocumentPart MainDocumentPart { get; }
    public HeaderPart HeaderPart { get; }
    public FootnotesPart FootnotesPart { get; }
    public EndnotesPart EndnotesPart { get; }
    public Body Body { get; } = new();
    public Header Header { get; } = new();
    public Footnotes Footnotes { get; } = new();
    public Endnotes Endnotes { get; } = new();

    public MockWordprocessingDocument()
    {
        wordDocument = WordprocessingDocument.Create(new MemoryStream(), WordprocessingDocumentType.Document);

        MainDocumentPart = wordDocument.AddMainDocumentPart();
        MainDocumentPart.Document = new Document();
        MainDocumentPart.Document.AppendChild(Body);

        HeaderPart = MainDocumentPart.AddNewPart<HeaderPart>();
        HeaderPart.Header = Header;
        Header.AppendChild(new Paragraph(new Run(new Text("This is a header."))));
        Body.AppendChild(
            new SectionProperties(
                new HeaderReference
                {
                    Type = HeaderFooterValues.Default, Id = MainDocumentPart.GetIdOfPart(HeaderPart)
                }));


        EndnotesPart = MainDocumentPart.AddNewPart<EndnotesPart>();
        EndnotesPart.Endnotes = Endnotes;
        Endnotes.AppendChild(new Endnote(new Paragraph(new Run(new Text("This is endnote content.")))));
        Body.AppendChild(new Paragraph(new Run(new Text("Body text with an endnote")),
            new Run(new EndnoteReference())));

        FootnotesPart = MainDocumentPart.AddNewPart<FootnotesPart>();
        FootnotesPart.Footnotes = Footnotes;
        Footnotes.AppendChild(new Footnote(new Paragraph(new Run(new Text("This is footnote content.")))));
        Body.AppendChild(new Paragraph(new Run(new Text("Body text with a footnote")),
            new Run(new FootnoteReference())));
    }

    public void Dispose()
    {
        wordDocument.Dispose();
    }
}

public static class MockWordprocessingDocumentExtensions
{
    public static DocumentFormat.OpenXml.Wordprocessing.Drawing AddExternalImageToDocument(
        this OpenXmlCompositeElement element, OpenXmlPart part, Uri uri, out string relationshipId)
    {
        relationshipId =
            part.AddExternalRelationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/image",
                uri).Id;

        var drawingElement = AddDrawingForImage(relationshipId);

        var run = new Run();
        run.AppendChild(drawingElement);

        element.AppendChild(run);

        return drawingElement;
    }

    public static Hyperlink AddHyperlink(this OpenXmlCompositeElement element, OpenXmlPart part, Uri uri)
    {
        return element.AppendChild(new Hyperlink(new Run(new Text(uri.ToString())))
        {
            Id = part.AddHyperlinkRelationship(uri, true).Id
        });
    }


    public static DocumentFormat.OpenXml.Wordprocessing.Drawing AddImage<T>(this OpenXmlCompositeElement element,
        T part, out ImagePart imagePart, out string relationshipId)
        where T : OpenXmlPart, ISupportedRelationship<ImagePart>
    {
        imagePart = part.AddImagePart(ImagePartType.Jpeg);
        relationshipId = part.GetIdOfPart(imagePart);

        var drawingElement = AddDrawingForImage(relationshipId);

        var run = new Run();
        run.AppendChild(drawingElement);

        element.AppendChild(run);

        return drawingElement;
    }

    public static DocumentFormat.OpenXml.Wordprocessing.Drawing AddDrawingForImage(string relationshipId)
    {
        var inline = new DrawingWordProcessing.Inline(new DrawingWordProcessing.Extent { Cx = 990000L, Cy = 792000L },
            new DrawingWordProcessing.EffectExtent
            {
                LeftEdge = 0L,
                TopEdge = 0L,
                RightEdge = 0L,
                BottomEdge = 0L
            }, new DrawingWordProcessing.DocProperties { Id = (UInt32Value)1U, Name = "Picture 1" },
            new DrawingWordProcessing.NonVisualGraphicFrameDrawingProperties(
                new Drawing.GraphicFrameLocks { NoChangeAspect = true }), new Drawing.Graphic(
                new Drawing.GraphicData(
                    new DrawingPictures.Picture(
                        new DrawingPictures.NonVisualPictureProperties(
                            new DrawingPictures.NonVisualDrawingProperties
                            {
                                Id = (UInt32Value)0U, Name = "New Bitmap Image.jpg"
                            },
                            new DrawingPictures.NonVisualPictureDrawingProperties()
                        ),
                        new DrawingPictures.BlipFill(
                            new Drawing.Blip(
                                new Drawing.BlipExtensionList(
                                    new Drawing.BlipExtension
                                    {
                                        Uri =
                                            "{28A0092B-C50C-407E-A947-70E740481C1C}"
                                    }
                                )
                            )
                            {
                                Embed = relationshipId,
                                CompressionState =
                                    Drawing.BlipCompressionValues.Print
                            },
                            new Drawing.Stretch(
                                new Drawing.FillRectangle()
                            )
                        ),
                        new DrawingPictures.ShapeProperties(
                            new Drawing.Transform2D(
                                new Drawing.Offset { X = 0L, Y = 0L },
                                new Drawing.Extents { Cx = 990000L, Cy = 792000L }
                            ),
                            new Drawing.PresetGeometry(
                                new Drawing.AdjustValueList()
                            ) { Preset = Drawing.ShapeTypeValues.Rectangle }
                        )
                    )
                ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
            ))
        {
            DistanceFromTop = (UInt32Value)0U,
            DistanceFromBottom = (UInt32Value)0U,
            DistanceFromLeft = (UInt32Value)0U,
            DistanceFromRight = (UInt32Value)0U,
            EditId = "50D07946"
        };

        var drawingElement = new DocumentFormat.OpenXml.Wordprocessing.Drawing();
        drawingElement.AppendChild(inline);
        return drawingElement;
    }
}
