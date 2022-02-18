
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public class Parser {

    private static ILogger logger = Logging.Factory.CreateLogger<UK.Gov.Legislation.Judgments.AkomaNtoso.Parser>();

    private static ILazyBundle Parse(Stream docx, Func<WordprocessingDocument, IJudgment> f) {
        MemoryStream ms = new MemoryStream();
        docx.CopyTo(ms);
        byte[] docx2 = ms.ToArray();
        MemoryStream stream2 = new MemoryStream();
        stream2.Write(docx2, 0, docx2.Length);
        WordprocessingDocument doc;
        try {
            doc = WordprocessingDocument.Open(stream2, false);
        } catch (OpenXmlPackageException) {
            stream2 = new MemoryStream();
            stream2.Write(docx2, 0, docx2.Length);
            var settings = new OpenSettings() {
                RelationshipErrorHandlerFactory = RelationshipErrorHandler.CreateRewriterFactory(DOCX.Relationships.MalformedUriRewriter)
            };
            doc = WordprocessingDocument.Open(stream2, true, settings);
        }
        IJudgment judgment = f(doc);
        return new Bundle(doc, judgment);
    }

    private static ILazyBundle Parse2(Stream docx, IOutsideMetadata meta, Func<WordprocessingDocument, IOutsideMetadata, IJudgment> f) {
        MemoryStream ms = new MemoryStream();
        docx.CopyTo(ms);
        byte[] docx2 = ms.ToArray();
        MemoryStream stream2 = new MemoryStream();
        stream2.Write(docx2, 0, docx2.Length);
        WordprocessingDocument doc;
        try {
            doc = WordprocessingDocument.Open(stream2, false);
        } catch (OpenXmlPackageException) {
            stream2 = new MemoryStream();
            stream2.Write(docx2, 0, docx2.Length);
            var settings = new OpenSettings() {
                RelationshipErrorHandlerFactory = RelationshipErrorHandler.CreateRewriterFactory(DOCX.Relationships.MalformedUriRewriter)
            };
            doc = WordprocessingDocument.Open(stream2, true, settings);
        }
        IJudgment judgment = f(doc, meta);
        return new Bundle(doc, judgment);
    }

    internal static WordprocessingDocument Read(Stream stream) {
        MemoryStream ms = new MemoryStream();
        stream.CopyTo(ms);
        try {
            return WordprocessingDocument.Open(ms, false);
        } catch (OpenXmlPackageException) {
            ms.Seek(0, SeekOrigin.Begin);
            var settings = new OpenSettings() {
                RelationshipErrorHandlerFactory = RelationshipErrorHandler.CreateRewriterFactory(DOCX.Relationships.MalformedUriRewriter)
            };
            return WordprocessingDocument.Open(ms, true, settings);
        }
    }
    internal static WordprocessingDocument Read(byte[] docx) {
        MemoryStream ms = new MemoryStream(docx);
        try {
            return WordprocessingDocument.Open(ms, false);
        } catch (OpenXmlPackageException) {
            ms.Seek(0, SeekOrigin.Begin);
            var settings = new OpenSettings() {
                RelationshipErrorHandlerFactory = RelationshipErrorHandler.CreateRewriterFactory(DOCX.Relationships.MalformedUriRewriter)
            };
            return WordprocessingDocument.Open(ms, true, settings);
        }
    }

    private static ILazyBundle Parse3(WordprocessingDocument doc, IOutsideMetadata meta, IEnumerable<WordprocessingDocument> attachments, Func<WordprocessingDocument, IOutsideMetadata, IEnumerable<WordprocessingDocument>, IJudgment> f) {
        IJudgment judgment = f(doc, meta, attachments);
        return new Bundle(doc, judgment);
    }

    internal static Func<Stream, ILazyBundle> MakeParser(Func<WordprocessingDocument, IJudgment> f) {
        return (Stream docx) => Parse(docx, f);
    }

    internal static Func<Stream, IOutsideMetadata, ILazyBundle> MakeParser2(Func<WordprocessingDocument, IOutsideMetadata, IJudgment> f) {
        return (Stream docx, IOutsideMetadata meta) => Parse2(docx, meta, f);
    }

    internal static Func<Stream, IOutsideMetadata, IEnumerable<Stream>, ILazyBundle> MakeParser3(Func<WordprocessingDocument, IOutsideMetadata, IEnumerable<WordprocessingDocument>, IJudgment> f) {
        return (Stream docx, IOutsideMetadata meta, IEnumerable<Stream> attachments) => {
            WordprocessingDocument doc = Read(docx);
            IEnumerable<WordprocessingDocument> attach2 = attachments.Select(Read);
            return Parse3(doc, meta, attach2, f);
        };
    }
    internal static Func<byte[], IOutsideMetadata, IEnumerable<byte[]>, ILazyBundle> MakeParser4(Func<WordprocessingDocument, IOutsideMetadata, IEnumerable<WordprocessingDocument>, IJudgment> f) {
        return (byte[] docx, IOutsideMetadata meta, IEnumerable<byte[]> attachments) => {
            WordprocessingDocument doc = Read(docx);
            IEnumerable<WordprocessingDocument> attach2 = attachments.Select(Read);
            return Parse3(doc, meta, attach2, f);
        };
    }

}

}
