using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UK.Gov.NationalArchives.CaseLaw.TRE;

public class Response
{
    [JsonPropertyName("parser-outputs")]
    public ParserOutputs Outputs { get; set; }
}

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
