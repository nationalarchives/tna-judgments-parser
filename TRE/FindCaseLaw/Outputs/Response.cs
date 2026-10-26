using System.Text.Json.Serialization;

namespace TRE.FindCaseLaw.Outputs;

public class Response
{
    [JsonPropertyName("parser-outputs")]
    public ParserOutputs Outputs { get; set; }
}
