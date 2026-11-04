#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;

using NationalArchives.FindCaseLaw.Utils.Metadata;
using NationalArchives.FindCaseLaw.Utils.Metadata.Enums;
using NationalArchives.FindCaseLaw.Utils.Metadata.MetadataFieldTypes;

using Shouldly;

using TRE.FindCaseLaw;
using TRE.Metadata;
using TRE.Metadata.MetadataFieldTypes;

using Xunit;

namespace test.TRE;

public class TestMetadataFieldJsonConverter
{
    private static readonly JsonSerializerOptions Options = ParserProcessMetadata.JsonSerializerOptions;

    private static readonly Guid CourtFieldId = Guid.Parse("6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10");

    private static readonly Guid JudgeFieldId = Guid.Parse("a41d7b52-8c3e-4f0a-b6d2-5e9c1f3a7d84");

    private static readonly DateTime FieldTimestamp = new(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly MetadataField<string> CourtField = new()
    {
        Id = CourtFieldId,
        Name = MetadataFieldName.Court,
        Source = MetadataSource.External,
        Timestamp = FieldTimestamp,
        Value = "EWCA"
    };

    private static readonly MetadataField<Judge> JudgeField = new()
    {
        Id = JudgeFieldId,
        Name = MetadataFieldName.Judge,
        Source = MetadataSource.External,
        Timestamp = FieldTimestamp,
        Value = new Judge("Judge Alpha", "judge-judge-alpha")
    };

    private const string CourtFieldJson =
        """{"id":"6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10","name":"court","source":"external","timestamp":"2024-05-01T00:00:00Z","value":"EWCA"}""";

    private const string JudgeFieldJson =
        """{"id":"a41d7b52-8c3e-4f0a-b6d2-5e9c1f3a7d84","name":"judge","source":"external","timestamp":"2024-05-01T00:00:00Z","value":{"name":"Judge Alpha","eId":"judge-judge-alpha"}}""";

    private static IMetadataField Read(string json)
    {
        return JsonSerializer.Deserialize<IMetadataField>(json, Options)!;
    }

    private static string Write(IMetadataField field)
    {
        return JsonSerializer.Serialize(field, Options);
    }

    [Fact]
    public void Read_CourtFieldJson_ReturnsMetadataFieldWithStringValue()
    {
        var field = Read(CourtFieldJson);

        field.ShouldBe(CourtField);
    }

    [Fact]
    public void Read_JudgeFieldJson_ReturnsMetadataFieldWithJudgeValue()
    {
        var field = Read(JudgeFieldJson);

        field.ShouldBe(JudgeField);
    }

    [Fact]
    public void Read_DateFieldJson_ReturnsMetadataFieldWithDateTimeValue()
    {
        const string json =
            """{"id":"6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10","name":"date","source":"external","timestamp":"2024-05-01T00:00:00Z","value":"2025-12-01T00:00:00Z"}""";

        var field = Read(json);

        field.ShouldBeOfType<MetadataField<DateTime>>().Value
             .ShouldBe(new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Read_CategoryFieldJson_ReturnsMetadataFieldWithCategoryValue()
    {
        const string json =
            """{"id":"6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10","name":"category","source":"external","timestamp":"2024-05-01T00:00:00Z","value":{"name":"Child Category","parent":"Parent Category"}}""";

        var field = Read(json);

        field.ShouldBeOfType<MetadataField<Category>>().Value
             .ShouldBe(new Category
             {
                 Name = "Child Category",
                 Parent = "Parent Category"
             });
    }

    [Fact]
    public void Read_CsvMetadataFilePropertiesFieldJson_ReturnsMetadataFieldWithCsvPropertiesValue()
    {
        const string json =
            """{"id":"6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10","name":"csv_metadata_file_properties","source":"external","timestamp":"2024-05-01T00:00:00Z","value":{"name":"file.csv","hash":"a-hash","fullLineContents":{"first":"1","second":"2"}}}""";

        var field = Read(json);

        var value = field.ShouldBeOfType<MetadataField<CsvProperties>>().Value;
        value.ShouldBeEquivalentTo(new CsvProperties("file.csv", "a-hash",
            new Dictionary<string, string>
            {
                ["first"] = "1",
                ["second"] = "2"
            }));
    }

    [Fact]
    public void Read_SnakeCaseName_ReturnsMatchingMetadataFieldName()
    {
        const string json =
            """{"id":"6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10","name":"headnote_summary","source":"external","timestamp":"2024-05-01T00:00:00Z","value":"A summary"}""";

        var field = Read(json);

        field.Name.ShouldBe(MetadataFieldName.HeadnoteSummary);
    }

    [Fact]
    public void Read_LowercaseSource_ReturnsMatchingMetadataSource()
    {
        const string json =
            """{"id":"6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10","name":"court","source":"document","timestamp":"2024-05-01T00:00:00Z","value":"EWCA"}""";

        var field = Read(json);

        field.Source.ShouldBe(MetadataSource.Document);
    }

    [Theory]
    [InlineData("Unrecognised structured metadata field name 'not_a_field'",
        """{"id":"6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10","name":"not_a_field","source":"external","timestamp":"2024-05-01T00:00:00Z","value":"x"}""")]
    [InlineData("""Could not deserialize structured metadata field '{"value":"x"}'""", """{"value":"x"}""")]
    [InlineData("""Could not deserialize structured metadata field '{"id":"6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10","name":"category","source":"external","timestamp":"2024-05-01T00:00:00Z","value":{"name":"Child Category","parent_field_has_typo":"Parent Category"}}'""",
        """{"id":"6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10","name":"category","source":"external","timestamp":"2024-05-01T00:00:00Z","value":{"name":"Child Category","parent_field_has_typo":"Parent Category"}}""")]
    public void Read_InvalidJson_ThrowsInvalidTreRequestException(string expectedMessage, string inputJson)
    {
        var act = () => Read(inputJson);

        act.ShouldThrow<InvalidTreRequestException>()
           .Message.ShouldBe(expectedMessage);
    }

    [Fact]
    public void Write_FieldWithStringValue_WritesValueAsJsonString()
    {
        var json = Write(CourtField);

        json.ShouldBe(CourtFieldJson);
    }

    [Fact]
    public void Write_FieldWithJudgeValue_WritesValueAsJsonObject()
    {
        var json = Write(JudgeField);

        json.ShouldBe(JudgeFieldJson);
    }

    [Fact]
    public void WriteThenRead_JudgeField_ReturnsEqualField()
    {
        var json = Write(JudgeField);

        var field = Read(json);

        field.ShouldBe(JudgeField);
    }

    [Fact]
    public void Write_ArrayOfFieldsWithDifferentValueTypes_WritesEachValueInItsOwnShape()
    {
        IMetadataField[] fields = [CourtField, JudgeField];

        var json = JsonSerializer.Serialize(fields, Options);

        json.ShouldBe($"[{CourtFieldJson},{JudgeFieldJson}]");
    }
}
