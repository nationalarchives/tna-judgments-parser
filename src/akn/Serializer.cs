
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public class Serializer {

    private static readonly ISet<string> blocks = new HashSet<string>{ "p", "block", "num", "heading", "tocItem" };

    private static readonly string ns = Builder.ns;

    public static void Serialize(XmlDocument doc, Stream stream) {
        new Serializer(stream).Serialize(doc);
    }

    private readonly XmlTextWriter writer;

    private Serializer(Stream stream) {
        writer = new XmlTextWriter(stream, new UTF8Encoding(false));
        writer.Formatting = Formatting.Indented;
    }

    private void Serialize(XmlDocument doc) {
        writer.WriteStartDocument();
        SerializeNodes(doc.ChildNodes);
        writer.WriteRaw("\n");
        writer.Close();
    }

    private void SerializeNodes(XmlNodeList nodes) {
        foreach (XmlNode node in nodes)
            SerializeNode(node);
    }

    private void SerializeNode(XmlNode node) {
        if (node is XmlElement e)
            SerializeElement(e);
        else if (node is XmlText text)
            writer.WriteString(text.Value);
    }

    private int blockDepth = 0;
    private Stack<int> DepthOfEmbeddedStructure = new();

    private void SerializeElement(XmlElement e) {
        writer.WriteStartElement(e.Prefix, e.LocalName, e.NamespaceURI);
        foreach (XmlAttribute attr in e.Attributes)
            writer.WriteAttributeString(attr.Prefix, attr.LocalName, attr.NamespaceURI, attr.Value);
        if (blocks.Contains(e.LocalName) && e.GetAttribute("name") != "embeddedStructure") {
            if (blockDepth == 0)
                writer.Formatting = Formatting.None;
            blockDepth += 1;
        }
        // if (e.LocalName == "embeddedStructure") {
        if (e.GetAttribute("name") == "embeddedStructure" || e.LocalName == "quotedStructure") {
            writer.Formatting = Formatting.Indented;
            DepthOfEmbeddedStructure.Push(blockDepth);
            blockDepth = 0;
        }
        SerializeNodes(e.ChildNodes);
        writer.WriteEndElement();
        if (blocks.Contains(e.LocalName) && e.GetAttribute("name") != "embeddedStructure") {
            blockDepth -= 1;
            if (blockDepth == 0)
                writer.Formatting = Formatting.Indented;
        }
        // if (e.LocalName == "embeddedStructure") {
        if (e.GetAttribute("name") == "embeddedStructure" || e.LocalName == "quotedStructure") {
            blockDepth = DepthOfEmbeddedStructure.Pop();
            if (blockDepth == 0)
                writer.Formatting = Formatting.Indented;
            else
                writer.Formatting = Formatting.None;
        }
    }

}

}
