#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;

using TRE.Metadata;
using TRE.Metadata.Enums;

using UK.Gov.NationalArchives.Judgments.Api;

using Xunit;

namespace test.TRE;

public class TestParserProcessMetadataSerialization
{
    private static readonly ParserProcessMetadata BasicParserProcessMetadata = new()
    {
        DocumentType = DocumentType.Judgment,
        ErrorMessages = [],
        MetadataFields = [],
        PrimarySource = new PrimarySourceFile
        {
            Filename = "file.docx",
            Mimetype = "some mimetype",
            Route = Route.Bulk,
            Sha256 = "definitely a SHA256"
        },
        XmlContainsDocumentText = false,
        Uri = "a",
        Court = "b",
        Cite = "c",
        Date = "d",
        Name = "e",
        Extensions = new Extensions
        {
            SourceFormat = null,
            CaseNumbers = null,
            Parties = null,
            Categories = null,
            NCN = null,
            WebArchivingLink = null
        },
        Attachments = []
    };

    public static (string TestCaseName, ParserProcessMetadata input, string json)[] TestCases =
    [
        ("Simple", BasicParserProcessMetadata,
            """{"documentType":"judgment","error-messages":[],"metadata_fields":[],"primary_source":{"filename":"file.docx","mimetype":"some mimetype","route":"bulk","sha256":"definitely a SHA256"},"xml_contains_document_text":false,"uri":"a","court":"b","cite":"c","date":"d","name":"e","extensions":{"sourceFormat":null,"caseNumbers":null,"parties":null,"categories":null,"ncn":null,"webArchivingLink":null},"attachments":[]}"""),
        ("With metadata fields", BasicParserProcessMetadata with
            {
                MetadataFields =
                [
                    new MetadataField<Dictionary<string, string>>
                    {
                        Id = Guid.Parse("8be4df61-93ca-11d2-aa0d-00e098032b8c"),
                        Name = MetadataFieldName.CsvMetadataFileContents,
                        Source = MetadataSource.Document,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, DateTimeKind.Utc),
                        Value = new Dictionary<string, string>
                        {
                            { "csv field name", "csv field value" }, { "second", "secondValue" }
                        }
                    }
                ]
            },
            """{"documentType":"judgment","error-messages":[],"metadata_fields":[{"id":"8be4df61-93ca-11d2-aa0d-00e098032b8c","name":"csv_metadata_file_contents","source":"document","timestamp":"2026-02-10T15:55:06Z","value":{"csv field name":"csv field value","second":"secondValue"}}],"primary_source":{"filename":"file.docx","mimetype":"some mimetype","route":"bulk","sha256":"definitely a SHA256"},"xml_contains_document_text":false,"uri":"a","court":"b","cite":"c","date":"d","name":"e","extensions":{"sourceFormat":null,"caseNumbers":null,"parties":null,"categories":null,"ncn":null,"webArchivingLink":null},"attachments":[]}"""),
        ("With snake cased enums", BasicParserProcessMetadata with
            {
                DocumentType = DocumentType.PressSummary,
                MetadataFields =
                [
                    new MetadataField<Dictionary<string, string>>
                    {
                        Id = Guid.Parse("8be4df61-93ca-11d2-aa0d-00e098032b8c"),
                        Name = MetadataFieldName.CsvMetadataFileContents,
                        Source = MetadataSource.Document,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, DateTimeKind.Utc),
                        Value = new Dictionary<string, string>
                        {
                            { "csv field name", "csv field value" }, { "second", "secondValue" }
                        }
                    }
                ]
            },
            """{"documentType":"press_summary","error-messages":[],"metadata_fields":[{"id":"8be4df61-93ca-11d2-aa0d-00e098032b8c","name":"csv_metadata_file_contents","source":"document","timestamp":"2026-02-10T15:55:06Z","value":{"csv field name":"csv field value","second":"secondValue"}}],"primary_source":{"filename":"file.docx","mimetype":"some mimetype","route":"bulk","sha256":"definitely a SHA256"},"xml_contains_document_text":false,"uri":"a","court":"b","cite":"c","date":"d","name":"e","extensions":{"sourceFormat":null,"caseNumbers":null,"parties":null,"categories":null,"ncn":null,"webArchivingLink":null},"attachments":[]}""")
    ];

    [Theory]
    [MemberData(nameof(TestCases))]
    public void CanSerialize(string _, ParserProcessMetadata deserializedObject, string json)
    {
        var result = JsonSerializer.Serialize(deserializedObject, ParserProcessMetadata.JsonSerializerOptions);
        Assert.Equal(json, result);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void CanDeserialize(string _, ParserProcessMetadata deserializedObject, string json)
    {
        var result =
            JsonSerializer.Deserialize<ParserProcessMetadata>(json, ParserProcessMetadata.JsonSerializerOptions);

        Assert.Equivalent(deserializedObject, result, true);
    }
}
