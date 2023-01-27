
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;
using Hash = UK.Gov.Legislation.Judgments.AkomaNtoso.SHA256;

namespace UK.Gov.NationalArchives.CaseLaw {

public class TestContent {

    public static IEnumerable<object[]> indices = Tests.indices;

    [Theory]
    [MemberData(nameof(indices))]
    public void Test(int i) {
        var docx = Tests.ReadDocx(i);
        var actual = Api.Parser.Parse(new Api.Request(){ Content = docx }).Xml;
        var expected = Tests.ReadXml(i);
        actual = ExtractNormalizedContent(actual);
        expected = ExtractNormalizedContent(expected);
        Assert.Equal(expected, actual);
    }

    private string ExtractNormalizedContent(string xml) {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(xml);
        string content = Hash.RemoveMetadata(doc);
        return Regex.Replace(content, @"\s", "");
    }

}

}
