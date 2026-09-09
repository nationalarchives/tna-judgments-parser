using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using test.Mocks;

using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;
using Parser = UK.Gov.NationalArchives.Judgments.Api.Parser;

namespace test.ApiTests;

public class TestParser_Judgments
{
    private const int Total = 99;

    private static readonly IEnumerable<int> Indices = Enumerable.Range(1, 10).Concat(
        Enumerable.Range(12, 16).Concat(
            Enumerable.Range(29, Total - 29 + 1)));

    public static readonly TheoryData<int> IndicesTheoryData = new(Indices);

    private readonly Parser parser = new(new MockLogger<Parser>().Object, new Validator());

    [Theory]
    [MemberData(nameof(IndicesTheoryData))]
    public void Parse_WithNoMetadata_ReturnsExpectedXml(int i)
    {
        var docx = DocumentHelpers.ReadDocx(i);

        var actual = parser.Parse(new Api.Request { Content = docx }).Xml;
        var expected = DocumentHelpers.ReadXml(i);

        actual = DocumentHelpers.RemoveNonDeterministicMetadata(actual);
        expected = DocumentHelpers.RemoveNonDeterministicMetadata(expected);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(11, "order")]
    public void Parse_WithAttachment_ReturnsExpectedXml(int i, string name)
    {
        var main = DocumentHelpers.ReadDocx(i, "main");
        var attach = DocumentHelpers.ReadDocx(i, name);
        Api.AttachmentType type;
        switch (name)
        {
            case "order":
                type = Api.AttachmentType.Order;
                break;
            default:
                throw new Exception();
        }

        var attachments = new List<Api.Attachment>(1) { new() { Content = attach, Type = type } };

        var actual = parser.Parse(new Api.Request { Content = main, Attachments = attachments }).Xml;
        var expected = DocumentHelpers.ReadXml(i);

        actual = DocumentHelpers.RemoveNonDeterministicMetadata(actual);
        expected = DocumentHelpers.RemoveNonDeterministicMetadata(expected);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(28)]
    public void Parse_JudgmentWithImages_ReturnsImagesInResponse(int i)
    {
        var docx = DocumentHelpers.ReadDocx(i);

        var response = parser.Parse(new Api.Request { Content = docx });

        var actualXml = response.Xml;
        var expectedXml = DocumentHelpers.ReadXml(i);
        actualXml = DocumentHelpers.RemoveNonDeterministicMetadata(actualXml);
        expectedXml = DocumentHelpers.RemoveNonDeterministicMetadata(expectedXml);
        Assert.Equal(expectedXml, actualXml);
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var actual in response.Images)
        {
            using var stream = assembly.GetManifestResourceStream($"test.judgments.test{i}-{actual.Name}");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var expected = ms.ToArray();
            Assert.Equal(expected, actual.Content);
        }
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(84)]
    public void Parse_JudgmentWithKingOnTheApplicationOf_SuppressesPartiesOnReparseWithCaseName(int i)
    {
        var docx = DocumentHelpers.ReadDocx(i);

        var cleanRunResponse = parser.Parse(new Api.Request { Content = docx });

        var runWithExternalMetadataResponse = parser.Parse(new Api.Request
        {
            Content = docx,
            Meta = cleanRunResponse.Meta,
            Hint = Api.Hint.EWHC
        });

        var cleanRunResponseXml = DocumentHelpers.RemoveNonDeterministicMetadata(cleanRunResponse.Xml);
        var runWithExternalMetadataResponseXml =
            DocumentHelpers.RemoveNonDeterministicMetadata(runWithExternalMetadataResponse.Xml);

        Assert.Equal(cleanRunResponseXml, runWithExternalMetadataResponseXml);
    }
}
