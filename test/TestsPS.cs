
using System.Collections.Generic;
using System.Linq;
using System.Xml.Xsl;

using Xunit;

using PS = UK.Gov.NationalArchives.CaseLaw.PressSummaries;

namespace UK.Gov.NationalArchives.CaseLaw {

public class TestsPS {

    private XslCompiledTransform Transform = new XslCompiledTransform();

    private static int N = 17;

    public static IEnumerable<object[]> indices = Enumerable.Range(1, N)
        .Select(i => new object[] { i });

    private Tests main = new Tests();

    [Theory]
    [MemberData(nameof(indices))]
    public void Test(int i) {
        var docx = Tests.ReadDocx($"test.ps.test{i}.docx");
        var expected = Tests.ReadXml($"test.ps.test{i}.xml");
        var actual = PS.Helper.Parse(docx);
        actual = main.RemoveSomeMetadata(actual);
        expected = main.RemoveSomeMetadata(expected);
        Assert.Equal(expected, actual);
    }

}

}
