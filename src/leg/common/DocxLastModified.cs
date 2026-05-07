using System;
using System.IO;
using System.IO.Compression;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Reads the last-saved time of a .docx, working around a LibreOffice
/// quirk: when LibreOffice converts .doc → .docx via
/// `soffice --headless --convert-to docx`, the resulting package's
/// `_rels/.rels` lists the core-properties relationship under the URI
///   http://schemas.openxmlformats.org/officedocument/2006/relationships/metadata/core-properties
/// instead of the OPC-spec-required
///   http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties
/// Both `WordprocessingDocument.PackageProperties` and
/// `WordprocessingDocument.CoreFilePropertiesPart` discover the part
/// via that relationship — so for these LibreOffice-produced files
/// they silently return null even though `docProps/core.xml` contains
/// a perfectly good `dcterms:modified`.
///
/// We fall back to opening the docx as a plain ZIP and reading
/// `docProps/core.xml` directly. The package can't always be reached
/// from the live `WordprocessingDocument` instance (the SDK 3.x
/// `IPackageFeature.Package` accessor is internal), so callers stash
/// the source bytes via <see cref="WithDocxBytes"/> for the duration
/// of the parse.
/// </summary>
internal static class DocxLastModified {

    private const string DcTermsNs = "http://purl.org/dc/terms/";

    [ThreadStatic]
    private static byte[] _currentDocxBytes;

    /// <summary>
    /// Stash <paramref name="docxBytes"/> on the current thread for the
    /// lifetime of the returned scope. <see cref="Get"/> consults this
    /// when the SDK's PackageProperties read returns null.
    /// </summary>
    internal static IDisposable WithDocxBytes(byte[] docxBytes) {
        _currentDocxBytes = docxBytes;
        return new BytesScope();
    }

    private sealed class BytesScope : IDisposable {
        public void Dispose() => _currentDocxBytes = null;
    }

    internal static DateTime? Get(WordprocessingDocument doc) {
        DateTime? v = doc.PackageProperties.Modified;
        if (v.HasValue)
            return v;
        byte[] bytes = _currentDocxBytes;
        if (bytes is null)
            return null;
        return GetFromBytes(bytes);
    }

    private static DateTime? GetFromBytes(byte[] docxBytes) {
        using var ms = new MemoryStream(docxBytes, writable: false);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);
        var entry = zip.GetEntry("docProps/core.xml");
        if (entry is null)
            return null;
        using Stream s = entry.Open();
        return ReadModifiedFromCoreXml(s);
    }

    private static DateTime? ReadModifiedFromCoreXml(Stream stream) {
        var xml = new XmlDocument();
        try {
            xml.Load(stream);
        } catch (XmlException) {
            return null;
        }
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("dcterms", DcTermsNs);
        XmlNode node = xml.SelectSingleNode("//dcterms:modified", nsmgr);
        if (node is null)
            return null;
        if (DateTime.TryParse(node.InnerText, System.Globalization.CultureInfo.InvariantCulture,
                              System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                              out DateTime dt))
            return dt;
        return null;
    }

}

}
