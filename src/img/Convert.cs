
using System.IO;

using SixLabors.ImageSharp;

namespace UK.Gov.NationalArchives.Imaging {

class Convert {

    internal static byte[] ConvertToPng(byte[] source) {
        using var image = Image.Load(source);
        return ConvertToPng(image);
    }

    internal static byte[] ConvertToPng(Stream source) {
        using var image = Image.Load(source);
        return ConvertToPng(image);
    }

    private static byte[] ConvertToPng(Image image) {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

}

}
