
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso;

public interface IValidator
{
    List<ValidationEventArgs> Validate(XmlDocument akn);
}

public class Validator : IValidator
{

    private XmlSchemaSet Schemas = new XmlSchemaSet();

    public Validator() {
        var assembly = Assembly.GetExecutingAssembly();
        using (Stream stream = assembly.GetManifestResourceStream("akn.akomantoso30.xsd")) {
            using (XmlReader reader = XmlReader.Create(stream)) {
                Schemas.Add(Builder.ns, reader);
            }
        }
        using (Stream stream = assembly.GetManifestResourceStream("akn.xml.xsd")) {
            using (XmlReader reader = XmlReader.Create(stream)) {
                Schemas.Add("http://www.w3.org/XML/1998/namespace", reader);
            }
        }
    }

    public List<ValidationEventArgs> Validate(XmlDocument akn) {
        XmlDocument copy = (XmlDocument) akn.CloneNode(true);
        copy.Schemas = Schemas;
        List<ValidationEventArgs> errors = new List<ValidationEventArgs>();
        copy.Validate((sender, e) => errors.Add(e));    // modifies the DOM
        return errors;
    }
}
