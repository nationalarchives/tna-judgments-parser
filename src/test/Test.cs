
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Microsoft.Extensions.Logging;


class Tester {

    public struct Result {

        public List<ValidationEventArgs> SchemaErrors { get; set; }
        public bool HasCourtType { get; set; }
        // public string Court { get; set; }
        public bool HasNeutralCitation { get; set; }
        public string NeutralCitation { get; set; }
        public bool HasCaseNumber { get; set; }
        public bool HasDocumentID { get; set; }
        public string DocumentId { get; set; }
        public bool HasDate { get; set; }
        public bool HasTwoPartiesOrDocTitle { get; set; }
        public string CaseName { get; set; }
        public bool HasJudge { get; set; }

        public bool HasEverything() {
            if (this.SchemaErrors.Count > 0)
                return false;
            return this.HasCourtType && this.HasNeutralCitation && this.HasCaseNumber &&
                this.HasDocumentID && this.HasDate && this.HasTwoPartiesOrDocTitle;
        }
        public bool Level2ExceptCaseNumber() {
            if (this.SchemaErrors.Count > 0)
                return false;
            return this.HasCourtType && this.HasNeutralCitation &&
                this.HasDocumentID && this.HasDate && this.HasTwoPartiesOrDocTitle;
        }

    }

    private static ILogger logger = Logging.Factory.CreateLogger<Tester>();

    static XmlSchemaSet schema = new XmlSchemaSet();
    static Tester() {
        schema.Add(Builder.ns, "src/akn/akomantoso30.xsd");
        schema.Add("http://www.w3.org/XML/1998/namespace", "src/akn/xml.xsd");
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
        if (result.HasCourtType)
            logger.LogInformation("has court type");
        else
            logger.LogError("does not have court type");

        if (result.HasNeutralCitation)
            logger.LogInformation("has neutral citation");
        else
            logger.LogError("does not have neutral citation");

        XmlNodeList docketNumber = akn.SelectNodes("//akn:docketNumber", nsmgr);
        result.HasCaseNumber = docketNumber.Count > 0;
        if (result.HasCaseNumber)
            logger.LogInformation("has case number");
        else
            logger.LogWarning("does not have case number");

        XmlAttribute docId = (XmlAttribute) akn.SelectSingleNode("//akn:FRBRWork/akn:FRBRthis/@value", nsmgr);
        result.HasDocumentID = !string.IsNullOrEmpty(docId.Value);
        result.DocumentId = docId?.Value;
        if (result.HasDocumentID)
            logger.LogInformation("has document id");
        else
            logger.LogError("does not have document id");

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

        if (result.HasDocumentID)
            System.Console.WriteLine("✓ has document id");
        else
            System.Console.WriteLine("x does not have document id");

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
