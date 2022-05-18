
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace UK.Gov.NationalArchives.Imaging {

// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-emf/91c257d7-c39d-4a36-9b1f-63e3f73d30ca
// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-emf/e0137630-f3ad-492c-bde9-e68866e255ba
class EMF {

    private static ILogger logger = UK.Gov.Legislation.Judgments.Logging.Factory.CreateLogger<EMF>();

    internal static Tuple<WMF.ImageType, byte[]> Convert(Stream emf) {
        using MemoryStream ms = new MemoryStream();
        emf.CopyTo(ms);
        ms.Position = 0;
        using BinaryReader reader = new BinaryReader(ms);
        List<EmfBitmapRecord> records = new List<EmfBitmapRecord>(1);
        while (ms.Position < ms.Length) {
            const int RECORD_HEADER_BYTES = 8;
            byte[] header1 = reader.ReadBytes(RECORD_HEADER_BYTES);
            UInt32 emfPlusRecordType = BitConverter.ToUInt32(header1, 0);
            UInt32 length = BitConverter.ToUInt32(header1, 4);
            logger.LogDebug("emf record of type { type } ({ length } bytes)", emfPlusRecordType, length);
            byte[] data = reader.ReadBytes(((int) length) - RECORD_HEADER_BYTES);
            switch (emfPlusRecordType) {
                case 1: // EmfPlusRecordType.EmfHeader:
                case 14:    // EmfPlusRecordType.EmfEof
                case 16386: // EmfPlusRecordType.EndOfFile
                    continue;
                case 81:    // EmfPlusRecordType.EmfStretchDIBits
                    EmfStretchDIBits record = new EmfStretchDIBits { Data = data };
                    records.Add(record);
                    break;
                default:
                    logger.LogWarning("unhandled EMF record type { type }", emfPlusRecordType);
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
            return records.First().DIB.Convert();
        } catch (Exception e) {
            logger.LogError(e, "error converting bitmap EMF record");
            return null;
        }
    }

internal interface EmfBitmapRecord {

    WMF.DeviceIndependentBitmap DIB { get; }

}

// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-emf/89c0d808-0dea-413f-be40-2e9e51fa36ac
struct EmfStretchDIBits : EmfBitmapRecord {

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

    public WMF.DeviceIndependentBitmap DIB => new WMF.DeviceIndependentBitmap { Offset = 72, Data = Data };

}

}

}
