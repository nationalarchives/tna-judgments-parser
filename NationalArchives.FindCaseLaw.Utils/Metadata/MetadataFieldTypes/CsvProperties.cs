namespace NationalArchives.FindCaseLaw.Utils.Metadata.MetadataFieldTypes;

public record CsvProperties(string Name, string Hash, Dictionary<string, string> FullLineContents);
