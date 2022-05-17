
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace UK.Gov.NationalArchives.Imaging {

class EMF {

    private static ILogger logger = UK.Gov.Legislation.Judgments.Logging.Factory.CreateLogger<EMF>();

    internal static Tuple<ImageType, byte[]> Convert(Stream emf) {
        using MemoryStream ms = new MemoryStream();
        emf.CopyTo(ms);
        ms.Position = 0;
        using BinaryReader reader = new BinaryReader(ms);
        List<EmfRecord> records = new List<EmfRecord>(1);
        while (ms.Position < ms.Length) {
            const int RECORD_HEADER_BYTES = 8;
            byte[] header1 = reader.ReadBytes(RECORD_HEADER_BYTES);
            EmfPlusRecordType type = (EmfPlusRecordType) BitConverter.ToUInt32(header1, 0);
            logger.LogDebug($"emf record of type { type }");
            UInt32 length = BitConverter.ToUInt32(header1, 4);
            byte[] data = reader.ReadBytes(((int) length) - RECORD_HEADER_BYTES);
            switch (type) {
                case EmfPlusRecordType.EmfHeader:
                case EmfPlusRecordType.EndOfFile:
                case EmfPlusRecordType.EmfEof:
                    continue;
                case EmfPlusRecordType.EmfStretchDIBits:
                    logger.LogInformation($"EMF record type { type }");
                    EmfStretchDIBits record = new EmfStretchDIBits { Data = data };
                    records.Add(record);
                    break;
                default:
                    logger.LogWarning($"unhandled EMF record type { type }");
                    break;
            }
        }
        if (!records.Any()) {
            logger.LogWarning($"no EMF bitmap records");
            return null;
        }
        if (records.Count != 1) {
            logger.LogWarning($"more than one EMF bitmap record");
            return null;
        }
        try {
            return records.First().Convert();
        } catch (Exception e) {
            logger.LogError("error converting bitmap EMF record");
            logger.LogError("{e}", e);
            return null;
        }
    }

internal enum ImageType { BMP }

interface EmfRecord {

    Tuple<ImageType, byte[]> Convert();

}

// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/4e588f70-bd92-4a6f-b77f-35d0feaf7a57
enum Compression {
    UncompressedRGB = 0
}

// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-emf/89c0d808-0dea-413f-be40-2e9e51fa36ac
struct EmfStretchDIBits : EmfRecord {

    internal byte[] Data { get; init; }

    // internal WMF.RectL Bounds { get {
    //     Int32 left = BitConverter.ToInt32(Data, 0);
    //     Int32 top = BitConverter.ToInt32(Data, 4);
    //     Int32 right = BitConverter.ToInt32(Data, 8);
    //     Int32 bottom = BitConverter.ToInt32(Data, 12);
    //     return new WMF.RectL { Left = left, Top = top, Right = right, Bottom = bottom };
    // } }

    internal Int32 xDest => BitConverter.ToInt32(Data, 16);

    internal Int32 yDest => BitConverter.ToInt32(Data, 20);

    internal Int32 xSrc => BitConverter.ToInt32(Data, 24);

    internal Int32 ySrc => BitConverter.ToInt32(Data, 28);

    internal Int32 cxSrc => BitConverter.ToInt32(Data, 32);

    internal Int32 cySrc => BitConverter.ToInt32(Data, 36);

    internal UInt32 offBmiSrc => BitConverter.ToUInt32(Data, 40);

    internal UInt32 cbBmiSrc => BitConverter.ToUInt32(Data, 44);

    internal UInt32 offBitsSrc => BitConverter.ToUInt32(Data, 48);

    internal UInt32 cbBitsSrc => BitConverter.ToUInt32(Data, 52);

    internal UInt32 usageSrc => BitConverter.ToUInt32(Data, 56);

    internal UInt32 bitBltRasterOperation => BitConverter.ToUInt32(Data, 60);  // TernaryRasterOperation Enumeration

    internal Int32 cxDest => BitConverter.ToInt32(Data, 64);

    internal Int32 cyDest => BitConverter.ToInt32(Data, 68);

    internal UInt32 HeaderSize => BitConverter.ToUInt32(Data, 72);

    internal BitmapInfoHeader Header { get {
        if (HeaderSize == 40)
            return new BitmapInfoHeader { Data = Data };
        throw new Exception();
    } }

    private byte[] _body;

    internal byte[] Body { get {
        if (_body is null) {
            int start = 72 + (int) HeaderSize;
            _body = new byte[Data.Length - start];
            Array.Copy(Data, start, _body, 0, _body.Length);
        }
        return _body;
    } }

    public Tuple<ImageType, byte[]> Convert() {
        const int EmfOffset = 72;
        if (Header.Compression == 0) {
            const int FileHeaderSize = 14;
            byte[] bmp = new byte[FileHeaderSize + Data.Length - EmfOffset];
            bmp[0] = 0x42;
            bmp[1] = 0x4D;
            UInt32 size = System.Convert.ToUInt32(bmp.Length);
            Array.Copy(BitConverter.GetBytes(size), 0, bmp, 2, 4);
            // bmp[6] = 0;
            // bmp[7] = 0;
            // bmp[8] = 0;
            // bmp[9] = 0;
            UInt32 pixelDataOffset = FileHeaderSize + HeaderSize;
            Array.Copy(BitConverter.GetBytes(pixelDataOffset), 0, bmp, 10, 4);
            Array.Copy(Data, EmfOffset, bmp, FileHeaderSize, Data.Length - EmfOffset);
            return new Tuple<ImageType, byte[]>(ImageType.BMP, bmp);
        }
        logger.LogWarning($"unhandled compression type { Header.Compression }");
        return null;
    }

}

struct BitmapInfoHeader {

    internal byte[] Data { get; init; }

    internal Int32 Width => BitConverter.ToInt32(Data, 76);

    internal Int32 Height => BitConverter.ToInt32(Data, 80);

    internal UInt16 Planes => BitConverter.ToUInt16(Data, 84);

    // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/792153f4-1e99-4ec8-93cf-d171a5f33903
    internal UInt16 BitCount => BitConverter.ToUInt16(Data, 86);

    // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/4e588f70-bd92-4a6f-b77f-35d0feaf7a57
    internal Compression Compression => (Compression) BitConverter.ToUInt32(Data, 88);

}

}

}
