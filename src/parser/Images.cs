
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class WImage : IImage {

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
        return document.MainDocumentPart.ImageParts.Select(part => new WImage(part));
    }

}

public class WImageRef : IImageRef {

    private readonly Uri uri;

    public WImageRef(MainDocumentPart main, Drawing drawing) {
        StringValue relId = drawing.Inline.Graphic.GraphicData.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().First().Embed;
        OpenXmlPart part = main.Parts.Where(part => part.RelationshipId == relId.Value).First().OpenXmlPart;
        this.uri = part.Uri;
    }
    public WImageRef(MainDocumentPart main, Picture picture) {
        StringValue relId = picture.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().First().RelationshipId;
        OpenXmlPart part = main.Parts.Where(part => part.RelationshipId == relId.Value).First().OpenXmlPart;
        this.uri = part.Uri;
    }

    public string Src {
        get => Path.GetFileName(uri.ToString());
    }

}

}