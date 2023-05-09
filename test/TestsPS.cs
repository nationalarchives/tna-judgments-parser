
using System.Collections.Generic;
using System.Linq;
using System.Xml.Xsl;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace UK.Gov.NationalArchives.CaseLaw {

public class TestsPS {

    private XslCompiledTransform Transform = new XslCompiledTransform();

    public static IEnumerable<object[]> indices = Enumerable.Range(1, 1)
        .Select(i => new object[] { i });

    private Tests main = new Tests();

    [Theory]
    [MemberData(nameof(indices))]
    public void Test(int i) {
        var docx = Tests.ReadDocx($"test.ps.test{i}.docx");
        var expected = Tests.ReadXml($"test.ps.test{i}.xml");
        var actual = Api.Parser.Parse(new Api.Request(){ Content = docx }).Xml;
        actual = main.RemoveSomeMetadata(actual);
        expected = main.RemoveSomeMetadata(expected);
        Assert.Equal(expected, actual);
    }

}

}
