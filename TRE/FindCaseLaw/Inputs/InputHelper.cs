using System;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using TRE.Metadata.Enums;

using UK.Gov.Legislation.Judgments;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace TRE.FindCaseLaw.Inputs;

public interface IInputHelper
{
    Api.Hint? GetHint(ParserInputs inputs, ILogger logger);
    Api.Meta GetMetadata(ParserInputs inputs, ILogger logger);
    Route GetRoute(ParserInputs inputs);
    string GetSourceFilename(ParserInputs inputs);
}

public partial class InputHelper : IInputHelper
{
    public Route GetRoute(ParserInputs inputs)
    {
        var hasStructuredMetadata = inputs.Metadata?.MetadataFields?.Count > 0
                                    || !string.IsNullOrWhiteSpace(inputs.Metadata?.Court);

        return hasStructuredMetadata ? Route.Eui : Route.Tdr;
    }

    public string GetSourceFilename(ParserInputs inputs)
    {
        var path = !string.IsNullOrEmpty(inputs.DocumentUrl)
            ? new Uri(inputs.DocumentUrl).AbsolutePath
            : inputs.S3InputKey;

        return Uri.UnescapeDataString(Path.GetFileName(path ?? string.Empty));
    }

    public Api.Hint? GetHint(ParserInputs inputs, ILogger logger)
    {
        if (inputs is null)
        {
            return null;
        }

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
            throw new ArgumentException("unrecognized document type: " + inputs.DocumentType);
        }

        var court = inputs.Metadata?.Court;
        if (court is null)
        {
            return Api.Hint.Judgment;
        }

        if (court == Courts.SupremeCourt.Code)
        {
            return Api.Hint.UKSC;
        }

        if (court == Courts.PrivyCouncil.Code)
        {
            return Api.Hint.UKSC;
        }

        if (court.StartsWith("EWCA"))
        {
            return Api.Hint.EWCA;
        }

        if (court.StartsWith("EWHC"))
        {
            return Api.Hint.EWHC;
        }

        if (court.StartsWith("UKUT"))
        {
            return Api.Hint.UKUT;
        }

        if (court.StartsWith("UKFTT") || court.StartsWith("FTT"))
        {
            return Api.Hint.UKUT;
        }

        return Api.Hint.Judgment;
    }

    public Api.Meta GetMetadata(ParserInputs inputs, ILogger logger)
    {
        void ValidateFclUri(string uri)
        {
            if (uri is null) { return; }

            logger.LogInformation("input URI is {}", uri);
            if (Citations.IsValidUriComponent(uri)) // old style, uksc/2023/1
            {
                var docPressSummary = inputs.DocumentType == "pressSummary";
                var uriPressSummary = uri.Contains("press-summary");
                if (docPressSummary != uriPressSummary)
                {
                    throw new ArgumentException("document type and URI do not match");
                }

                return;
            }

            if (Regex.IsMatch(uri, @"^d-[0-9a-f-]{36}$")) // new style, UUID
            {
                return;
            }

            throw new ArgumentException("input URI is not supported: " + inputs.Metadata.URI);
        }

        if (inputs is null)
        {
            return new Api.Meta();
        }

        if (inputs.Metadata is null)
        {
            logger.LogInformation("input metadata is null");
            return new Api.Meta();
        }

        if (string.IsNullOrWhiteSpace(inputs.DocumentType))
        {
            throw new ArgumentException("metadata is present but document type is null");
        }

        var uri = inputs.Metadata.URI;
        var cite = inputs.Metadata.Cite;
        var court = inputs.Metadata.Court;
        var date = inputs.Metadata.Date;
        var name = inputs.Metadata.Name;

        if (string.IsNullOrWhiteSpace(uri))
        {
            uri = null;
        }

        if (string.IsNullOrWhiteSpace(cite))
        {
            cite = null;
        }

        if (string.IsNullOrWhiteSpace(court))
        {
            court = null;
        }

        if (string.IsNullOrWhiteSpace(date))
        {
            date = null;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = null;
        }

        /* validation and normalization */

        uri = Api.URI.ExtractShortURIComponent(uri);
        ValidateFclUri(uri);

        cite = cite is null ? null : Citations.Normalize(cite);
        if (cite is null && inputs.Metadata.Cite is not null)
        {
            throw new FormatException("input cite is not supported: " + inputs.Metadata.Cite);
        }

        if (cite is not null)
        {
            logger.LogInformation("input cite is {}", cite);
        }

        if (uri is not null && cite is not null
            && !uri.StartsWith(Citations.MakeUriComponent(cite))) // handles press-summary URIs
        {
            logger.LogWarning("cite and URI do not match");
        }

        if (court is not null && !Courts.Exists(court))
        {
            throw new ArgumentException("input court is not recognized: " + court);
        }

        if (court is not null)
        {
            logger.LogInformation("input court is {}", court);
        }

        if (date is not null && !DateRegex().IsMatch(date))
        {
            throw new FormatException("input date is not valid: " + date);
        }

        if (date is not null)
        {
            logger.LogInformation("input date is {}", date);
        }

        if (name is not null)
        {
            logger.LogInformation("input name is {}", name);
        }

        return new Api.Meta
        {
            Uri = uri,
            Cite = cite,
            Court = court,
            Date = date,
            Name = name
        };
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DateRegex();
}
