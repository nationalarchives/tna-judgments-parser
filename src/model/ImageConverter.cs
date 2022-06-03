
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.Imaging;

namespace UK.Gov.Legislation.Judgments {

class ImageConverter {

    private static ILogger logger = UK.Gov.Legislation.Judgments.Logging.Factory.CreateLogger<EMF>();

    internal static void ConvertImages(IJudgment jugdment) {
        ConvertEmfFiles(jugdment);
        CropImages(jugdment);
    }

    private static void ConvertEmfFiles(IJudgment jugdment) {
        List<IImage> images = new List<IImage>();
        foreach (IImage image in jugdment.Images) {
            logger.LogDebug("image { name }", image.Name);
            if (!image.Name.EndsWith(".emf") && !image.Name.EndsWith(".wmf")) {
                images.Add(image);
                continue;
            }
            System.Tuple<WMF.ImageType, byte[]> converted = null;
            if (image.Name.EndsWith(".emf"))
                converted = EMF.Convert(image.Content());
            else if (image.Name.EndsWith(".wmf"))
                converted = WMF.Convert(image.Content());

            if (converted is null) {
                logger.LogDebug("{ name } not converted", image.Name);
                images.Add(image);
                continue;
            }
            logger.LogDebug("{ name } converted to { type }", image.Name, Enum.GetName(typeof(WMF.ImageType), converted.Item1));
            if (converted.Item1 == WMF.ImageType.BMP) {
                string newName = image.Name + ".bmp";
                foreach (var iRef in Util.Descendants<IImageRef>(jugdment).Where(r => r.Src == image.Name)) {
                    iRef.Src = newName;
                }
                var converted2 = new ConvertedImage { Name = newName, ContentType = "image/bmp", Data = converted.Item2 };
                images.Add(converted2);
                continue;
            }
            throw new System.Exception();
        }
        jugdment.Images = images;
    }

    private static string MakeCroppedSrc(string src, int n) {
        int i = src.LastIndexOf('.');
        if (i == -1)
            return src + ".crop" + n;
        return src.Substring(0, i) + ".crop" + n + src.Substring(i);
    }

    private static void CropImages(IJudgment jugdment) {
        List<IImage> images = new List<IImage>();
        foreach (IImage image in jugdment.Images) {
            var allRefs = Util.Descendants<IImageRef>(jugdment).Where(r => r.Src == image.Name);
            if (!allRefs.Any()) {
                logger.LogWarning("removing image { name }", image.Name);
                continue;
            }
            var uncroppedRefs = allRefs.Where(r => r.Crop is null);
            if (uncroppedRefs.Any())
                images.Add(image);
            var croppedRefs = allRefs.Where(r => r.Crop is not null);
            int n = 0;
            foreach (var croppedRef in croppedRefs) {
                n += 1;
                string croppedSrc = MakeCroppedSrc(croppedRef.Src, n);
                logger.LogInformation("cropping { src } to ({ top },{ right },{ bottom },{ left })", croppedSrc, croppedRef.Crop.Value.Top, croppedRef.Crop.Value.Right, croppedRef.Crop.Value.Bottom, croppedRef.Crop.Value.Left);
                croppedRef.Src = croppedSrc;
                var converted = new ConvertedImage {
                    Name = croppedSrc,
                    ContentType = image.ContentType,
                    Data = Mutate.Crop(image.Content(), croppedRef.Crop.Value)
                };
                images.Add(converted);
            }
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
