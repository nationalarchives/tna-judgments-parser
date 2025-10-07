
using System;
using System.Text.Json.Serialization;

namespace UK.Gov.NationalArchives.CaseLaw.TRE.Lawmaker
{

    public class Request
    {

        [JsonPropertyName("parser-inputs")]
        public ParserInputs Inputs { get; set; }

    }

    public class ParserInputs
    {

        [JsonPropertyName("consignment-reference")]
        public string ConsignmentReference { get; set; }

        [JsonPropertyName("s3-bucket")]
        public string S3Bucket { get; set; }

        [JsonPropertyName("s3-output-prefix")]
        public string S3OutputPrefix { get; set; }

        [JsonPropertyName("s3-input-bucket")]
        public string S3InputBucket { get; set; }

        [JsonPropertyName("s3-input-key")]
        public string S3InputKey { get; set; }

        [JsonPropertyName("document-type")]
        public string DocumentType { get; set; }

        [JsonPropertyName("document-subtype")]
        public string SubType { get; set; }

        [JsonPropertyName("document-procedure")]
        public string Procedure { get; set; }
    }
}
