using UK.Gov.NationalArchives.CaseLaw.Model;

namespace TRE.Metadata.MetadataFieldTypes;

public record Category : ICategory {

    public string Name { get; init; }

    public string Parent { get; init; }

}
