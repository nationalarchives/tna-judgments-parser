
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DrawingML = DocumentFormat.OpenXml.Drawing;

using Microsoft.Extensions.Logging;

using Imaging = UK.Gov.NationalArchives.Imaging;

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
        DocumentFormat.OpenXml.Drawing.Blip blip = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
        if (blip is null) {
            logger.LogWarning("unable to represent drawing");
            logger.LogWarning(drawing.OuterXml);
        } else {
            this.uri = DOCX.Relationships.GetUriForImage(blip.Embed, drawing);
        }

        // for alt text
        // var props = drawing.Descendants().OfType<DrawingML.Wordprocessing.DocProperties>().FirstOrDefault();
        // var name = props?.Name;
        // var desc = props?.Description;
        // StringValue relId = drawing.Inline.Graphic.GraphicData.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().First().Embed;
        // StringValue relId = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().First().Embed;
        // OpenXmlPart part = main.Parts.Where(part => part.RelationshipId == relId.Value).First().OpenXmlPart;
        // this.uri = part.Uri;
        DrawingML.Extents ext = drawing.Descendants().OfType<DrawingML.Extents>().FirstOrDefault();
        if (ext is not null) {
            List<string> styles = new List<string>(2);
            Int64Value cx = ext.Cx;
            if (cx.HasValue) {
                double widthInPoints = cx.Value / 12700d;
                styles.Add("width:" + CSS.ConvertSize(widthInPoints, "pt"));
            }
            Int64Value cy = ext.Cy;
            if (cy.HasValue) {
                double heightInPoints = cy.Value / 12700d;
                styles.Add("height:" + CSS.ConvertSize(heightInPoints, "pt"));
            }
            if (styles.Any())
                Style = string.Join(';', styles);
        }
        DrawingML.SourceRectangle srcRect = drawing.Descendants().OfType<DrawingML.SourceRectangle>()
            .Where(sr => sr.Top is not null || sr.Right is not null || sr.Bottom is not null || sr.Left is not null)
            .FirstOrDefault();
        if (srcRect is not null) {
            logger.LogInformation("found source rectangle for {0} ({1}, {2}, {3}, {4})",  this.uri, srcRect.Top, srcRect.Right, srcRect.Bottom, srcRect.Left);
            double top = CropValue(srcRect.Top);
            double right = CropValue(srcRect.Right);
            double bottom = CropValue(srcRect.Bottom);
            double left = CropValue(srcRect.Left);
            Crop = new Imaging.Inset { Top = top, Right = right, Bottom = bottom, Left = left };
        }
    }
    public static WImageRef Make(MainDocumentPart main, Picture picture) {
        DocumentFormat.OpenXml.Vml.ImageData imageData = picture.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().FirstOrDefault();
        if (imageData is null) {
            logger.LogWarning("skipping picutre because it has no 'image data'");
            return null;
        }
        StringValue relId = imageData.RelationshipId;
        if (relId is null) {
            logger.LogWarning("skipping picutre because its 'image data' has no relationship");
            return null;
        }
        return new WImageRef(main, picture);
    }
    private WImageRef(MainDocumentPart main, Picture picture) {
        var data = picture.Descendants<DocumentFormat.OpenXml.Vml.ImageData>();
        if (data.Skip(1).Any())
            logger.LogWarning("picutre contains more than one image data");
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

    private string _src;

    public string Src {
        get {
            if (_src is null && uri is not null)
                _src = Path.GetFileName(uri.ToString());
            return _src;
        }
        set {
            _src = value;
        }
    }

    public string Style { get; }

    public Imaging.Inset? Crop { get; private set; }

    private static double CropValue(Int32Value value) {
        if (value is null)
            return 0.0d;
        if (value < 0) {
            logger.LogWarning("negative crop value: {0}", value.Value);
            return 0.0d;
        }
        return value.Value / 100000d;
    }

}

class WExternalImage : IExternalImage {

    public string URL { get; init; }

}

}