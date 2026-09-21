
using System.Collections.Generic;
using System.Text.Json;

namespace UK.Gov.Legislation.Lawmaker.Api;


public class Response
{

    public string Xml { get; init; }

    public IEnumerable<Image> Images { get; init; }

    public ParseError Error { get; init; }

    private static readonly JsonSerializerOptions options = new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, options);
    }

}

public class ParseError
{

    public int BlockNumber { get; init; }

    public string BlockText { get; init; }

    public string Message { get; init; }

}

public class Image
{

    public string Name { get; init; }

    public string Type { get; init; }

    public byte[] Content { get; init; }

}
