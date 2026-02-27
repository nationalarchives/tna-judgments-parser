#nullable enable

using System.Collections.Generic;

namespace TRE.Metadata.MetadataFieldTypes;

public record CsvProperties(string Name, string Hash, Dictionary<string, string> FullLineContents);
