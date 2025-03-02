
using System;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

using Api = UK.Gov.Legislation.Lawmaker.Api;

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

    }

    public partial class InputHelper
    {

        public static Api.DocType? GetDocType(ParserInputs inputs, ILogger logger)
        {
            if (inputs is null)
                return null;
            if (string.IsNullOrWhiteSpace(inputs.DocumentType))
            {
                logger.LogInformation("document type is null");
                return null;
            }
            Api.DocType docType;
            if (Enum.TryParse<Api.DocType>(inputs.DocumentType, out docType))
                return docType;
            else
                logger.LogCritical("unrecognized document type: {}", inputs.DocumentType);
            throw new Exception("unrecognized document type: " + inputs.DocumentType);
        }

    }

}
