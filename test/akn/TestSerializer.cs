using System.Xml;

using Xunit;

using UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace test.akn;

public class TestSerializer
{
    [Fact]
    public void SerializeToString_WritesXmlDeclarationAndContent()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root><child>text</child></root>");

        var actual = Serializer.SerializeToString(doc);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", actual);
        Assert.Contains("<child>text</child>", actual);
    }
}
