using System.Text.Json.Serialization;

namespace TRE.Metadata.Enums;

public enum DocumentType
{
    Decision,
    Judgment,
    [JsonStringEnumMemberName("pressSummary")] PressSummary
}
