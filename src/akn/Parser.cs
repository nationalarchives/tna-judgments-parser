
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using AttachmentPair = System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>;
using AttachmentPair2 = System.Tuple<byte[], UK.Gov.Legislation.Judgments.AttachmentType>;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public class Parser {

    private static ILogger logger = Logging.Factory.CreateLogger<UK.Gov.Legislation.Judgments.AkomaNtoso.Parser>();

    internal static WordprocessingDocument Read(MemoryStream ms) {
        try {
            return WordprocessingDocument.Open(ms, false);
        } catch (OpenXmlPackageException) {
            ms.Seek(0, SeekOrigin.Begin);
            return WordprocessingDocument.Open(ms, true);
        }
    }
    internal static WordprocessingDocument Read(Stream docx) {
        MemoryStream ms = new MemoryStream();
        docx.CopyTo(ms);
        return Read(ms);
    }
    internal static WordprocessingDocument Read(byte[] docx) {
        MemoryStream ms = new MemoryStream(docx);
        return Read(ms);
    }

    internal static IEnumerable<AttachmentPair> ConvertAttachments(IEnumerable<AttachmentPair2> attachments) {
        return attachments.Select(a => new System.Tuple<WordprocessingDocument, AttachmentType>(Read(a.Item1), a.Item2));
    }

}

}
