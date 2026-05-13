#nullable enable

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;

using Microsoft.Extensions.Options;

namespace Backlog.Options;

public sealed class BacklogParserOptions
{
    public static readonly string SectionName = "BacklogParser";

    [Required]
    public required string CourtMetadataFilePath { get; set; }

    [Required]
    public required string DataFolderPath { get; set; }

    [Required]
    public required string OutputFolderPath { get; set; }

    [Required]
    public required string TrackerFilePath { get; set; }

    public string? BucketName { get; set; }

    public bool IsDryRun { get; set; }
    public uint? SingleIdToRun { get; set; }
    public bool AutoPublish { get; set; }
}

public class BacklogParserOptionsValidation(IFileSystem fileSystem) : IValidateOptions<BacklogParserOptions>
{
    public ValidateOptionsResult Validate(string? name, BacklogParserOptions options)
    {
        var validateOptionsResultBuilder = new ValidateOptionsResultBuilder();
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(options, new ValidationContext(options), validationResults, true);
        if (string.IsNullOrWhiteSpace(options.BucketName) && !options.IsDryRun)
        {
            validationResults.Add(new ValidationResult(
                $"{nameof(options.BucketName)} must be set when {nameof(options.IsDryRun)} is false",
                [nameof(options.BucketName), nameof(options.IsDryRun)]));
        }

        if (!string.IsNullOrWhiteSpace(options.CourtMetadataFilePath) && !fileSystem.File.Exists(options.CourtMetadataFilePath))
        {
            validationResults.Add(new ValidationResult(
                $"{nameof(options.CourtMetadataFilePath)} \"{options.CourtMetadataFilePath}\" does not exist",
                [nameof(options.CourtMetadataFilePath)]));
        }

        if (!string.IsNullOrWhiteSpace(options.DataFolderPath) && !fileSystem.Directory.Exists(options.DataFolderPath))
        {
            validationResults.Add(new ValidationResult(
                $"{nameof(options.DataFolderPath)} \"{options.DataFolderPath}\" does not exist",
                [nameof(options.DataFolderPath)]));
        }

        validateOptionsResultBuilder.AddResults(validationResults);

        return validateOptionsResultBuilder.Build();
    }
}
