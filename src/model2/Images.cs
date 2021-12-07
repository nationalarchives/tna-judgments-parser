
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DrawingML = DocumentFormat.OpenXml.Drawing;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.Parse {

class WImage : IImage {

    private static ILogger logger = Logging.Factory.CreateLogger<WImage>();

    private readonly ImagePart part;

    public string Name  {
        get => Path.GetFileName(part.Uri.ToString());
    }

    public string ContentType  {
        get => part.ContentType;
    }

    public Stream Content() => part.GetStream();

    private WImage(ImagePart part) {
        this.part = part;
    }

    public static IEnumerable<WImage> Get(WordprocessingDocument document) {
        return Enumerable.Concat(
            document.MainDocumentPart.HeaderParts.SelectMany(hp => hp.ImageParts),
            document.MainDocumentPart.ImageParts
        ).Select(part => new WImage(part));
    }

}

public class WImageRef : IImageRef {

    private static ILogger logger = Logging.Factory.CreateLogger<WImageRef>();

    private readonly Uri uri;

    public WImageRef(MainDocumentPart main, Drawing drawing) {
        StringValue relId = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().First().Embed;
        this.uri = DOCX.Relationships.GetUriForImage(relId, drawing);

        // StringValue relId = drawing.Inline.Graphic.GraphicData.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().First().Embed;
        // StringValue relId = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().First().Embed;
        // OpenXmlPart part = main.Parts.Where(part => part.RelationshipId == relId.Value).First().OpenXmlPart;
        // this.uri = part.Uri;
        DrawingML.Extents ext = drawing.Descendants().OfType<DrawingML.Extents>().FirstOrDefault();
        if (ext is not null) {
            string style = "";
            Int64Value cx = ext.Cx;
            if (cx.HasValue) {
                double widthInEMU = System.Convert.ToDouble(cx.Value);
                double widthInInches = widthInEMU / 914400d;
                style += "width:" + widthInInches + "in;";
            }
            Int64Value cy = ext.Cy;
            if (cy.HasValue) {
                double heightInEMU = System.Convert.ToDouble(cy.Value);
                double heightInInches = heightInEMU / 914400d;
                style += "height:" + heightInInches + "in;";
            }
            if (!string.IsNullOrEmpty(style))
                Style = style;
        }
    }
    public static WImageRef Make(MainDocumentPart main, Picture picture) {
        DocumentFormat.OpenXml.Vml.ImageData imageData = picture.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().FirstOrDefault();
        if (imageData is null) {
            logger.LogWarning("skipping picutre because it has no 'image data'");
            return null;
        }
        StringValue relId = picture.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().FirstOrDefault().RelationshipId;
        if (relId is null) {
            logger.LogWarning("skipping picutre because its 'image data' has no relationship");
            return null;
        }
        return new WImageRef(main, picture);
    }
    private WImageRef(MainDocumentPart main, Picture picture) {
        StringValue relId = picture.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().FirstOrDefault().RelationshipId;
        this.uri = DOCX.Relationships.GetUriForImage(relId, picture);
        string tempStyle = picture.Descendants<DocumentFormat.OpenXml.Vml.Shape>().First().Style?.Value;
        if (!string.IsNullOrEmpty(tempStyle)) {
            Dictionary<string, string> parsed = DOCX.CSS.ParseInline(tempStyle);
            Dictionary<string, string> filtered = parsed
                .Where(pair => pair.Key.Equals("width", StringComparison.OrdinalIgnoreCase) || pair.Key.Equals("height", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            tempStyle = DOCX.CSS.SerializeInline(filtered);
        }
        Style = tempStyle;
    }
    public WImageRef(MainDocumentPart main, DocumentFormat.OpenXml.Vml.Shape shape) {
        StringValue relId = shape.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().First().RelationshipId;
        // OpenXmlPart part = main.Parts.Where(part => part.RelationshipId == relId.Value).First().OpenXmlPart;
        // this.uri = part.Uri;
        this.uri = DOCX.Relationships.GetUriForImage(relId, shape);
        // Style = shape.Style?.Value;
        string tempStyle = shape.Style?.Value;
        if (!string.IsNullOrEmpty(tempStyle)) {
            Dictionary<string, string> parsed = DOCX.CSS.ParseInline(tempStyle);
            Dictionary<string, string> filtered = parsed
                .Where(pair => pair.Key.Equals("width", StringComparison.OrdinalIgnoreCase) || pair.Key.Equals("height", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            tempStyle = DOCX.CSS.SerializeInline(filtered);
        }
        Style = tempStyle;
    }

    public string Src {
        get => Path.GetFileName(uri.ToString());
    }

    public string Style { get; }

}

class WExternalImage : IExternalImage {

    public string URL { get; init; }

}

}