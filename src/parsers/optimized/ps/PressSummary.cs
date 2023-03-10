
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class PressSummary : IAknDocument {

    public DocType Type { get => DocType.Summary; }

    public PSMetadata Metadata { get; init; }

    IAknMetadata IAknDocument.Metadata { get => Metadata; }

    public IEnumerable<IBlock> Body { get; init; }

    public IEnumerable<IImage> Images { get; init; }

    public static readonly int PerfectScore = PSMetadata.PerfectScore;

    public static int Score(PressSummary ps) {
        return ps.Metadata.Score();
    }

}

class PSResource : IResource {

    public ResourceType Type { get; init; }

    public string ID { get; init; }

    public string URI { get; init; }

    public string ShowAs { get; init; }

    public string ShortForm { get; init; }

}

class PSMetadata : IAknMetadata {

    private IResource TNA = new PSResource { ID = "tna", URI = "https://www.nationalarchives.gov.uk", ShowAs = "The National Archives" };
    private IResource UKSC = new PSResource { ID = "uksc", URI = "https://www.supremecourt.uk", ShowAs = "The Supreme Court" };

    public IResource Source { get => TNA; }

    public IResource Author { get => UKSC; }

    private string ShortUriComponent { get; init; }

    public string WorkURI { get => "https://caselaw.nationalarchives.gov.uk/id/" + ShortUriComponent; }

    public string ExpressionURI { get => "https://caselaw.nationalarchives.gov.uk/" + ShortUriComponent; }

    public INamedDate Date { get; private init; }

    public string Name { get; private init; }

    public string ProprietaryNamespace { get => "https://caselaw.nationalarchives.gov.uk/akn"; }

    public IList<Tuple<String, String>> Proprietary { get; private init; }

    public Dictionary<string, Dictionary<string, string>> CSSStyles { get; private init; }

    internal PSMetadata(MainDocumentPart main, IEnumerable<IBlock> contents) {
        Proprietary = new List<Tuple<String, String>>();
        WDocDate date = Util.Descendants<WDocDate>(contents).FirstOrDefault();
        if (date is not null) {
            Date = new WNamedDate { Name = "release", Date = date.Date };
        }
        WDocType docType = Util.Descendants<WDocType>(contents).FirstOrDefault();
        if (docType is not null) {
            Name = docType.Name();
        }
        string cite = Util.Descendants<WNeutralCitation>(contents).FirstOrDefault()?.Text;
        if (cite is null)
            cite = cite = Util.Descendants<WNeutralCitation2>(contents).FirstOrDefault()?.Text;
        if (cite is not null) {
            string normalized = Citations.Normalize(cite);
            ShortUriComponent = Citations.MakeUriComponent(normalized) + "/summary/1";
        }
        CSSStyles = DOCX.CSS.Extract(main, "#main");
    }

    internal static readonly int PerfectScore = 3;

    internal int Score() {
        int score = 0;
        if (Date is not null)
            score += 1;
        if (Name is not null)
            score += 1;
        if (ShortUriComponent is not null)
            score += 1;
        return score;
    }

}

}
