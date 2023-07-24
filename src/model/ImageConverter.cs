
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.CaseLaw.PressSummaries;
using UK.Gov.NationalArchives.Imaging;
using Imaging = UK.Gov.NationalArchives.Imaging;

namespace UK.Gov.Legislation.Judgments {

class ImageConverter {

    private static ILogger logger = UK.Gov.Legislation.Judgments.Logging.Factory.CreateLogger<EMF>();

    internal static void ConvertImages(IJudgment jugdment) {
        Func<IEnumerable<IImage>> getter = () => jugdment.Images;
        IEnumerable<IImageRef> refs = Util.Descendants<IImageRef>(jugdment);
        Action<List<IImage>> setter = (List<IImage> images) => { jugdment.Images = images; };
        ConvertImages(getter, refs, setter);
    }

    internal static void ConvertImages(PressSummary ps) {
        Func<IEnumerable<IImage>> getter = () => ps.Images;
        IEnumerable<IImageRef> refs = Util.Descendants<IImageRef>(ps);
        Action<List<IImage>> setter = (List<IImage> images) => { ps.Images = images; };
        ConvertImages(getter, refs, setter);
    }

    internal static void ConvertImages(Func<IEnumerable<IImage>> getter, IEnumerable<IImageRef> refs, Action<List<IImage>> setter) {
        ConvertEmfFiles(getter(), refs, setter);
        ConvertTiffFiles(getter(), refs, setter);
        MutateImages(getter(), refs, setter);
    }

    private static void ConvertEmfFiles(IEnumerable<IImage> unconverted, IEnumerable<IImageRef> refs, Action<List<IImage>> setter) {
        List<IImage> images = new List<IImage>();
        foreach (IImage image in unconverted) {
            // logger.LogDebug("image {name}", image.Name);
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
                logger.LogDebug("{name} not converted", image.Name);
                images.Add(image);
                continue;
            }
            logger.LogInformation("{name} converted to {type}", image.Name, Enum.GetName(typeof(WMF.ImageType), converted.Item1));
            if (converted.Item1 == WMF.ImageType.BMP) {
                logger.LogInformation("converting .bmp to .png");
                byte[] png;
                try {
                    png = Imaging.Convert.ConvertToPng(converted.Item2);
                } catch (Exception e) {
                    logger.LogWarning("cannot further convert {0} from .bmp to .png: {1}", image.Name, e.Message);
                    images.Add(image);
                    continue;
                }
                string newName = image.Name + ".png";
                foreach (var iRef in refs.Where(r => r.Src == image.Name)) {
                    iRef.Src = newName;
                }
                var converted2 = new ConvertedImage { Name = newName, ContentType = "image/png", Data = png };
                images.Add(converted2);
                continue;
            }
            throw new System.Exception();
        }
        setter(images);
    }

    private static void ConvertTiffFiles(IEnumerable<IImage> unconverted, IEnumerable<IImageRef> refs, Action<List<IImage>> setter) {
        if (!unconverted.Any(image => image.ContentType == "image/tiff"))
            return;
        List<IImage> images = new List<IImage>();
        foreach (IImage image in images) {
            if (image.ContentType != "image/tiff") {
                images.Add(image);
                continue;
            }
            byte[] png = Imaging.Convert.ConvertToPng(image.Content());
            string newName = image.Name + ".png";
            foreach (var iRef in refs.Where(r => r.Src == image.Name)) {
                iRef.Src = newName;
            }
            var converted = new ConvertedImage { Name = newName, ContentType = "image/png", Data = png };
            images.Add(converted);
        }
        setter(images);
    }

    private static string MakeChangedSrc(string src, int n) {
        int i = src.LastIndexOf('.');
        if (i == -1)
            return src + ".change" + n;
        return src.Substring(0, i) + ".change" + n + src.Substring(i);
    }

    private static void MutateImages(IEnumerable<IImage> unconverted, IEnumerable<IImageRef> refs, Action<List<IImage>> setter) {
        List<IImage> images = new List<IImage>();
        foreach (IImage image in unconverted) {
            var allRefs = refs.Where(r => r.Src == image.Name);
            if (!allRefs.Any()) {
                logger.LogWarning("removing image {0}", image.Name);
                continue;
            }
            var unchangedRefs = allRefs.Where(r => r.Crop is null && r.Rotate is null);
            if (unchangedRefs.Any())
                images.Add(image);
            var changedRefs = allRefs.Where(r => r.Crop is not null || r.Rotate is not null);
            int n = 0;
            foreach (var changedRef in changedRefs) {
                n += 1;
                string changedSrc = MakeChangedSrc(changedRef.Src, n);
                changedRef.Src = changedSrc;
                byte[] data;
                if (changedRef.Crop is not null) {
                    logger.LogInformation("cropping {0} to ({1}, {2}, {3}, {4})", changedRef.Src, changedRef.Crop.Value.Top, changedRef.Crop.Value.Right, changedRef.Crop.Value.Bottom, changedRef.Crop.Value.Left);
                    data = Mutate.Crop(image.Content(), changedRef.Crop.Value);
                } else {
                    using var stream = new MemoryStream();
                    image.Content().CopyTo(stream);
                    data = stream.ToArray();
                }
                if (changedRef.Rotate.HasValue) {
                    logger.LogInformation("rotating {0} by {1}", changedRef.Src, changedRef.Rotate.Value);
                    data = Mutate.Rotate(data, changedRef.Rotate.Value);
                }
                var converted = new ConvertedImage {
                    Name = changedSrc,
                    ContentType = image.ContentType,
                    Data = data
                };
                images.Add(converted);
            }
        }
        setter(images);
    }

}

class ConvertedImage : UK.Gov.Legislation.Judgments.IImage {

    public string Name { get; internal init; }

    public string ContentType { get; internal init; }

    internal byte[] Data { get; init; }

    public Stream Content() => new MemoryStream(Data);

}

}
