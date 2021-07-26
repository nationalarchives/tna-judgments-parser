
using System;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

static class Bookmarks {

    public static BookmarkStart Get(MainDocumentPart main, string name) {
        BookmarkStart bkmrk = main.Document.Descendants<BookmarkStart>().Where(b => b.Name == name).FirstOrDefault();
        if (bkmrk is not null)
            return bkmrk;
        throw new Exception();
        // WordprocessingDocument doc = (WordprocessingDocument) main.OpenXmlPackage;
        // foreach (var item in main.Parts) {
            
        // }
    }

}

}
