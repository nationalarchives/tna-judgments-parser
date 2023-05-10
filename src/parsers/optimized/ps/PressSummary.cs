
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

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

    internal string DocType { get; init; }

    public string ProprietaryNamespace { get => "https://caselaw.nationalarchives.gov.uk/akn"; }

    public IList<Tuple<String, String>> Proprietary { get; private init; }

    public Dictionary<string, Dictionary<string, string>> CSSStyles { get; private init; }

    private static ILogger logger = Logging.Factory.CreateLogger<PSMetadata>();

    internal PSMetadata(MainDocumentPart main, IEnumerable<IBlock> contents) {
        Proprietary = new List<Tuple<String, String>>();
        WDocDate date = Util.Descendants<WDocDate>(contents).FirstOrDefault();
        if (date is not null) {
            Date = new WNamedDate { Name = "release", Date = date.Date };
            logger.LogInformation("date is {0}", date.Date);
        } else {
            logger.LogWarning("date is null");
        }

        Name = makeName(contents);
        if (Name is null)
            logger.LogWarning("name is null");
        else
            logger.LogInformation("name is {0}", Name);

        WDocType1 docType = Util.Descendants<WDocType1>(contents).FirstOrDefault();
        if (docType is not null) {
            DocType = docType.Name();
            logger.LogInformation("doc type is {0}", DocType);
        } else {
            WDocType2 docType1 = Util.Descendants<WDocType2>(contents).FirstOrDefault();
            if (docType1 is not null) {
                DocType = docType1.Name();
                logger.LogInformation("doc type is {0}", DocType);
            } else {
                logger.LogWarning("doc type is null");
            }
        }

        string cite = Util.Descendants<WNeutralCitation>(contents).FirstOrDefault()?.Text;
        if (cite is null)
            cite = cite = Util.Descendants<WNeutralCitation2>(contents).FirstOrDefault()?.Text;
        if (cite is not null) {
            string normalized = Citations.Normalize(cite);
            string judgmentUriComponent = Citations.MakeUriComponent(normalized);
            ShortUriComponent = judgmentUriComponent + "/press-summary/1";
            logger.LogInformation("uri is {0}", ShortUriComponent);

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
        } else {
            logger.LogWarning("uri is null");
        }
        Proprietary.Add(new Tuple<string, string>("parser", Metadata.GetParserVersion()));
        CSSStyles = DOCX.CSS.Extract(main, "#main");
    }

    private static string makeName(IEnumerable<IBlock> contents) {
        var titles = Util.Descendants<IInline>(contents)
            .Where(i => i is WDocTitle || i is WDocTitle2)
            .Select(dt => {
                if (dt is WDocTitle dt1) return dt1.Text;
                if (dt is WDocTitle2 dt2) return IInline.ToString(dt2.Contents);
                throw new Exception();
            });
        if (!titles.Any())
            return null;
        var title = string.Join(" ", titles)
            .Replace("(Appellant)", "")
            .Replace("(Appellants)", "")
            .Replace("(Respondent)", "")
            .Replace("(Respondents)", "");
        title = Regex.Replace(title, @"\s+", " ").Trim();
        return "Press Summary of " + title;
    }

    internal static readonly int PerfectScore = 4;

    internal int Score() {
        int score = 0;
        if (Date is not null)
            score += 1;
        if (Name is not null)
            score += 1;
        if (DocType is not null)
            score += 1;
        if (ShortUriComponent is not null)
            score += 1;
        return score;
    }

}

}
