#nullable enable

using System.Linq;
using System.Reflection;
using System.Text.Json;

using NationalArchives.FindCaseLaw.Utils.Metadata;

using TRE.Metadata;

namespace TRE.FindCaseLaw;

public interface IStructuredMetadataConsolidator
{
    IMetadataField[] BuildMetadataFields(IMetadataField[] inputMetadata, IMetadataField[] metadataFoundInDocument);
}

public class StructuredMetadataConsolidator : IStructuredMetadataConsolidator
{
    public IMetadataField[] BuildMetadataFields(IMetadataField[] inputMetadata,
        IMetadataField[] metadataFoundInDocument)
    {
        var inputMetadataKeys = inputMetadata.Select(GetFieldKey).ToHashSet();

        var newFields = metadataFoundInDocument.Where(docField => !inputMetadataKeys.Contains(GetFieldKey(docField)));

        return [.. inputMetadata, .. newFields];
    }

    private static string GetFieldKey(IMetadataField field)
    {
        // We need to use reflection to get the value of the field because it is not accessible from the interface
        // We can't make it accessible from the interface because it is generic
        var fieldValue = field.GetType()
                              .InvokeMember(nameof(MetadataField<>.Value), BindingFlags.GetProperty, null, field, null);

        return $"{field.Source}:{field.Name}:{JsonSerializer.Serialize(fieldValue, ParserProcessMetadata.JsonSerializerOptions)}";
    }
}
