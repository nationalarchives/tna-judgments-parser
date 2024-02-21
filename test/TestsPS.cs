
using System.Collections.Generic;
using System.Linq;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace UK.Gov.NationalArchives.CaseLaw {

public class TestsPS {

    private static int N = 19;

    public static IEnumerable<object[]> indices = Enumerable.Range(1, N)
        .Select(i => new object[] { i });

    private Tests main = new Tests();

    [Theory]
    [MemberData(nameof(indices))]
    public void Test(int i) {
        var docx = ReadDocx(i);
        var expected = ReadXml(i);
        var actual = Api.Parser.Parse(new Api.Request { Content = docx }).Xml;
        actual = main.RemoveSomeMetadata(actual);
        expected = main.RemoveSomeMetadata(expected);
        Assert.Equal(expected, actual);
    }

    public static byte[] ReadDocx(int i) {
        return Tests.ReadDocx($"test.ps.test{i}.docx");
    }
    public static string ReadXml(int i) {
        return Tests.ReadXml($"test.ps.test{i}.xml");
    }

}

}
