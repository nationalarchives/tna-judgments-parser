
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace UK.Gov.Legislation {

public class Validator {

    private XmlSchemaSet Schemas = new XmlSchemaSet();

    public Validator() {
        var assembly = Assembly.GetExecutingAssembly();

        using Stream stream1 = assembly.GetManifestResourceStream("leg.subschema.xsd");
        using XmlReader reader1 = XmlReader.Create(stream1);
        Schemas.Add(Builder.ns, reader1);

        using Stream stream2 = assembly.GetManifestResourceStream("akn.xml.xsd");
        using XmlReader reader2 = XmlReader.Create(stream2);
        Schemas.Add("http://www.w3.org/XML/1998/namespace", reader2);
    }

    public List<ValidationEventArgs> Validate(XmlDocument akn) {
        XmlDocument copy = (XmlDocument) akn.CloneNode(true);
        copy.Schemas = Schemas;
        List<ValidationEventArgs> errors = new List<ValidationEventArgs>();
        copy.Validate((sender, e) => errors.Add(e));    // modifies the DOM
        return errors;
    }

}

}
