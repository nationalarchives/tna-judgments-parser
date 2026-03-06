#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using TRE.Metadata.Enums;

namespace TRE.Metadata;

public class MetadataFieldJsonConverter : JsonConverter<IMetadataField>
{
    public override IMetadataField Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var metadataFieldJsonElement = JsonElement.ParseValue(ref reader);
        var metaDataFieldName = metadataFieldJsonElement.GetProperty("name")
                                                        .Deserialize<MetadataFieldName>(options);

        IMetadataField deserializedMetadataField = metaDataFieldName switch
        {
            MetadataFieldName.CsvMetadataFileContents
                => metadataFieldJsonElement.Deserialize<MetadataField<Dictionary<string, string>>>(options)!,
            _ => throw new NotImplementedException()
        };

        return deserializedMetadataField;
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
