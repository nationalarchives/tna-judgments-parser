
using System;
using System.IO;

using SixLabors.ImageSharp;

namespace UK.Gov.NationalArchives.Imaging {

class Convert {

    internal static bool IsImage(byte[] source) {
        try {
            Image.Identify(source);
            return true;
        } catch (Exception) {
            return false;
        }
    }

    internal static byte[] ConvertToPng(byte[] source) {
        using var image = Image.Load(source);
        return ConvertToPng(image);
    }

    private static byte[] ConvertToPng(Image image) {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    internal static byte[] ConvertToGif(byte[] source) {
        using var image = Image.Load(source);
        using var stream = new MemoryStream();
        image.SaveAsGif(stream);
        return stream.ToArray();
    }

}

}
