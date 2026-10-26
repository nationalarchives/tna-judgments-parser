using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TRE.FindCaseLaw.Outputs;

public class ParserOutputs
{
    [JsonPropertyName("xml")]
    public string XMLFilename { get; set; }

    [JsonPropertyName("metadata")]
    public string MetadataFilename { get; set; }

    [JsonPropertyName("images")]
    public IEnumerable<string> ImageFilenames { get; set; }

    [JsonPropertyName("log")]
    public string LogFilename { get; set; }

    [JsonPropertyName("error-messages")]
    public IEnumerable<string> ErrorMessages { get; set; }
}
