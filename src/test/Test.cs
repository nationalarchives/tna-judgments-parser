
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Microsoft.Extensions.Logging;
using System.Reflection;
using System.IO;

using Api = UK.Gov.NationalArchives.Judgments.Api;

class Tester {

    public struct Result {

        public List<ValidationEventArgs> SchemaErrors { get; set; }
        public bool HasCourtType { get; set; }
        public string Court { get; set; }
        public int? Year { get; set; }
        public int? CaseNumber { get; set; }
        public bool HasNeutralCitation { get; set; }
        public string NeutralCitation { get; set; }
        public bool HasCaseNumber { get; set; }
        public bool HasUri { get; set; }
        public string LongUri { get; set; }
        public string ShortUriComponent { get; set; }
        public bool HasDate { get; set; }
        public string DocumentDate { get; set; }
        public bool HasTwoPartiesOrDocTitle { get; set; }
        public string CaseName { get; set; }
        public bool HasJudge { get; set; }

        public bool HasEverything() {
            if (this.SchemaErrors.Count > 0)
                return false;
            return this.HasCourtType && this.HasNeutralCitation && this.HasCaseNumber &&
                this.HasUri && this.HasDate && this.HasTwoPartiesOrDocTitle;
        }
        public bool Level2ExceptCaseNumber() {
            if (this.SchemaErrors.Count > 0)
                return false;
            return this.HasCourtType && this.HasNeutralCitation &&
                this.HasUri && this.HasDate && this.HasTwoPartiesOrDocTitle;
        }

    }

    private static ILogger logger = Logging.Factory.CreateLogger<Tester>();

    static XmlSchemaSet schema = new XmlSchemaSet();
    static Tester() {
        var assembly = Assembly.GetExecutingAssembly();
        using (Stream stream = assembly.GetManifestResourceStream("akn.akomantoso30.xsd")) {
            using (XmlReader reader = XmlReader.Create(stream)) {
                schema.Add(Builder.ns, reader);
            }
        }
        using (Stream stream = assembly.GetManifestResourceStream("akn.xml.xsd")) {
            using (XmlReader reader = XmlReader.Create(stream)) {
                schema.Add("http://www.w3.org/XML/1998/namespace", reader);
            }
        }
    }

    public static List<ValidationEventArgs> Validate(XmlDocument akn) {
        akn.Schemas = schema;
        List<ValidationEventArgs> errors = new List<ValidationEventArgs>();
        akn.Validate((sender, e) => errors.Add(e));
        return errors;
    }

    public static Result Test(XmlDocument akn) {
        Result result = new Result();
        result.SchemaErrors = Validate(akn);
        if (result.SchemaErrors.Count == 0)
            logger.LogInformation("no AkN schema errors");
        else {
            logger.LogError(result.SchemaErrors.Count + " schema errors");
            logger.LogCritical(result.SchemaErrors[0].Exception.Message);
        }

        XmlNamespaceManager nsmgr = new XmlNamespaceManager(akn.NameTable);
        nsmgr.AddNamespace("akn", Builder.ns);
        nsmgr.AddNamespace("uk", Metadata.ukns);

        XmlNodeList neutralCitation = akn.SelectNodes("//akn:neutralCitation", nsmgr);
        result.HasNeutralCitation = neutralCitation.Count > 0;
        if (result.HasNeutralCitation)
            result.NeutralCitation = neutralCitation[0].InnerText;

        XmlNodeList courtType = akn.SelectNodes("//akn:courtType", nsmgr);
        result.HasCourtType = courtType.Count > 0;
        if (!result.HasCourtType && result.HasNeutralCitation) {
            Court? c2 = Courts.ExtractFromCitation(result.NeutralCitation);
            result.HasCourtType = c2 is not null;
        }
        XmlElement court = (XmlElement) akn.SelectSingleNode("//akn:meta/akn:proprietary/uk:court", nsmgr);
        result.Court = court?.InnerText;

        if (result.HasCourtType)
            logger.LogInformation("has court type: " + result.Court);
        else
            logger.LogError("does not have court type");

        if (result.HasNeutralCitation)
            logger.LogInformation("has neutral citation: " + result.NeutralCitation);
        else
            logger.LogError("does not have neutral citation");
        
        XmlElement year = (XmlElement) akn.SelectSingleNode("//akn:meta/akn:proprietary/uk:year", nsmgr);
        result.Year = (year is null) ? null : int.Parse(year.InnerText);
        XmlElement number = (XmlElement) akn.SelectSingleNode("//akn:meta/akn:proprietary/uk:number", nsmgr);
        result.CaseNumber = (number is null) ? null : int.Parse(number.InnerText);

        XmlNodeList docketNumber = akn.SelectNodes("//akn:docketNumber", nsmgr);
        result.HasCaseNumber = docketNumber.Count > 0;
        if (result.HasCaseNumber)
            logger.LogInformation("has case number");
        else
            logger.LogWarning("does not have case number");

        XmlAttribute workThis = (XmlAttribute) akn.SelectSingleNode("//akn:FRBRWork/akn:FRBRthis/@value", nsmgr);
        result.HasUri = !string.IsNullOrEmpty(workThis.Value);
        if (result.HasUri) {
            result.LongUri = workThis.Value;
            result.ShortUriComponent = Api.URI.ExtractShortURIComponent(result.LongUri);
        }
        if (result.HasUri)
            logger.LogInformation("has uri: " + result.LongUri);
        else
            logger.LogError("does not have uri");

        XmlNodeList dates = akn.SelectNodes("//akn:docDate", nsmgr);
        result.HasDate = dates.Count > 0;
        // if (!result.HasDate) {
        //     XmlAttribute frbrDate = (XmlAttribute) akn.SelectSingleNode("//akn:FRBRWork/akn:FRBRdate/@date", nsmgr);
        //     if (frbrDate is not null)
        //         result.HasDate = frbrDate.Value != UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata.DummyDate;
        // }
        if (dates.Count > 1)
            logger.LogWarning("has more than one document date");
        else if (result.HasDate)
            logger.LogInformation("has document date");
        else
            logger.LogError("does not have document date");
        result.DocumentDate = (dates.Count == 0) ? null : ((XmlElement) dates.Item(0)).GetAttribute("date");

        XmlNodeList parties = akn.SelectNodes("//akn:party", nsmgr);
        ISet<string> roles = new HashSet<string>();
        for (int i = 0; i < parties.Count; i++) {
            XmlElement party = (XmlElement) parties.Item(i);
            string role = party.GetAttribute("as");
            roles.Add(role);
        }
        XmlNodeList docTitle = akn.SelectNodes("//akn:docTitle", nsmgr);
        result.HasTwoPartiesOrDocTitle = roles.Count == 2 || docTitle.Count == 1;

        XmlElement docName = (XmlElement) akn.SelectSingleNode("//akn:FRBRWork/akn:FRBRname", nsmgr);

        if (!result.HasTwoPartiesOrDocTitle)
            result.HasTwoPartiesOrDocTitle = docName is not null;

        if (result.HasTwoPartiesOrDocTitle)
            logger.LogInformation("has two parties or doc title");
        else
            logger.LogError("does not have two parties or doc title");
        // if (docTitle.Count == 1) {
        //     logger.LogInformation("doc title is " + ((XmlElement) docTitle.Item(0)).InnerText);
        // }
        if (docTitle.Count > 1) {
            logger.LogWarning("has more than one docTitle");
        }

        if (docName is not null) {
            result.CaseName = docName.GetAttribute("value");
            logger.LogInformation("case name is " + result.CaseName);
        }

        XmlNodeList judges = akn.SelectNodes("//akn:judge", nsmgr);
        result.HasJudge = judges.Count > 0;
        if (result.HasJudge)
            logger.LogInformation("has at least one judge name");
        else
            logger.LogWarning("does not have at least one judge name");

        return result;
    }

    public static void Print(Result result) {
        if (result.SchemaErrors.Count == 0)
            System.Console.WriteLine("✓ no AkN schema errors");
        else {
            System.Console.WriteLine("x " + result.SchemaErrors.Count + " schema errors");
            System.Console.WriteLine(result.SchemaErrors[0].Exception.Message);
        }
        if (result.HasCourtType)
            System.Console.WriteLine("✓ has court type");
        else
            System.Console.WriteLine("x does not have court type");
        if (result.HasNeutralCitation)
            System.Console.WriteLine("✓ has neutral citation");
        else
            System.Console.WriteLine("x does not have neutral citation");
        if (result.HasCaseNumber)
            System.Console.WriteLine("✓ has case number");
        else
            System.Console.WriteLine("x does not have case number");

        if (result.HasUri)
            System.Console.WriteLine("✓ has document uri");
        else
            System.Console.WriteLine("x does not have uri");

        if (result.HasDate)
            System.Console.WriteLine("✓ has document date");
        else
            System.Console.WriteLine("x does not have document date");

        if (result.HasTwoPartiesOrDocTitle)
            System.Console.WriteLine("✓ has two parties or doc title");
        else
            System.Console.WriteLine("x does not have two parties or doc title");

        if (result.HasJudge)
            System.Console.WriteLine("✓ has at least one judge name");
        else
            System.Console.WriteLine("x does not have at least one judge name");

    }


}
