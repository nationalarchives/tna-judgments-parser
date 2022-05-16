
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.Imaging;

namespace UK.Gov.Legislation.Judgments {

class ImageConverter {

    private static ILogger logger = UK.Gov.Legislation.Judgments.Logging.Factory.CreateLogger<EMF>();

    internal static void ConvertImages(IJudgment jugdment) {
        List<IImage> images = new List<IImage>();
        foreach (IImage image in jugdment.Images) {
            logger.LogDebug($"image { image.Name }");
            if (!image.Name.EndsWith(".emf") && !image.Name.EndsWith(".wmf")) {
                images.Add(image);
                continue;
            }
            Image converted = null;
            if (image.Name.EndsWith(".emf"))
                converted = EMF.Convert(image.Content());

            if (converted is null) {
                logger.LogDebug($"{ image.Name } not converted");
                images.Add(image);
                continue;
            }
            logger.LogDebug($"{ image.Name } converted to { converted.GetType().Name }");
            if (converted is Bitmap) {
                string newName = image.Name + ".bmp";
                foreach (var iRef in Util.Descendants<IImageRef>(jugdment).Where(r => r.Src == image.Name)) {
                    iRef.Src = newName;
                }
                MemoryStream ms = new MemoryStream();
                converted.Save(ms, ImageFormat.Bmp);
                var converted2 = new ConvertedImage { Name = newName, ContentType = "image/bmp", Data = ms.ToArray() };
                images.Add(converted2);
                continue;
            }
            throw new System.Exception();
        }
        jugdment.Images = images;
    }
}

class ConvertedImage : UK.Gov.Legislation.Judgments.IImage {

    public string Name { get; internal init; }

    public string ContentType { get; internal init; }

    internal byte[] Data { get; init; }

    public Stream Content() => new MemoryStream(Data);

}

}
