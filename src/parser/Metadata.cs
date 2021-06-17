
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Judgments.Parse {

class WMetadata : IMetadata {

    private readonly MainDocumentPart main;
    private readonly Judgment judgment;

    internal WMetadata(MainDocumentPart main, Judgment judgment) {
        this.main = main;
        this.judgment = judgment;
    }

    public string DocumentId() {
        INeutralCitation cite = judgment.Header.OfType<ILine>().SelectMany(line => line.Contents).OfType<INeutralCitation>().FirstOrDefault();
        if (cite is null)
            return null;
        return cite.Text.Replace("[", "").Replace("]","").Replace("(", "").Replace(")","").Replace(" ","/").ToLower();
    }

    public string ComponentId() {
        return DocumentId();
    }

    public string Date() {
        IDocDate date = judgment.Header.OfType<ILine>().SelectMany(line => line.Contents).OfType<IDocDate>().FirstOrDefault();
        if (date is not null)
            return date.Date;
        return null;
    }

    public Dictionary<string, Dictionary<string, string>> CSSStyles() {
        return DOCX.CSS.Extract(main);
    }

}

}
