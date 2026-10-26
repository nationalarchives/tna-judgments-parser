using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TRE.FindCaseLaw.Inputs;

public class ParserInputs
{
    public static readonly string JudgmentDocumentType = "judgment";

    public static readonly string PressSummaryDocumentType = "pressSummary";

    [JsonPropertyName("consignment-reference")]
    public string ConsignmentReference { get; set; }

    [JsonPropertyName("document-url")]
    public string DocumentUrl { get; set; }

    [JsonPropertyName("attachment-urls")]
    public IEnumerable<string> AttachmentURLs { get; set; }

    [JsonPropertyName("s3-bucket")]
    public string S3Bucket { get; set; }

    [JsonPropertyName("s3-output-prefix")]
    public string S3OutputPrefix { get; set; }

    /* new params for TREv2 */

    [JsonPropertyName("s3-input-bucket")]
    public string S3InputBucket { get; set; }

    [JsonPropertyName("s3-input-key")]
    public string S3InputKey { get; set; }

    [JsonPropertyName("document-type")]
    public string DocumentType { get; set; }

    [JsonPropertyName("metadata")]
    public InputMetadata Metadata { get; set; }
}
