#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;

using TRE.Metadata;
using TRE.Metadata.Enums;
using TRE.Metadata.MetadataFieldTypes;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.Judgments.Api;

using Xunit;

using Party = UK.Gov.NationalArchives.CaseLaw.Model.Party;

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
                    new MetadataField<string>
                    {
                        Id = Guid.Parse("f57a16c6-820b-4db3-a26d-dea8678adef2"),
                        Name = MetadataFieldName.CaseNumber,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = "2025/IMTU/42"
                    },
                    new MetadataField<Category>
                    {
                        Id = Guid.Parse("3d2cc21b-ee13-4a9b-bdd2-d9d3b13ea362"),
                        Name = MetadataFieldName.Category,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = new Category { Name = "Parent Category" }
                    },
                    new MetadataField<Category>
                    {
                        Id = Guid.Parse("5cb9e06b-82c9-4535-bc0c-3a1aee8ae92e"),
                        Name = MetadataFieldName.Category,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = new Category { Name = "Child Category", Parent = "Parent Category" }
                    },
                    new MetadataField<string>
                    {
                        Id = Guid.Parse("8f81be24-3d52-4f8a-9b18-80e5fa05ddfe"),
                        Name = MetadataFieldName.Court,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = "IMTU"
                    },
                    new MetadataField<CsvProperties>
                    {
                        Id = Guid.Parse("8be4df61-93ca-11d2-aa0d-00e098032b8c"),
                        Name = MetadataFieldName.CsvMetadataFileProperties,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 007, DateTimeKind.Utc),
                        Value = new CsvProperties("With metadata fields.csv", "the-csv-contents-hash",
                            new Dictionary<string, string>
                            {
                                { "csv field name", "csv field value" }, { "second", "secondValue" }
                            })
                    },
                    new MetadataField<DateTime>
                    {
                        Id = Guid.Parse("5da9745d-79e6-42a6-a939-758898f23595"),
                        Name = MetadataFieldName.Date,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = new DateTime(2025, 12, 01, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new MetadataField<string>
                    {
                        Id = Guid.Parse("2b1d6385-2305-4f40-8480-16986259e831"),
                        Name = MetadataFieldName.HeadnoteSummary,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = "Summary of the judgment"
                    },
                    new MetadataField<string>
                    {
                        Id = Guid.Parse("8f9de628-7ee1-4fcd-a650-f8de41bbc3c1"),
                        Name = MetadataFieldName.Jurisdiction,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = "Some jurisdiction"
                    },
                    new MetadataField<string>
                    {
                        Id = Guid.Parse("a83f467e-b71f-4a24-bd4c-7a3108ffdd35"),
                        Name = MetadataFieldName.Name,
                        Source = MetadataSource.Document,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 456, DateTimeKind.Utc),
                        Value = "Aaron Aaronson v Some government department"
                    },
                    new MetadataField<string>
                    {
                        Id = Guid.Parse("7bc3c348-9884-4523-a3c9-d9f31bfc89b2"),
                        Name = MetadataFieldName.Ncn,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = "[2025] IMTU 42"
                    },
                    new MetadataField<Party>
                    {
                        Id = Guid.Parse("484d3253-414a-453a-8c21-bae3d7fc1666"),
                        Name = MetadataFieldName.Party,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = new Party { Name = "Aaron Aaronson", Role = PartyRole.Appellant }
                    },
                    new MetadataField<Party>
                    {
                        Id = Guid.Parse("1aebdbda-344e-4d77-806c-7ff1c24bfc94"),
                        Name = MetadataFieldName.Party,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = new Party { Name = "Some government department", Role = PartyRole.Respondent }
                    },
                    new MetadataField<string>
                    {
                        Id = Guid.Parse("687d0024-5d51-409d-8c10-22c608f65682"),
                        Name = MetadataFieldName.SourceFormat,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                    },
                    new MetadataField<Uri>
                    {
                        Id = Guid.Parse("e2280d85-3e28-4467-9351-402264663189"),
                        Name = MetadataFieldName.Uri,
                        Source = MetadataSource.Editor,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = new Uri("https://caselaw.nationalarchives.gov.uk/id/imtu/2025/42")
                    },
                    new MetadataField<string>
                    {
                        Id = Guid.Parse("9d6d8a6f-f73b-49f1-97df-4ffa5e047cf8"),
                        Name = MetadataFieldName.WebArchivingLink,
                        Source = MetadataSource.External,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, 100, DateTimeKind.Utc),
                        Value = "https://webarchive/a-link"
                    }
                ]
            },
            """{"documentType":"judgment","error-messages":[],"metadata_fields":[{"id":"f57a16c6-820b-4db3-a26d-dea8678adef2","name":"case_number","source":"external","timestamp":"2026-02-10T15:55:06.1Z","value":"2025/IMTU/42"},{"id":"3d2cc21b-ee13-4a9b-bdd2-d9d3b13ea362","name":"category","source":"external","timestamp":"2026-02-10T15:55:06.1Z","value":{"name":"Parent Category","parent":null}},{"id":"5cb9e06b-82c9-4535-bc0c-3a1aee8ae92e","name":"category","source":"external","timestamp":"2026-02-10T15:55:06.1Z","value":{"name":"Child Category","parent":"Parent Category"}},{"id":"8f81be24-3d52-4f8a-9b18-80e5fa05ddfe","name":"court","source":"external","timestamp":"2026-02-10T15:55:06.1Z","value":"IMTU"},{"id":"8be4df61-93ca-11d2-aa0d-00e098032b8c","name":"csv_metadata_file_properties","source":"external","timestamp":"2026-02-10T15:55:06.007Z","value":{"name":"With metadata fields.csv","hash":"the-csv-contents-hash","fullLineContents":{"csv field name":"csv field value","second":"secondValue"}}},{"id":"5da9745d-79e6-42a6-a939-758898f23595","name":"date","source":"external","timestamp":"2026-02-10T15:55:06.1Z","value":"2025-12-01T00:00:00Z"},{"id":"2b1d6385-2305-4f40-8480-16986259e831","name":"headnote_summary","source":"external","timestamp":"2026-02-10T15:55:06.1Z","value":"Summary of the judgment"},{"id":"8f9de628-7ee1-4fcd-a650-f8de41bbc3c1","name":"jurisdiction","source":"external","timestamp":"2026-02-10T15:55:06.1Z","value":"Some jurisdiction"},{"id":"a83f467e-b71f-4a24-bd4c-7a3108ffdd35","name":"name","source":"document","timestamp":"2026-02-10T15:55:06.456Z","value":"Aaron Aaronson v Some government department"},{"id":"7bc3c348-9884-4523-a3c9-d9f31bfc89b2","name":"ncn","source":"external","timestamp":"2026-02-10T15:55:06.1Z","value":"[2025] IMTU 42"},{"id":"484d3253-414a-453a-8c21-bae3d7fc1666","name":"party","source":"external","timestamp":"2026-02-10T15:55:06.1Z","value":{"name":"Aaron Aaronson","role":"appellant"}},{"id":"1aebdbda-344e-4d77-806c-7ff1c24bfc94","name":"party","source":"external","timestamp":"2026-02-10T15:55:06.1Z","value":{"name":"Some government department","role":"respondent"}},{"id":"687d0024-5d51-409d-8c10-22c608f65682","name":"source_format","source":"external","timestamp":"2026-02-10T15:55:06.1Z","value":"application/vnd.openxmlformats-officedocument.wordprocessingml.document"},{"id":"e2280d85-3e28-4467-9351-402264663189","name":"uri","source":"editor","timestamp":"2026-02-10T15:55:06.1Z","value":"https://caselaw.nationalarchives.gov.uk/id/imtu/2025/42"},{"id":"9d6d8a6f-f73b-49f1-97df-4ffa5e047cf8","name":"web_archiving_link","source":"external","timestamp":"2026-02-10T15:55:06.1Z","value":"https://webarchive/a-link"}],"primary_source":{"filename":"file.docx","mimetype":"some mimetype","route":"bulk","sha256":"definitely a SHA256"},"xml_contains_document_text":false,"uri":"a","court":"b","cite":"c","date":"d","name":"e","extensions":{"sourceFormat":null,"caseNumbers":null,"parties":null,"categories":null,"ncn":null,"webArchivingLink":null},"attachments":[]}"""),
        ("With snake cased enums", BasicParserProcessMetadata with
            {
                DocumentType = DocumentType.PressSummary,
                MetadataFields =
                [
                    new MetadataField<CsvProperties>
                    {
                        Id = Guid.Parse("8be4df61-93ca-11d2-aa0d-00e098032b8c"),
                        Name = MetadataFieldName.CsvMetadataFileProperties,
                        Source = MetadataSource.Document,
                        Timestamp = new DateTime(2026, 02, 10, 15, 55, 06, DateTimeKind.Utc),
                        Value = new CsvProperties("With snake cased enums.csv", "the-csv-contents-hash",
                            new Dictionary<string, string>
                            {
                                { "csv field name", "csv field value" }, { "second", "secondValue" }
                            })
                    }
                ]
            },
            """{"documentType":"press_summary","error-messages":[],"metadata_fields":[{"id":"8be4df61-93ca-11d2-aa0d-00e098032b8c","name":"csv_metadata_file_properties","source":"document","timestamp":"2026-02-10T15:55:06Z","value":{"name":"With snake cased enums.csv","hash":"the-csv-contents-hash","fullLineContents":{"csv field name":"csv field value","second":"secondValue"}}}],"primary_source":{"filename":"file.docx","mimetype":"some mimetype","route":"bulk","sha256":"definitely a SHA256"},"xml_contains_document_text":false,"uri":"a","court":"b","cite":"c","date":"d","name":"e","extensions":{"sourceFormat":null,"caseNumbers":null,"parties":null,"categories":null,"ncn":null,"webArchivingLink":null},"attachments":[]}""")
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
