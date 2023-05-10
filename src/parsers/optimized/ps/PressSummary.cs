
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;
using UK.Gov.Legislation.Judgments.AkomaNtoso;
using System.Xml;

namespace UK.Gov.NationalArchives.CaseLaw {

class PressSummary : IAknDocument {

    public DocType Type { get => DocType.PressSummary; }

    private WordprocessingDocument Source { get; init; }

    public PSMetadata Metadata { get; internal init; }

    IAknMetadata IAknDocument.Metadata { get => Metadata; }

    public IEnumerable<IBlock> Body { get; internal init; }

    public IEnumerable<IImage> Images { get; set; } // setter required by ImageConverter

    public PressSummary(WordprocessingDocument source) {
        Source = source;
    }

    public static readonly int PerfectScore = PSMetadata.PerfectScore;

    public static int Score(PressSummary ps) {
        return ps.Metadata.Score();
    }

    public PSBundle Bundle() {
        return new PSBundle(Source, this);
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
    private IResource UKSC = new PSResource { ID = "uksc", URI = Courts.SupremeCourt.URL, ShowAs = Courts.SupremeCourt.LongName };
    private IResource UKPC = new PSResource { ID = "ukpc", URI = Courts.PrivyCouncil.URL, ShowAs = Courts.PrivyCouncil.LongName };

    public IResource Source { get => TNA; }

    public IResource Author { get; private init; }

    internal string ShortUriComponent { get; private init; }

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

            Match match = Regex.Match(normalized, @"\[(\d{4})\] UKSC \d");
            if (match.Success) {
                Author = UKSC;
                Proprietary.Add(new Tuple<string, string>("court", Courts.SupremeCourt.Code));
                Proprietary.Add(new Tuple<string, string>("year", match.Groups[1].Value));
            } else {
                match = Regex.Match(normalized, @"\[(\d{4})\] UKPC \d");
                if (match.Success) {
                    Author = UKPC;
                    Proprietary.Add(new Tuple<string, string>("court", Courts.PrivyCouncil.Code));
                    Proprietary.Add(new Tuple<string, string>("year", match.Groups[1].Value));
                }
            }
        }
        Proprietary.Add(new Tuple<string, string>("parser", Metadata.GetParserVersion()));
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
