using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace UK.Gov.NationalArchives.Imaging {

// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/4813e7fd-52d0-4f42-965f-228c8b7488d2
// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/33c95db8-eff2-44d3-b2ce-23d0ebdca5c2
internal class WMF {

    internal enum ImageType { BMP }

    private static ILogger logger = UK.Gov.Legislation.Judgments.Logging.Factory.CreateLogger<WMF>();

    internal static Tuple<ImageType, byte[]> Convert(byte[] wmf) {
        using MemoryStream ms = new MemoryStream(wmf);
        ms.Position = 0;

        byte[] firstFourBytes = new byte[4];
        ms.Read(firstFourBytes, 0, 4);
        UInt32 key = BitConverter.ToUInt32(firstFourBytes, 0);

        ms.Position = 0;
        using BinaryReader reader = new BinaryReader(ms);

        if (key == 0x9AC6CDD7) {
            logger.LogDebug("found placeable metafile header");
            MetaPlaceable placeable = new MetaPlaceable { Data = reader.ReadBytes(MetaPlaceable.Size) };
        }
        MetaHeader header = new MetaHeader { Data = reader.ReadBytes(MetaHeader.Size) };
        logger.LogDebug("wmf file of type {type}", header.Type);

        List<WmfBitmatRecord> records = new List<WmfBitmatRecord>(1);

        while (ms.Position < ms.Length) {
            byte[] beginning = reader.ReadBytes(6);
            UInt32 size = BitConverter.ToUInt32(beginning, 0);
            UInt16 type = BitConverter.ToUInt16(beginning, 4);
            byte[] rest = reader.ReadBytes((int) size * 2 - 6);
            logger.LogDebug("wmf record of type {type} ({size} bytes)", type.ToString("X"), size * 2);
            if (type == 0xF43) {    // META_STRETCHDIB
                MetaStretchDIB r = new MetaStretchDIB { Data = rest };
                records.Add(r);
                continue;
            }
            logger.LogWarning("unhandled WMF record type {type}", type.ToString("X"));
        }
        if (!records.Any()) {
            logger.LogWarning("no WMF bitmap records");
            return null;
        }
        if (records.Count != 1) {
            logger.LogWarning("more than one WMF bitmap record");
            return null;
        }
        try {
            return records.First().DIB.Convert();
        } catch (Exception e) {
            logger.LogError(e, "error converting bitmap WMF record");
            return null;
        }
    }

internal struct MetaPlaceable {

    internal static int Size => 22;

    internal byte[] Data;

}

internal struct MetaHeader {

    internal static int Size => 18;

    internal byte[] Data;

    internal UInt16 Type => BitConverter.ToUInt16(Data, 0);

    internal UInt16 HeaderSize => BitConverter.ToUInt16(Data, 2);

    internal UInt16 Version => BitConverter.ToUInt16(Data, 4);

    internal UInt16 SizeLow => BitConverter.ToUInt16(Data, 6);

    internal UInt16 SizeHigh => BitConverter.ToUInt16(Data, 8);

    internal UInt16 NumberOfObjects => BitConverter.ToUInt16(Data, 10);

    internal UInt32 MaxRecord => BitConverter.ToUInt32(Data, 12);

    internal UInt16 NumberOfMembers => BitConverter.ToUInt16(Data, 16);

}

internal interface WmfBitmatRecord {

    DeviceIndependentBitmap DIB { get; }

}

// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/7ebae08d-61ee-4d82-9aa5-9217ba2aa8c1
internal struct MetaStretchDIB : WmfBitmatRecord {

    // UInt32 Size;

    internal byte[] Data;

    UInt32 RasterOperation => BitConverter.ToUInt32(Data, 0);

    UInt16 ColorUsage => BitConverter.ToUInt16(Data, 4);

    Int16 SrcHeight => BitConverter.ToInt16(Data, 6);

    Int16 SrcWidth => BitConverter.ToInt16(Data, 8);

    Int16 YSrc => BitConverter.ToInt16(Data, 10);

    Int16 XSrc => BitConverter.ToInt16(Data, 12);

    Int16 DestHeight => BitConverter.ToInt16(Data, 14);

    Int16 DestWidth => BitConverter.ToInt16(Data, 16);

    Int16 yDst => BitConverter.ToInt16(Data, 18);

    Int16 xDst => BitConverter.ToInt16(Data, 20);

    public DeviceIndependentBitmap DIB => new DeviceIndependentBitmap { Offset = 22, Data = Data };

}

// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/7376542a-cce9-4625-8ead-585e9538f9f1
internal struct DeviceIndependentBitmap {

    internal int Offset;

    internal byte[] Data;

    private UInt32 HeaderSize => BitConverter.ToUInt32(Data, Offset);

    BitmapInfoHeader Header { get {
        if (HeaderSize == BitmapCoreHeader.Size)
            throw new Exception();
        else if (HeaderSize == BitmapInfoHeader.Size)
            return new BitmapInfoHeader { Data = Data, Offset = Offset };
        else
            throw new Exception();
    } }

    /* Colors (variable): An optional array of either RGBQuad Objects (section 2.2.2.20) or 16-bit unsigned integers that define a color table.
    The size and contents of this field SHOULD be determined from the metafile record or object that contains this DeviceIndependentBitmap Object and from information in the DIBHeaderInfo field. See ColorUsage Enumeration (section 2.1.1.6) and BitCount Enumeration (section 2.1.1.3) for additional details. */

    public Tuple<ImageType, byte[]> Convert() {
        if (Header.Compression == 0) {
            const int FileHeaderSize = 14;
            byte[] bmp = new byte[FileHeaderSize + Data.Length - Offset];
            bmp[0] = 0x42;
            bmp[1] = 0x4D;
            UInt32 size = System.Convert.ToUInt32(bmp.Length);
            Array.Copy(BitConverter.GetBytes(size), 0, bmp, 2, 4);
            UInt32 pixelDataOffset = FileHeaderSize + HeaderSize;
            if (Header.ColorUsed > 0 && Header.BitCount < 16) {
                pixelDataOffset += Header.ColorUsed * 2;
            }
            Array.Copy(BitConverter.GetBytes(pixelDataOffset), 0, bmp, 10, 4);
            Array.Copy(Data, Offset, bmp, FileHeaderSize, Data.Length - Offset);
            return new Tuple<ImageType, byte[]>(ImageType.BMP, bmp);
        }
        logger.LogWarning("unhandled compression type {0}", Header.Compression);
        return null;
    }

}

internal interface DIBHeaderInfo { }

internal struct BitmapCoreHeader : DIBHeaderInfo {

    internal static int Size = 0xC;

}

// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/567172fa-b8a2-4d79-86a2-5e21d6659ef3
internal struct BitmapInfoHeader : DIBHeaderInfo {

    internal static int Size = 40;

    internal int Offset;

    internal byte[] Data { get; init; }

    internal Int32 Width => BitConverter.ToInt32(Data, Offset + 4);

    internal Int32 Height => BitConverter.ToInt32(Data, Offset + 8);

    internal UInt16 Planes => BitConverter.ToUInt16(Data, Offset + 12);

    // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/792153f4-1e99-4ec8-93cf-d171a5f33903
    internal UInt16 BitCount => BitConverter.ToUInt16(Data, Offset + 14);

    // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/4e588f70-bd92-4a6f-b77f-35d0feaf7a57
    internal UInt32 Compression => BitConverter.ToUInt32(Data, Offset + 16);

    internal UInt32 ImageSize => BitConverter.ToUInt32(Data, Offset + 20);

    internal Int32 XPelsPerMeter => BitConverter.ToInt32(Data, Offset + 24);

    internal Int32 YPelsPerMeter => BitConverter.ToInt32(Data, Offset + 28);

    /* ColorUsed (4 bytes): A 32-bit unsigned integer that specifies the number of indexes in the color table used by the DIB, as follows:
        If this value is zero, the DIB uses the maximum number of colors that correspond to the BitCount value.
        If this value is nonzero and the BitCount value is less than 16, this value specifies the number of colors used by the DIB.
        If this value is nonzero and the BitCount value is 16 or greater, this value specifies the size of the color table used to optimize performance of the system palette. */
    internal UInt32 ColorUsed => BitConverter.ToUInt32(Data, Offset + 32);

    internal UInt32 ColorImportant => BitConverter.ToUInt32(Data, Offset + 36);

}
}

}
