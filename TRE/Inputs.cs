
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace UK.Gov.NationalArchives.CaseLaw.TRE
{

    public class Request
    {

        [JsonPropertyName("parser-inputs")]
        public ParserInputs Inputs { get; set; }

    }

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
        //        [JsonUnmappedMemberHandling] in .NET 8
        public InputMetadata Metadata { get; set; }

        // [JsonPropertyName("passthrough")]
        // public object Passthrough { get; set; }

    }

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

    public partial class InputHelper
    {

        public static Api.Hint? GetHint(ParserInputs inputs, ILogger logger)
        {
            if (inputs is null)
                return null;
            if (string.IsNullOrWhiteSpace(inputs.DocumentType))
            {
                logger.LogInformation("document type is null");
                return null;
            }
            if (inputs.DocumentType == ParserInputs.PressSummaryDocumentType)
            {
                logger.LogInformation("document type is {}", inputs.DocumentType);
                return Api.Hint.PressSummary;
            }
            if (inputs.DocumentType != ParserInputs.JudgmentDocumentType)
            {
                logger.LogCritical("unrecognized document type: {}", inputs.DocumentType);
                throw new Exception("unrecognized document type: " + inputs.DocumentType);
            }
            string court = inputs.Metadata?.Court;
            if (court is null)
                return Api.Hint.Judgment;
            if (court == Courts.SupremeCourt.Code)
                return Api.Hint.UKSC;
            if (court == Courts.PrivyCouncil.Code)
                return Api.Hint.UKSC;
            if (court.StartsWith("EWCA"))
                return Api.Hint.EWCA;
            if (court.StartsWith("EWHC"))
                return Api.Hint.EWHC;
            if (court.StartsWith("UKUT"))
                return Api.Hint.UKUT;
            if (court.StartsWith("UKFTT") || court.StartsWith("FTT"))
                return Api.Hint.UKUT;
            return Api.Hint.Judgment;
        }

        public static Api.Meta GetMetadata(ParserInputs inputs, ILogger logger)
        {
            void ValidateFclUri(string uri)
            {
                if (uri is null) { return; }
                logger.LogInformation("input URI is {}", uri);
                if (Citations.IsValidUriComponent(uri)) // old style, uksc/2023/1
                {
                    bool docPressSummary = inputs.DocumentType == "pressSummary";
                    bool uriPressSummary = uri.Contains("press-summary");
                    if (docPressSummary != uriPressSummary)
                    {
                        logger.LogCritical("document type and URI do not match");
                        throw new Exception("document type and URI do not match");
                    }
                    return;
                }
                if (Regex.IsMatch(uri, @"^d-[0-9a-f-]{36}$")) // new style, UUID
                {
                    return;
                }
                    
                logger.LogCritical("input URI is not supported: {}", inputs.Metadata.URI);
                throw new Exception("input URI is not supported: " + inputs.Metadata.URI);            
            }

            if (inputs is null)
                return new Api.Meta();
            if (inputs.Metadata is null)
            {
                logger.LogInformation("input metadata is null");
                return new Api.Meta();
            }

            if (string.IsNullOrWhiteSpace(inputs.DocumentType))
            {
                logger.LogCritical("metadata is present but document type is null");
                throw new Exception("metadata is present but document type is null");
            }

            string uri = inputs.Metadata.URI;
            string cite = inputs.Metadata.Cite;
            string court = inputs.Metadata.Court;
            string date = inputs.Metadata.Date;
            string name = inputs.Metadata.Name;

            if (string.IsNullOrWhiteSpace(uri))
                uri = null;
            if (string.IsNullOrWhiteSpace(cite))
                cite = null;
            if (string.IsNullOrWhiteSpace(court))
                court = null;
            if (string.IsNullOrWhiteSpace(date))
                date = null;
            if (string.IsNullOrWhiteSpace(name))
                name = null;

            /* validation and normalization */

            uri = Api.URI.ExtractShortURIComponent(uri);
            ValidateFclUri(uri);

            cite = cite is null ? null : Citations.Normalize(cite);
            if (cite is null && inputs.Metadata.Cite is not null)
            {
                logger.LogCritical("input cite is not supported: {}", inputs.Metadata.Cite);
                throw new Exception("input cite is not supported: " + inputs.Metadata.Cite);
            }
            if (cite is not null)
                logger.LogInformation("input cite is {}", cite);

            if (uri is not null && cite is not null && !uri.StartsWith(Citations.MakeUriComponent(cite))) // handles press-summary URIs
            {
                logger.LogWarning("cite and URI do not match");
                // throw new Exception("cite and URI do not match");
            }

            if (court is not null && !Courts.Exists(court))
            {
                logger.LogCritical("input court is not recognized: {}", court);
                throw new Exception("input court is not recognized: " + court);
            }
            if (court is not null)
                logger.LogInformation("input court is {}", court);

            if (date is not null && !DateRegex().IsMatch(date))
            {
                logger.LogCritical("input date is not valid: {}", date);
                throw new Exception("input date is not valid: {}" + date);
            }
            if (date is not null)
                logger.LogInformation("input date is {}", date);

            if (name is not null)
                logger.LogInformation("input name is {}", name);

            return new Api.Meta()
            {
                Uri = uri,
                Cite = cite,
                Court = court,
                Date = date,
                Name = name
            };
        }

        [GeneratedRegex("^\\d{4}-\\d{2}-\\d{2}$")]
        private static partial Regex DateRegex();

    }

}
