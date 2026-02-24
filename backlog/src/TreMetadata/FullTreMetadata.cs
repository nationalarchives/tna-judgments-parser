#nullable enable

using System.Text.Json;

namespace Backlog.TreMetadata;

internal class FullTreMetadata
{

    public readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public required Parameters Parameters { get; init; }

}
