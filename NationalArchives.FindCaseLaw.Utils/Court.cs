using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace NationalArchives.FindCaseLaw.Utils;

public record struct TopLevelCourt
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("display_name")] public required string DisplayName { get; init; }
    [JsonPropertyName("is_tribunal")] public required bool IsTribunal { get; init; }
    [JsonPropertyName("courts")] public required Court[] Courts { get; init; }
}

public readonly record struct Court()
{
    [JsonPropertyName("code")] public required string Code { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("long_name")] public string? LongName { get; init; }
    [JsonPropertyName("link")] public string? Link { get; init; }
    [JsonPropertyName("ncn_pattern")] public string? NcnPattern { get; init; }
    [JsonPropertyName("ncn_examples")] public string[] NcnExamples { get; init; } = [];
    [JsonPropertyName("param")] public string? Param { get; init; }
    [JsonPropertyName("start_year")] public int? StartYear { get; init; }
    [JsonPropertyName("selectable")] public bool? Selectable { get; init; }
    [JsonPropertyName("listable")] public bool? Listable { get; init; }
    [JsonPropertyName("identifier_iri")] public required string IdentifierIri { get; init; }
    [JsonPropertyName("grouped_name")] public string? GroupedName { get; init; }
    [JsonPropertyName("extra_params")] public string[] ExtraParams { get; init; } = [];
    [JsonPropertyName("end_year")] public int? EndYear { get; init; }
    [JsonPropertyName("jurisdictions")] public Jurisdictions[] Jurisdictions { get; init; } = [];
}

public record struct Jurisdictions
{
    [JsonPropertyName("code")] public required string Code { get; init; }
    [JsonPropertyName("prefix")] public required string Prefix { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
}
