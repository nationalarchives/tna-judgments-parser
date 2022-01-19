
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

class Serializer {

    private static readonly ISet<string> blocks = new HashSet<string>{ "p", "block", "num", "heading", "tocItem" };

    private static readonly string ns = Builder.ns;

    public static void Serialize(XmlDocument doc, Stream stream) {
        new Serializer(stream).Serialize(doc);
    }

    private readonly XmlWriter writer;

    private Serializer(Stream stream) {
        XmlWriterSettings settings = new XmlWriterSettings();
        settings.OmitXmlDeclaration = false;
        settings.Indent = true;
        // settings.IndentChars = ("\t");
        settings.ConformanceLevel = ConformanceLevel.Document;
        writer = XmlWriter.Create(stream, settings);
    }

    private void Serialize(XmlDocument doc) {
        foreach (XmlNode child in doc.ChildNodes) {
            if (child.NodeType == XmlNodeType.ProcessingInstruction) {
                XmlProcessingInstruction pi = (XmlProcessingInstruction) child;
                writer.WriteProcessingInstruction(pi.Name, pi.Data);
            } else if (child.NodeType == XmlNodeType.Element) {
                XmlElement e = (XmlElement) child;
                SerializeElement(e);
            }
        }
        writer.WriteRaw("\n");
        writer.Close();
    }

    private void SerializeChildElements(XmlNode parent) {
        SerializeElements(parent.ChildNodes);
    }

    private void SerializeElements(XmlNodeList nodes) {
        foreach (XmlNode node in nodes) {
            if (node.NodeType != XmlNodeType.Element)
                continue;
            SerializeElement((XmlElement) node);
        }
    }

    private void SerializeElement(XmlElement e) {
        if (e.NamespaceURI == Metadata.ukns)
            writer.WriteStartElement("uk", e.LocalName, e.NamespaceURI);
        else
            writer.WriteStartElement(e.Prefix, e.LocalName, e.NamespaceURI);
        foreach (XmlAttribute attr in e.Attributes)
            writer.WriteAttributeString(attr.Prefix, attr.LocalName, attr.NamespaceURI, attr.Value);
        if (blocks.Contains(e.LocalName))
            SerializeFlat(e);
        else if (e.NamespaceURI != ns)
            e.WriteContentTo(writer);
        else
            SerializeChildElements(e);
        writer.WriteEndElement();
    }

    private void SerializeFlat(XmlElement e) {
        XmlWriterSettings settings = new XmlWriterSettings();
        settings.OmitXmlDeclaration = true;
        settings.Indent = false;
        settings.ConformanceLevel = ConformanceLevel.Fragment;
        StringBuilder builder = new StringBuilder();
        XmlWriter inline = XmlWriter.Create(builder, settings);
        foreach (XmlNode child in e.ChildNodes)
            Serialize2(child, inline);
        inline.Close();
        writer.WriteRaw(builder.ToString());
    }

    private static void Serialize2(XmlNode node, XmlWriter inline) {
        if (node is XmlElement e) {
            if (e.Name.Contains(':')) {
                int i = e.Name.IndexOf(':');
                string prefix = e.Name.Substring(0, i);
                string localName = e.Name.Substring(i + 1);
                inline.WriteStartElement(prefix, localName, node.GetNamespaceOfPrefix(prefix));
            } else {
                inline.WriteStartElement(e.Name);
            }
            foreach (XmlAttribute attr in e.Attributes) {
                if (attr.Name.Contains(':')) {
                    int i = attr.Name.IndexOf(':');
                    string prefix = attr.Name.Substring(0, i);
                    string localName = attr.Name.Substring(i + 1);
                    inline.WriteAttributeString(prefix, localName, attr.GetNamespaceOfPrefix(prefix), attr.Value);
                } else {
                    inline.WriteAttributeString(attr.Name, attr.Value);
                }
            }
            foreach (XmlNode child in e.ChildNodes)
                Serialize2(child, inline);
            inline.WriteEndElement();
        } else if (node is XmlText text) {
            inline.WriteString(text.Value);
        }
    }

}

}
