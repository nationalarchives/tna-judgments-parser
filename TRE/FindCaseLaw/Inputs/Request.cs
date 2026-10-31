using System.Text.Json.Serialization;

namespace TRE.FindCaseLaw.Inputs;

public class Request
{
    [JsonPropertyName("parser-inputs")]
    public ParserInputs Inputs { get; set; }
}
