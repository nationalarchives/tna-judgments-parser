using System.Text.Json.Serialization;

namespace TRE.FindCaseLaw.Inputs;

public class InputMetadata
{
    [JsonPropertyName("uri")]
    public string URI { get; set; }

    [JsonPropertyName("cite")]
    public string Cite { get; set; }

    [JsonPropertyName("court")]
    public string Court { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}
