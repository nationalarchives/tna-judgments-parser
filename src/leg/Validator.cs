
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace UK.Gov.Legislation {

public class Validator {

    private XmlSchemaSet EmSchemas = new XmlSchemaSet();
    private XmlSchemaSet IaSchemas = new XmlSchemaSet();

    public Validator() {
        var assembly = Assembly.GetExecutingAssembly();

        // Load EM schemas
        using (Stream stream1 = assembly.GetManifestResourceStream("leg.em-subschema.xsd")) {
            using XmlReader reader1 = XmlReader.Create(stream1);
            EmSchemas.Add(Builder.ns, reader1);
        }

        using (Stream stream2 = assembly.GetManifestResourceStream("akn.xml.xsd")) {
            using XmlReader reader2 = XmlReader.Create(stream2);
            EmSchemas.Add("http://www.w3.org/XML/1998/namespace", reader2);
        }

        // Load IA schemas
        using (Stream stream3 = assembly.GetManifestResourceStream("leg.ia-subschema.xsd")) {
            using XmlReader reader3 = XmlReader.Create(stream3);
            IaSchemas.Add(Builder.ns, reader3);
        }

        using (Stream stream4 = assembly.GetManifestResourceStream("akn.xml.xsd")) {
            using XmlReader reader4 = XmlReader.Create(stream4);
            IaSchemas.Add("http://www.w3.org/XML/1998/namespace", reader4);
        }
    }

    public List<ValidationEventArgs> Validate(XmlDocument akn) {
        XmlDocument copy = (XmlDocument) akn.CloneNode(true);
        
        // Determine which schema to use based on document type
        XmlNamespaceManager nsmgr = new XmlNamespaceManager(akn.NameTable);
        nsmgr.AddNamespace("akn", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
        
        XmlNode docNode = akn.SelectSingleNode("//akn:doc", nsmgr);
        string docType = docNode?.Attributes?["name"]?.Value;
        
        if (docType == "ImpactAssessment") {
            copy.Schemas = IaSchemas;
        } else {
            copy.Schemas = EmSchemas; // Default to EM schemas for ExplanatoryMemorandum and PolicyNote
        }
        
        List<ValidationEventArgs> errors = new List<ValidationEventArgs>();
        copy.Validate((sender, e) => errors.Add(e));    // modifies the DOM
        return errors;
    }

}

}
