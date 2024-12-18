
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DrawingML = DocumentFormat.OpenXml.Drawing;
using Vml = DocumentFormat.OpenXml.Vml;

using Microsoft.Extensions.Logging;

using Imaging = UK.Gov.NationalArchives.Imaging;
using Word2 = UK.Gov.NationalArchives.WordImpl;

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

    public byte[] Read() {
        using Stream content = Content();
        if (content is MemoryStream ms1)
            return ms1.ToArray();
        using MemoryStream ms2 = new MemoryStream();
        content.CopyTo(ms2);
        return ms2.ToArray();
    }

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
        bool isAbsolutelyPositioned = drawing.ChildElements.OfType<DrawingML.Wordprocessing.Anchor>().Any(); // types are WrapSquare, WrapTight, WrapThrough, WrapTopBottom and WrapNone
        if (isAbsolutelyPositioned)
            logger.LogWarning("image is absolutely positioned");
        DrawingML.Blip blip = drawing.Descendants<DrawingML.Blip>().FirstOrDefault();
        if (blip is null) {
            logger.LogWarning("unable to represent drawing");
            logger.LogWarning(drawing.OuterXml);
        } else {
            this.uri = DOCX.Relationships.GetUriForImage(blip.Embed, drawing);
        }

        /* alt text */
        // var props = drawing.Descendants().OfType<DrawingML.Wordprocessing.DocProperties>().FirstOrDefault();
        // see also DrawingML.Pictures.NonVisualDrawingProperties>().FirstOrDefault()?.Description;
        // var name = props?.Name;
        // var desc = props?.Description;
        // StringValue relId = drawing.Inline.Graphic.GraphicData.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().First().Embed;
        // StringValue relId = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().First().Embed;
        // OpenXmlPart part = main.Parts.Where(part => part.RelationshipId == relId.Value).First().OpenXmlPart;
        // this.uri = part.Uri;

        /* width and height */
        string width = null;
        string height = null;
        DrawingML.Extents ext = drawing.Descendants().OfType<DrawingML.Extents>().FirstOrDefault();
        if (ext is not null) {
            Int64Value cx = ext.Cx;
            if (cx.HasValue) {
                double widthInPoints = cx.Value / 12700d;
                width = CSS.ConvertSize(widthInPoints, "pt");
            }
            Int64Value cy = ext.Cy;
            if (cy.HasValue) {
                double heightInPoints = cy.Value / 12700d;
                height = CSS.ConvertSize(heightInPoints, "pt");
            }
        }

        /* rotatation */
        Rotate = GetRotation(drawing);
        if (Rotate == 90)
            (width, height) = (height, width);

        List<string> styles = new(2);
        if (width is not null)
            styles.Add("width:"+width);
        if (height is not null)
            styles.Add("height:"+height);
        if (styles.Count != 0)
            Style = string.Join(';', styles);

        /* crop */
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
        IEnumerable<Vml.ImageData> data = picture.Descendants<Vml.ImageData>();
        if (!data.Any()) {
            var drawing = picture.Descendants<Drawing>().FirstOrDefault();
            if (drawing is not null) {
                logger.LogWarning("drawing within picture");
                return new WImageRef(main, drawing);
            }
            if (picture.Descendants<Vml.TextBox>().Any()) {
                logger.LogCritical("skipping text box");
                Word2.WTextBox textBox = Word2.WTextBox.Extract(main, picture);
                if (textBox is null) {
                    logger.LogCritical("couldn't extract text from text box");
                    return null;
                }
                textBox.Lines.ForEach(line => logger.LogCritical("skipped text: {}", line.NormalizedContent));
                return null;
            }
            logger.LogWarning("skipping picture because it has no 'image data'");
            return null;
        }
        if (data.Skip(1).Any())
            logger.LogCritical("picture contains more than one image data");
        Vml.ImageData datum = data.Last();  // !?
        string relId = datum.RelationshipId?.Value;
        if (relId is null) {
            logger.LogWarning("skipping picture because its 'image data' has no relationship");
            return null;
        }
        if (datum.Parent is Vml.Shape shape && IsOffThePage(shape)) {  // see test 69
            logger.LogWarning("skipping picture because it is located off the page");
            return null;
        }
        return new WImageRef(main, picture, datum, relId);
    }

    private WImageRef(MainDocumentPart main, Picture picture, Vml.ImageData data, string relId) {
        this.uri = DOCX.Relationships.GetUriForImage(relId, picture);
        if (data.CropTop is not null || data.CropRight is not null || data.CropBottom is not null || data.CropLeft is not null) {   // e.g., ukut/aac/2022/207
            logger.LogInformation("found crop rectangle for {0} ({1}, {2}, {3}, {4})",  this.uri, data.CropTop, data.CropRight, data.CropBottom, data.CropLeft);
            double top = CropValue(data.CropTop);
            double right = CropValue(data.CropRight);
            double bottom = CropValue(data.CropBottom);
            double left = CropValue(data.CropLeft);
            Crop = new Imaging.Inset { Top = top, Right = right, Bottom = bottom, Left = left };
        }
        if (data.Parent is Vml.Shape shape) {
            Style = ExtractStyles(shape.Style?.Value);
            if (shape.Parent is Vml.Group group) {
                var groupStyle = ExtractStyles(group.Style?.Value);
                if (groupStyle is not null)  // [2023] EWHC 3056 (Ch)
                    Style = groupStyle;
            }
        }
        Rotate = GetRotation(picture);
    }

    public WImageRef(MainDocumentPart main, Vml.Shape shape) {
        StringValue relId = shape.Descendants<Vml.ImageData>().First().RelationshipId;
        this.uri = DOCX.Relationships.GetUriForImage(relId, shape);
        Style = ExtractStyles(shape.Style?.Value);
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

    private static string ExtractStyles(string style) {
        if (string.IsNullOrEmpty(style))
            return null;
        Dictionary<string, string> all = DOCX.CSS.ParseInline(style);
        Dictionary<string, string> filtered = all
            .Where(pair => pair.Key.Equals("width", StringComparison.OrdinalIgnoreCase) || pair.Key.Equals("height", StringComparison.OrdinalIgnoreCase))
            .Where(pair => !Regex.IsMatch(pair.Value, @"^\d+$"))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (!filtered.Any())
            return null;
        return DOCX.CSS.SerializeInline(filtered);
    }

    public Imaging.Inset? Crop { get; private set; }

    private static double CropValue(Int32Value value) {
        if (value?.Value is null)
            return 0.0d;
        if (value.Value < 0) {
            logger.LogWarning("negative crop value: {0}", value.Value);
            return 0.0d;
        }
        return value.Value / 100000d;
    }
    private static double CropValue(StringValue value) {
        if (value?.Value is null)
            return 0.0d;
        int v;
        try {
            v = Int32.Parse(value.Value.TrimEnd('f'));
        } catch (FormatException) {
            logger.LogError("error parsing crop value: {}", value.Value);
            return 0.0d;
        }
        if (v < 0) {
            logger.LogWarning("negative crop value: {}", value.Value);
            return 0.0d;
        }
        if (value.Value.EndsWith('f'))
            return v / 100000d * 1.5;   // this is just some reverse engineering
        return v / 100000d;
    }

    public int? Rotate { get; private set; }

    // see test 64
    private int? GetRotation(OpenXmlElement ancestor) {
        DrawingML.Transform2D xfrm = ancestor.Descendants().OfType<DrawingML.Transform2D>().FirstOrDefault();
        if (xfrm is null)
            return null;
        if (xfrm.Rotation is null)
            return null;
        if (!xfrm.Rotation.HasValue)
            return null;
        return xfrm.Rotation.Value / 60000;
    }

    internal static bool IsOffThePage(Vml.Shape shape) {
        string style = shape.Style?.Value;
        if (string.IsNullOrEmpty(style))
            return false;
        Dictionary<string, string> styles = DOCX.CSS.ParseInline(style);
        if (!styles.ContainsKey("position"))
            return false;
        string position = styles["position"];
        if (position != "absolute")
            return false;
        if (styles.ContainsKey("margin-left")) {
            string raw = styles["margin-left"];
            try {
                float inches = DOCX.CSS.ConvertToInches(raw);
                if (inches > 8.5f)
                    return true;
                if (inches < 0 && styles.ContainsKey("width")) {
                    float width = DOCX.CSS.ConvertToInches(styles["width"]);
                    if (width < Math.Abs(inches)) // [2021] UKUT 82 (TCC)
                        return true;
                if (inches < -8.5f) // [2021] UKUT 151 (TCC)
                    return true;
                }
            } catch {
                logger.LogWarning("can't convert to inches: {}", raw);
            }
        }
        return false;
    }

}

class WExternalImage : IExternalImage {

    public string URL { get; init; }

}

}