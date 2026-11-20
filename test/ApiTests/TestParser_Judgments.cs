using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.Extensions.Time.Testing;

using NationalArchives.FindCaseLaw.Utils.Metadata;
using NationalArchives.FindCaseLaw.Utils.Metadata.Enums;
using NationalArchives.FindCaseLaw.Utils.Metadata.MetadataFieldTypes;

using test.Mocks;

using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;
using Parser = UK.Gov.NationalArchives.Judgments.Api.Parser;

namespace test.ApiTests;

public class TestParser_Judgments
{
    internal const int Total = 106;

    private static readonly IEnumerable<int> Indices = Enumerable.Range(1, 10).Concat(
        Enumerable.Range(12, 16).Concat(
            Enumerable.Range(29, Total - 29 + 1)));

    public static readonly TheoryData<int> IndicesTheoryData = new(Indices);

    private static readonly DateTimeOffset Now = new(2024, 6, 1, 10, 30, 0, TimeSpan.Zero);

    private readonly Parser parser = new(new MockLogger<Parser>().Object, new Validator(), new FakeTimeProvider(Now));

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

    [Fact]
    public void Parse_JudgmentWithJudges_ReturnsADocumentJudgeFieldForEachJudge()
    {
        var docx = DocumentHelpers.ReadDocx(1);

        var fields = parser.Parse(new Api.Request { Content = docx })
                           .StructuredMetadataFields.Cast<MetadataField<Judge>>().ToArray();

        Assert.Equal(
            [
                new Judge("LORD JUSTICE SIMON BROWN", "judge-lord-justice-simon-brown"),
                new Judge("LORD JUSTICE DYSON", "judge-lord-justice-dyson"),
                new Judge("LORD JUSTICE SCOTT BAKER", "judge-lord-justice-scott-baker")
            ],
            fields.Select(field => field.Value));
        Assert.All(fields, field =>
        {
            Assert.Equal(MetadataFieldName.Judge, field.Name);
            Assert.Equal(MetadataSource.Document, field.Source);
            Assert.Equal(Now.UtcDateTime, field.Timestamp);
            Assert.NotEqual(Guid.Empty, field.Id);
        });
        Assert.Equal(3, fields.Select(field => field.Id).Distinct().Count());
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
