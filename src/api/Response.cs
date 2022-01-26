
using System.Collections.Generic;
using System.Text.Json;

namespace UK.Gov.NationalArchives.Judgments.Api {

public class Response {

    public string Xml { get; init; }

    public Meta Meta { get; init; }

    public IEnumerable<Image> Images { get; init; }

    private static JsonSerializerOptions options = new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal string ToJson() {
        return JsonSerializer.Serialize(this, options);
    }

}

public class Image {

    public string Name { get; init; }

    public string Type { get; init; }

    public byte[] Content { get; init; }

}

}
