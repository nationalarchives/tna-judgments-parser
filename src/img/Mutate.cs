
using System;
using System.IO;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace UK.Gov.NationalArchives.Imaging {

public struct Inset {
    internal double Top { get; init; }
    internal double Right { get; init; }
    internal double Bottom { get; init; }
    internal double Left { get; init; }
}

class Mutate {

    static Rectangle ToRect(Inset inset, int width, int height) {
        int x = (int) Math.Round(width * inset.Left);
        int y = (int) Math.Round(height * inset.Top);
        int w = width - (int) Math.Round(width * inset.Right) - x;
        int h = height - (int) Math.Round(height * inset.Bottom) - y;
        return new Rectangle(x, y, w, h);
    }

    internal static byte[] Crop(byte[] data, Inset inset) {
        IImageFormat format = Image.DetectFormat(data);
        using var image = Image.Load(data);
        Rectangle rect = ToRect(inset, image.Width, image.Height);
        image.Mutate(img => img.Crop(rect));
        return ToByteArray(image, format);
    }

    internal static byte[] Rotate(byte[] data, int angle) {
        IImageFormat format = Image.DetectFormat(data);
        using var image = Image.Load(data);
        image.Mutate(img => img.Rotate(angle));
        return ToByteArray(image, format);
    }

    private static byte[] ToByteArray(Image image, IImageFormat format) {
        using var stream = new MemoryStream();
        image.Save(stream, format);
        return stream.ToArray();
    }

}

}
