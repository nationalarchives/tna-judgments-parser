using System.Collections.Generic;
using System.IO;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.Imaging;

using Xunit;

namespace test;

/// <summary>
/// ImageConverter must never fail the whole document on an image it can't decode
/// (e.g. a vector EMF/WMF metafile carried as an embedded picture, which ImageSharp
/// has no decoder for). It should degrade: log and keep the image unmodified rather
/// than throwing. Regression guard for the UnknownImageFormatException crash.
/// </summary>
public class TestImageConverterRobustness
{
    private sealed class FakeImage : IImage
    {
        public string Name { get; init; }
        public string ContentType { get; init; }
        public byte[] Data { get; init; }
        public Stream Content() => new MemoryStream(Data);
        public byte[] Read() => Data;
    }

    private sealed class FakeRef : IImageRef
    {
        public string Src { get; set; }
        public string Style => null;
        public Inset? Crop { get; init; }
        public int? Rotate => null;
    }

    [Theory]
    [InlineData("garbage.dat")]   // unknown format, skipped by the EMF/WMF and TIFF passes
    [InlineData("garbage.wmf")]   // also exercises the EMF/WMF conversion pass
    public void UndecodableCroppedImageDegradesInsteadOfThrowing(string name)
    {
        var image = new FakeImage { Name = name, ContentType = "image/x-wmf", Data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 } };
        var imageRef = new FakeRef { Src = name, Crop = new Inset { Top = 0.1, Right = 0.1, Bottom = 0.1, Left = 0.1 } };

        List<IImage> images = new() { image };
        var refs = new List<IImageRef> { imageRef };

        var exception = Record.Exception(() =>
            ImageConverter.ConvertImages(() => images, refs, updated => images = updated));

        Assert.Null(exception);
    }
}
