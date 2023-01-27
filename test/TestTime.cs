
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace UK.Gov.NationalArchives.CaseLaw {

public class Time {

    public static IEnumerable<object[]> indices = Tests.indices;

    [Theory]
    [MemberData(nameof(indices))]
    public void Test(int i) {
        var docx = Tests.ReadDocx(i);
        var actual = Api.Parser.Parse(new Api.Request(){ Content = docx }).Xml;
        Assert.NotEqual("", actual);
    }

}

}
