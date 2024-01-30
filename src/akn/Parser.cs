
using System;
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

    // private static ILazyBundle Parse(Stream docx, Func<WordprocessingDocument, IJudgment> f) {
    //     MemoryStream ms = new MemoryStream();
    //     docx.CopyTo(ms);
    //     byte[] docx2 = ms.ToArray();
    //     MemoryStream stream2 = new MemoryStream();
    //     stream2.Write(docx2, 0, docx2.Length);
    //     WordprocessingDocument doc;
    //     try {
    //         doc = WordprocessingDocument.Open(stream2, false);
    //     } catch (OpenXmlPackageException) {
    //         stream2 = new MemoryStream();
    //         stream2.Write(docx2, 0, docx2.Length);
    //         var settings = new OpenSettings() {
    //             RelationshipErrorHandlerFactory = RelationshipErrorHandler.CreateRewriterFactory(DOCX.Relationships.MalformedUriRewriter)
    //         };
    //         doc = WordprocessingDocument.Open(stream2, true, settings);
    //     }
    //     IJudgment judgment = f(doc);
    //     return new Bundle(doc, judgment);
    // }

    private static ILazyBundle Parse2(Stream docx, IOutsideMetadata meta, Func<WordprocessingDocument, IOutsideMetadata, IJudgment> f) {
        WordprocessingDocument doc = Read(docx);
        // MemoryStream ms = new MemoryStream();
        // docx.CopyTo(ms);
        // byte[] docx2 = ms.ToArray();
        // MemoryStream stream2 = new MemoryStream();
        // stream2.Write(docx2, 0, docx2.Length);
        // WordprocessingDocument doc;
        // try {
        //     doc = WordprocessingDocument.Open(stream2, false);
        // } catch (OpenXmlPackageException) {
        //     stream2 = new MemoryStream();
        //     stream2.Write(docx2, 0, docx2.Length);
        //     var settings = new OpenSettings() {
        //         RelationshipErrorHandlerFactory = RelationshipErrorHandler.CreateRewriterFactory(DOCX.Relationships.MalformedUriRewriter)
        //     };
        //     doc = WordprocessingDocument.Open(stream2, true, settings);
        // }
        IJudgment judgment = f(doc, meta);
        return new Bundle(doc, judgment);
    }

    // internal static WordprocessingDocument Read(Stream stream) {
    //     MemoryStream ms = new MemoryStream();
    //     stream.CopyTo(ms);
    //     try {
    //         return WordprocessingDocument.Open(ms, false);
    //     } catch (OpenXmlPackageException) {
    //         ms.Seek(0, SeekOrigin.Begin);
    //         var settings = new OpenSettings() {
    //             RelationshipErrorHandlerFactory = RelationshipErrorHandler.CreateRewriterFactory(DOCX.Relationships.MalformedUriRewriter)
    //         };
    //         return WordprocessingDocument.Open(ms, true, settings);
    //     }
    // }
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

    private static ILazyBundle Parse3(WordprocessingDocument doc, IOutsideMetadata meta, IEnumerable<AttachmentPair> attachments, Func<WordprocessingDocument, IOutsideMetadata, IEnumerable<AttachmentPair>, IJudgment> f) {
        IJudgment judgment = f(doc, meta, attachments);
        return new Bundle(doc, judgment);
    }

    // internal static Func<Stream, ILazyBundle> MakeParser(Func<WordprocessingDocument, IJudgment> f) {
    //     return (Stream docx) => Parse(docx, f);
    // }

    internal static Func<Stream, IOutsideMetadata, ILazyBundle> MakeParser2(Func<WordprocessingDocument, IOutsideMetadata, IJudgment> f) {
        return (Stream docx, IOutsideMetadata meta) => Parse2(docx, meta, f);
    }

    // internal static Func<Stream, IOutsideMetadata, IEnumerable<Stream>, ILazyBundle> MakeParser3(Func<WordprocessingDocument, IOutsideMetadata, IEnumerable<WordprocessingDocument>, IJudgment> f) {
    //     return (Stream docx, IOutsideMetadata meta, IEnumerable<Stream> attachments) => {
    //         WordprocessingDocument doc = Read(docx);
    //         IEnumerable<WordprocessingDocument> attach2 = attachments.Select(Read);
    //         return Parse3(doc, meta, attach2, f);
    //     };
    // }
    internal static IEnumerable<AttachmentPair> ConvertAttachments(IEnumerable<AttachmentPair2> attachments) {
        return attachments.Select(a => new System.Tuple<WordprocessingDocument, AttachmentType>(Read(a.Item1), a.Item2));
    }

    internal static Func<byte[], IOutsideMetadata, IEnumerable<AttachmentPair2>, ILazyBundle> MakeParser4(Func<WordprocessingDocument, IOutsideMetadata, IEnumerable<AttachmentPair>, IJudgment> f) {
        return (byte[] docx, IOutsideMetadata meta, IEnumerable<AttachmentPair2> attachments) => {
            WordprocessingDocument doc = Read(docx);
            IEnumerable<AttachmentPair> attach2 = ConvertAttachments(attachments);
            return Parse3(doc, meta, attach2, f);
        };
    }

}

}
