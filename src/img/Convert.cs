
using System.IO;

using SixLabors.ImageSharp;

namespace UK.Gov.NationalArchives.Imaging {

class Convert {

    internal static byte[] ConvertToPng(byte[] source) {
        using var image = Image.Load(source);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

}

}
