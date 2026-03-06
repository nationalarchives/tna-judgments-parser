#nullable enable

using System;
using System.IO;
using System.Linq;

using test.Mocks;

using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;
using Parser = UK.Gov.NationalArchives.Judgments.Api.Parser;

namespace test;

public class UpdateXmlFiles
{
    private const int Total = 99;

    public static readonly TheoryData<int> IndicesTheoryData = new(
        Enumerable.Range(1, Total)
                  .Except([11]) //Skip 11 because it is a little more complicated
    );

    private readonly Parser parser = new(new MockLogger<Parser>().Object, new Validator());

    /// <summary>
    ///     This is not a test - it is a utility for test data
    ///     Updates test xml files in the judgments folder. Run this via command line with `-e UPDATE_XML="true"` when you need
    ///     to update the files en masse.
    /// </summary>
    [Theory]
    [MemberData(nameof(IndicesTheoryData))]
    public void UpdateJudgmentXmls(int i)
    {
        var successfulParse = bool.TryParse(Environment.GetEnvironmentVariable("UPDATE_XML"), out var shouldUpdateXml);
        Assert.SkipUnless(successfulParse && shouldUpdateXml,
            $"This is not a test. If you want to update the judgment test xml files then run: `dotnet test tna-judgments-parser.sln --filter {nameof(test)}.{nameof(UpdateXmlFiles)}.{nameof(UpdateJudgmentXmls)} -e UPDATE_XML=\"true\"`");

        var workingDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        var judgmentsDirectory = workingDirectory
                                 .Parent?
                                 .Parent?
                                 .Parent?
                                 .GetDirectories("judgments").SingleOrDefault()
                                 ?? throw new DirectoryNotFoundException("Could not find judgments directory");

        var file = judgmentsDirectory.GetFiles($"test{i}.xml").Single();

        var oldXml = File.ReadAllText(file.FullName);
        var newXml = parser.Parse(new Api.Request { Content = DocumentHelpers.ReadDocx(i) }).Xml;

        //Is it substantially different?
        if (string.Equals(DocumentHelpers.RemoveNonDeterministicMetadata(newXml),
                DocumentHelpers.RemoveNonDeterministicMetadata(oldXml), StringComparison.Ordinal))
        {
            Assert.Skip("No substantial changes detected, no updates made");
        }

        File.WriteAllText(file.FullName, newXml);
    }
}
