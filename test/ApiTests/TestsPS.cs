#nullable enable

using System.Collections.Generic;
using System.Linq;

using test.Mocks;

using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace test.ApiTests;

public class TestsPS
{
    private static readonly int N = 20;

    public static IEnumerable<object[]> indices = Enumerable.Range(1, N)
                                                            .Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(indices))]
    public void Test(int i)
    {
        var docx = DocumentHelpers.ReadDocx($"test.ps.test{i}.docx");
        var expected = DocumentHelpers.ReadXml($"test.ps.test{i}.xml");
        var parser = new Api.Parser(new MockLogger<Api.Parser>().Object, new Validator());

        var actual = parser.Parse(new Api.Request { Content = docx }).Xml;

        actual = DocumentHelpers.RemoveNonDeterministicMetadata(actual);
        expected = DocumentHelpers.RemoveNonDeterministicMetadata(expected);
        Assert.Equal(expected, actual);
    }
}
