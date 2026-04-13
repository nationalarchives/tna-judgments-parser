
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace UK.Gov.Legislation {

public class Validator {

    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

    /// <summary>
    /// Shared instance — the schema sets are expensive to construct (hundreds of KB
    /// of XSD compiled on first use) and the public methods are stateless once built.
    /// </summary>
    public static readonly Validator Shared = new();

    private readonly XmlSchemaSet EmSchemas = new();
    private readonly XmlSchemaSet IaSchemas = new();
    private readonly XmlSchemaSet EnSchemas = new();
    private readonly XmlSchemaSet MainAknSchemas = new();

    public Validator() {
        var assembly = Assembly.GetExecutingAssembly();

        // The W3C xml: namespace schema and the OASIS AKN 3.0 schema are vendor-neutral
        // resources embedded under the akn.* prefix and shared with the judgment product.
        // Load the W3C schema once and reference it from every set, rather than re-reading
        // it four times.
        XmlSchema xmlNs;
        using (Stream s = assembly.GetManifestResourceStream("akn.xml.xsd"))
        using (XmlReader r = XmlReader.Create(s)) {
            xmlNs = XmlSchema.Read(r, null);
        }

        void AddSubschema(XmlSchemaSet set, string resourceName) {
            using Stream s = assembly.GetManifestResourceStream(resourceName);
            using XmlReader r = XmlReader.Create(s);
            set.Add(Builder.ns, r);
            set.Add(xmlNs);
        }

        AddSubschema(EmSchemas, "leg.em-subschema.xsd");
        AddSubschema(EnSchemas, "leg.en-subschema.xsd");
        AddSubschema(IaSchemas, "leg.ia-subschema.xsd");

        // Full OASIS Akoma Ntoso 3.0 schema — ground truth for "is this valid AKN?".
        // The subschemas are internal narrowings layered on top; this is the interop check.
        using (Stream s = assembly.GetManifestResourceStream("akn.akomantoso30.xsd"))
        using (XmlReader r = XmlReader.Create(s)) {
            MainAknSchemas.Add(Builder.ns, r);
        }
        MainAknSchemas.Add(xmlNs);
    }

    /// <summary>
    /// Validate against the parser's tight subschema contract. Dispatches by doc/@name
    /// — TN/CoP/OD fall through to the EM subschema as a catch-all.
    /// </summary>
    public List<ValidationEventArgs> Validate(XmlDocument akn) {
        XmlNamespaceManager nsmgr = new XmlNamespaceManager(akn.NameTable);
        nsmgr.AddNamespace("akn", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");

        string docType = akn.SelectSingleNode("//akn:doc", nsmgr)?.Attributes?["name"]?.Value;
        XmlSchemaSet schemas = docType switch {
            "ExplanatoryNotes" => EnSchemas,
            "ImpactAssessment" => IaSchemas,
            _ => EmSchemas
        };

        return RunValidation(akn, schemas);
    }

    /// <summary>
    /// Validate against the full OASIS Akoma Ntoso 3.0 schema — the authoritative
    /// check for "is this valid AKN for downstream consumers?".
    /// </summary>
    public List<ValidationEventArgs> ValidateAgainstMainAkn(XmlDocument akn) {
        return RunValidation(akn, MainAknSchemas);
    }

    private static List<ValidationEventArgs> RunValidation(XmlDocument akn, XmlSchemaSet schemas) {
        XmlDocument copy = (XmlDocument) akn.CloneNode(true);
        copy.Schemas = schemas;

        List<ValidationEventArgs> errors = new();
        copy.Validate((sender, e) => errors.Add(e));
        return errors;
    }

}

}
