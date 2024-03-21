
using System.Linq;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

static class Bookmarks {

    public static BookmarkStart Get(MainDocumentPart main, string name) {
        return main.Document.Descendants<BookmarkStart>().Where(b => b.Name == name).FirstOrDefault();
        // check other main.Parts ?
    }

}

}
