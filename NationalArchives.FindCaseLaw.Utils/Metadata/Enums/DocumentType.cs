using System.Text.Json.Serialization;

namespace NationalArchives.FindCaseLaw.Utils.Metadata.Enums;

public enum DocumentType
{
    Decision,
    Judgment,
    [JsonStringEnumMemberName("pressSummary")] PressSummary
}
