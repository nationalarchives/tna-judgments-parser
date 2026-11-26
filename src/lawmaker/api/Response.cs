
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

    public string Stage { get; init; }

    public int? BlockNumber { get; init; }

    public string BlockText { get; init; }

    public string Message { get; init; }

    // Lawmaker reads the block number and text out of this message by matching
    // "at block N" and "block text:", so the block text must stay last. Each part is
    // its own sentence, with a space before the line break, so the message still reads
    // properly when Lawmaker removes the line break.
    public string Describe()
    {
        var location = BlockNumber is int blockNumber ? $" at block {blockNumber}" : "";
        var stage = Stage is null ? "" : $" while {Stage}";
        var description = $"Parsing failed{location}{stage} ({Message}).";
        if (!string.IsNullOrEmpty(BlockText))
            description += $" \nBlock text: {BlockText}";
        return description;
    }

}

public class Image
{

    public string Name { get; init; }

    public string Type { get; init; }

    public byte[] Content { get; init; }

}
