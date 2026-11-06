#nullable enable

using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using NationalArchives.FindCaseLaw.Utils.Metadata;
using NationalArchives.FindCaseLaw.Utils.Metadata.Enums;

using TRE.FindCaseLaw;
using TRE.Metadata.Enums;

namespace TRE.Metadata;

public class MetadataFieldJsonConverter : JsonConverter<IMetadataField>
{
    public override IMetadataField Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var metadataFieldJsonElement = JsonElement.ParseValue(ref reader);
        try
        {
            var nameElement = metadataFieldJsonElement.GetProperty("name");
            MetadataFieldName metaDataFieldName;
            try
            {
                metaDataFieldName = nameElement.Deserialize<MetadataFieldName>(options);
            }
            catch (Exception)
            {
                throw new InvalidTreRequestException(
                    $"Unrecognised structured metadata field name '{nameElement.ToString()}'");
            }

            var metadataFieldType = typeof(MetadataField<>).MakeGenericType(metaDataFieldName.GetFieldValueType());

            var deserializedMetadataField =
                (IMetadataField)metadataFieldJsonElement.Deserialize(metadataFieldType, options)!;

            return deserializedMetadataField;
        }
        catch (Exception e) when (e is not InvalidTreRequestException)
        {
            throw new InvalidTreRequestException(
                $"Could not deserialize structured metadata field '{metadataFieldJsonElement.ToString()}'");
        }
    }

    public override void Write(Utf8JsonWriter writer, IMetadataField metadataField, JsonSerializerOptions options)
    {
        // Get a json converter for the concrete MetadataField<T> type
        var jsonConverter = options.GetConverter(metadataField.GetType());

        // Use reflection to make the retrieved converter write the json object
        jsonConverter.GetType()
                     .InvokeMember(
                         nameof(JsonConverter<IMetadataField>.Write),
                         BindingFlags.InvokeMethod,
                         null,
                         jsonConverter,
                         [writer, metadataField, options]);
    }
}
