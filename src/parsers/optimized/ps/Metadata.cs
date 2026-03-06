
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

namespace UK.Gov.NationalArchives.CaseLaw.PressSummaries {

class Resource : IResource {

    public ResourceType Type { get; init; }

    public string ID { get; init; }

    public string URI { get; init; }

    public string ShowAs { get; init; }

    public string ShortForm { get; init; }

}

class Metadata : IAknMetadata {

    private IResource TNA = new Resource { ID = "tna", URI = "https://www.nationalarchives.gov.uk", ShowAs = "The National Archives" };
    private IResource UKSC = new Resource { ID = "uksc", URI = Courts.SupremeCourt.URL, ShowAs = Courts.SupremeCourt.Name };
    private IResource UKPC = new Resource { ID = "ukpc", URI = Courts.PrivyCouncil.URL, ShowAs = Courts.PrivyCouncil.Name };

    public IResource Source { get => TNA; }

    public IResource Author { get; private init; }

    internal string ShortUriComponent { get; private init; }

    public string WorkURI { get => "https://caselaw.nationalarchives.gov.uk/id/" + ShortUriComponent; }

    public string ExpressionURI { get => "https://caselaw.nationalarchives.gov.uk/" + ShortUriComponent; }

    public INamedDate Date { get; private init; }

    public string Name { get; private init; }

    internal string DocType { get; init; }

    public IList<IResource> References = new List<IResource>();
    IEnumerable<IResource> IAknMetadata.References { get => References; }

    public string ProprietaryNamespace { get => "https://caselaw.nationalarchives.gov.uk/akn"; }

    public IList<Tuple<String, String>> Proprietary { get; private init; }

    public Dictionary<string, Dictionary<string, string>> CSSStyles { get; private init; }

    private static ILogger logger = Logging.Factory.CreateLogger<Metadata>();

    internal Metadata(MainDocumentPart main, IEnumerable<IBlock> contents) {
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
            var judgmentUri = "https://caselaw.nationalarchives.gov.uk/id/" + judgmentUriComponent;
            Proprietary.Add(new Tuple<string, string>("summaryOf", judgmentUri));
            Proprietary.Add(new Tuple<string, string>("summaryOfCite", cite));
        } else {
            logger.LogWarning("uri is null");
        }
            Proprietary.Add(new Tuple<string, string>("parser", Legislation.Judgments.AkomaNtoso.Metadata.GetParserVersion()));
        CSSStyles = DOCX.CSS.Extract(main, "#main");

        foreach (IJudge judge in Util.Descendants<WJudge>(contents)) {
            Resource resource = new Resource {
                Type = ResourceType.Person,
                ID = judge.Id,
                ShowAs = judge.Name
            };
            References.Add(resource);
        }
    }

    private static string makeName(IEnumerable<IBlock> contents) {
        var title = Util.Descendants<IInline>(contents)
            .Where(i => i is WDocTitle || i is WDocTitle2)
            .Select(dt => {
                if (dt is WDocTitle dt1) return dt1.Text;
                if (dt is WDocTitle2 dt2) return IInline.ToString(dt2.Contents);
                throw new Exception();
            })
            .FirstOrDefault();
        if (title is null)
            return null;
        title = title
            .Replace("(Appellant)", "")
            .Replace("(Appellants)", "")
            .Replace("(Appellant and Cross-Respondent)", "")
            .Replace("(Respondent)", "")
            .Replace("(Respondents)", "")
            .Replace("(Respondent and Cross-Appellant)", "");
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

class CombinedMetadata : IAknMetadata {

    private Metadata InternalMetadata { get; init; }

    private IOutsideMetadata ExternalMetadata { get; init; }

    internal CombinedMetadata(Metadata intMeta, IOutsideMetadata extMeta) {
        InternalMetadata = intMeta;
        ExternalMetadata = extMeta;

        /* set ShortUriComponent */
        if (extMeta.ShortUriComponent is not null)
            ShortUriComponent = extMeta.ShortUriComponent;
        else if (extMeta.Cite is not null)
            ShortUriComponent = Citations.MakeUriComponent(extMeta.Cite) + "/press-summary/1";
        else
            ShortUriComponent = intMeta.ShortUriComponent;

        /* set Proprietary */
        string court = extMeta.Court?.Code ?? intMeta.Proprietary.Where(t => t.Item1 == "court").Select(t => t.Item2).FirstOrDefault();
        string year = ShortUriComponent is not null ? Citations.YearFromUriComponent(ShortUriComponent).ToString() : null;
        string summaryOfCite = extMeta.Cite ?? intMeta.Proprietary.Where(t => t.Item1 == "summaryOfCite").Select(t => t.Item2).FirstOrDefault();
        string summaryOf;
        if (extMeta.Cite is not null)
            summaryOf = "https://caselaw.nationalarchives.gov.uk/id/" + Citations.MakeUriComponent(extMeta.Cite);
        else if (extMeta.ShortUriComponent is not null)
            summaryOf = "https://caselaw.nationalarchives.gov.uk/id/" + extMeta.ShortUriComponent.Substring(0, extMeta.ShortUriComponent.IndexOf("/press-summary"));
        else
            summaryOf = intMeta.Proprietary.Where(t => t.Item1 == "summaryOf").Select(t => t.Item2).FirstOrDefault();
        Proprietary = new List<Tuple<String, String>>();
        if (court is not null)
            Proprietary.Add(new Tuple<string, string>("court", court));
        if (year is not null)
            Proprietary.Add(new Tuple<string, string>("year", year));
        if (summaryOf is not null)
            Proprietary.Add(new Tuple<string, string>("summaryOf", summaryOf));
        if (summaryOfCite is not null)
            Proprietary.Add(new Tuple<string, string>("summaryOfCite", summaryOfCite));
        Proprietary.Add(new Tuple<string, string>("parser", Legislation.Judgments.AkomaNtoso.Metadata.GetParserVersion()));
    }

    public IResource Source => InternalMetadata.Source;

    public IResource Author => InternalMetadata.Author;

    private string ShortUriComponent { get; init; }

    public string WorkURI => "https://caselaw.nationalarchives.gov.uk/id/" + ShortUriComponent;

    public string ExpressionURI => "https://caselaw.nationalarchives.gov.uk/" + ShortUriComponent;

    public INamedDate Date => ExternalMetadata.Date is not null
        ? new WNamedDate { Name = "release", Date = ExternalMetadata.Date }
        : InternalMetadata.Date;

    public string Name => ExternalMetadata.Name ?? InternalMetadata.Name;

    public IEnumerable<IResource> References => InternalMetadata.References;

    public string ProprietaryNamespace => InternalMetadata.ProprietaryNamespace;

    public IList<Tuple<string, string>> Proprietary { get; init; }

    public Dictionary<string, Dictionary<string, string>> CSSStyles => InternalMetadata.CSSStyles;

}

}
