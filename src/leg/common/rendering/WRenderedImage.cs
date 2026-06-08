using System.IO;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.Imaging;

namespace UK.Gov.Legislation.Common.Rendering {

internal sealed class WRenderedImage : IImage {

    private readonly byte[] bytes;

    public string Name { get; }
    public string ContentType { get; }

    public WRenderedImage(string name, string contentType, byte[] bytes) {
        Name = name;
        ContentType = contentType;
        this.bytes = bytes;
    }

    public Stream Content() => new MemoryStream(bytes, writable: false);

    public byte[] Read() => (byte[]) bytes.Clone();

}

internal sealed class WRenderedImageRef : IImageRef {

    public string Src { get; set; }
    public string Style => null;
    public Inset? Crop => null;
    public int? Rotate => null;

    public WRenderedImageRef(string src) { Src = src; }

}

}
