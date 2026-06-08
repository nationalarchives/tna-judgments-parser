#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Backlog.Csv;
using Backlog.Options;
using Backlog.Src;
using Backlog.Tracking;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog;

internal interface IBacklogParserWorker
{
    Task<int> RunAsync();
}

/// <summary>
///     This is the main entry point to the bulk backlog parsing process
/// </summary>
internal class BacklogParserWorker(
    ILogger<BacklogParserWorker> logger,
    Api.IParser parser,
    IBacklogFiles backlogFiles,
    ICsvMetadataReader csvMetadataReader,
    ITracker tracker,
    IBucket bucket,
    IMetadataTransformer metadataTransformer,
    IOptions<BacklogParserOptions> backlogParserOptions) : IBacklogParserWorker
{
    public async Task<int> RunAsync()
    {
        logger.LogInformation("Starting parser run {ParserRunId}", tracker.CurrentParserRunId);
        var lines = csvMetadataReader.Read(out var skippedCsvLineIdentifiers, out var csvParseErrors,
            out var numAllLinesInCsv);
        if (lines.Count == 0)
        {
            logger.LogCritical("No valid records found in the metadata file");
            return 1;
        }

        var singleIdToRun = backlogParserOptions.Value.SingleIdToRun;
        if (singleIdToRun.HasValue)
        {
            // Process only the specific ID
            lines = lines.Where(line => line.id == singleIdToRun.ToString()).ToList();
            if (!lines.Any())
            {
                logger.LogCritical("No valid records found for id {SuppliedId}", singleIdToRun);
                return 1;
            }
        }

        var alreadyDoneLines = new List<CsvLine>();
        var successfulNewLines = new List<CsvLine>();
        var failedToProcessLines = new List<(CsvLine line, Exception exception)>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            Guid? sourceUuid = null;
            try
            {
                sourceUuid = Guid.Parse(line.Uuid);
                if (tracker.IsAlreadySentToIngester(sourceUuid.Value))
                {
                    logger.LogInformation("Skipping {LineId} because it was previously processed", line.id);
                    alreadyDoneLines.Add(line);
                    continue;
                }

                await tracker.StartTrackingAsync(sourceUuid.Value, line, line.CsvProperties.Hash);

                logger.LogInformation("Processing file: {FilePath}", line.FilePath);
                var bundle = await GenerateBundleAsync(line);

                var bundleFileName = bundle.Uuid + ".tar.gz";
                var output = Path.Combine(backlogParserOptions.Value.OutputFolderPath, bundleFileName);
                logger.LogInformation("  Writing to output: {Output}", output);
                File.WriteAllBytes(output, bundle.TarGz);

                await bucket.UploadBundleAsync(bundleFileName, bundle.TarGz);

                await tracker.UpdateToSentToIngesterAsync(sourceUuid.Value);
                successfulNewLines.Add(line);

                logger.LogInformation("  success");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing line {LineId}:", line.id);
                failedToProcessLines.Add((line, ex));
                if(sourceUuid.HasValue)
                    await tracker.UpdateToParserFailedAsync(sourceUuid.Value, ex);
            }
            finally
            {
                logger.LogInformation("{Percent}% done", 100 * (i + 1) / lines.Count);
            }
        }

        LogFinalStatistics(alreadyDoneLines, successfulNewLines, failedToProcessLines, csvParseErrors,
            skippedCsvLineIdentifiers, numAllLinesInCsv);

        if (failedToProcessLines.Count > 0 || csvParseErrors.Count > 0)
        {
            return 1;
        }

        return 0;
    }

    private void LogFinalStatistics(List<CsvLine> alreadyDoneLines, List<CsvLine> successfulNewLines,
        List<(CsvLine line, Exception exception)> failedToProcessLines, List<string> csvParseErrors,
        List<string> skippedCsvLineIdentifiers, int numAllLinesInCsv)
    {
        var numSkippedCsvLines = skippedCsvLineIdentifiers.Count;
        var markedAsSkipIds = numSkippedCsvLines > 0
            ? StringJoinFirstFive(skippedCsvLineIdentifiers, ", ")
            : string.Empty;
        var successfulFileExtensionBreakdown = string.Join(", ", successfulNewLines.GroupBy(l => l.Extension).Select(g => $"{g.Count()} {g.Key}"));
        
        logger.LogInformation("""
                              ---------------------------
                              Successfully processed {SuccessfulLinesCount} of {CsvLinesCount} csv lines, of which:
                                - {NewLinesCount} lines were new ({SuccessfulFileExtensionBreakdown})
                                - {MarkedToSkipLineCount} lines were marked in the csv to skip ({MarkedToSkipIds})
                                - {AlreadyDoneLineCount} lines were skipped because they had been processed in a previous run
                              """,
            numSkippedCsvLines + alreadyDoneLines.Count + successfulNewLines.Count,
            numAllLinesInCsv,
            successfulNewLines.Count,
            successfulFileExtensionBreakdown,
            numSkippedCsvLines, markedAsSkipIds,
            alreadyDoneLines.Count
        );

        if (csvParseErrors.Count > 0)
        {
            logger.LogError("""
                            ---------------------------
                            Failed to read {FailedLineCount} lines from the csv:
                            {FailedLineDetails}
                            """,
                csvParseErrors.Count,
                string.Join(Environment.NewLine, csvParseErrors.Select(error => $"  - {error}"))
            );
        }

        if (failedToProcessLines.Count > 0)
        {
            var failedIdsGroupedByErrorMessage = failedToProcessLines
                .GroupBy(f =>
                    {
                        return f.exception.Message switch
                        {
                            _ when f.exception.Message.StartsWith("Could not find file") => "Could not find file",
                            _ when f.exception.Message.StartsWith("Couldn't find file with UUID") =>
                                "Couldn't find file with UUID",
                            _ when f.exception.Message.EndsWith("was not recognized as a valid DateTime.") =>
                                "String was not recognized as a valid DateTime",
                            _ => f.exception.Message
                        };
                    },
                    f => f.line.id);

            var groupedErrorDescriptions = failedIdsGroupedByErrorMessage.Select(groupOfErrors =>
                $"  - {groupOfErrors.Count()} lines failed with exception message \"{groupOfErrors.Key}\". Ids affected were: ({StringJoinFirstFive(groupOfErrors, ", ")})");
            var failedFileExtensionBreakdown = string.Join(", ", failedToProcessLines.GroupBy(l => l.line.Extension).Select(g => $"{g.Count()} {g.Key}"));

            logger.LogError("""
                            ---------------------------
                            Failed to process {FailedLineCount} lines ({FailedFileExtensionBreakdown}), of which:
                            {GroupedErrorDescriptions}
                            """,
                failedToProcessLines.Count,
                failedFileExtensionBreakdown,
                StringJoinFirstFive(groupedErrorDescriptions, Environment.NewLine)
            );
        }

        if (failedToProcessLines.Count == 0 && csvParseErrors.Count == 0)
        {
            logger.LogInformation("No failed lines");
        }
    }

    private static string StringJoinFirstFive(IEnumerable<string> unenumeratedCollection, string separator)
    {
        var array = unenumeratedCollection as string[] ?? unenumeratedCollection.ToArray();
        return array.Length <= 5
            ? string.Join(separator, array)
            : string.Join(separator, array.Take(5)) + "...";
    }

    private Api.Response CreateResponse(CsvLine csvLine, string mimeType, byte[] sourceContent, bool isStub)
    {
        Api.Response response;
        if (isStub)
        {
            var stubMetadata = MetadataTransformer.MakeMetadata(csvLine);
            var stub = Stub.Make(stubMetadata);

            response = new Api.Response
            {
                Xml = stub.Serialize(),
                Meta = new Api.Meta
                {
                    DocumentType = "decision",
                    Court = stubMetadata.Court?.Code,
                    Date = stubMetadata.Date?.Date,
                    Name = stubMetadata.Name
                }
            };
        }
        else
        {
            var request = new Api.Request
            {
                Meta = new Api.Meta
                {
                    DocumentType = "decision",
                    Cite = csvLine.CleanedNcn,
                    Court = csvLine.Court,
                    Date = csvLine.DecisionDateTime.ToString("yyyy-MM-dd"),
                    Name = csvLine.FirstPartyName + " v " + csvLine.Respondent,
                    JurisdictionShortNames = csvLine.Jurisdictions.ToList(),
                    Extensions = new Api.Extensions
                    {
                        SourceFormat = mimeType,
                        CaseNumbers = csvLine.CaseNo.ToList(),
                        Parties = csvLine.Parties.ToList(),
                        Categories = csvLine.Categories.ToList(),
                        WebArchivingLink = csvLine.WebArchiving
                    }
                },
                Hint = Api.Hint.UKUT,
                Content = sourceContent
            };

            response = parser.Parse(request);

            if (response.Xml.Contains("<header />"))
            {
                throw new NotSupportedException(
                    "Couldn't parse header - try updating titles used to identify the end of header in OptimizedUKUTParser.titles");
            }
        }

        return response;
    }

    private async Task<Bundle> GenerateBundleAsync(CsvLine csvLine)
    {
        var sourceContent = backlogFiles.ReadFile(csvLine.Uuid);
        var mimeType = MetadataTransformer.GetMimeType(csvLine.Extension);

        var isStub = string.Equals(mimeType, "application/pdf", StringComparison.InvariantCultureIgnoreCase);
        var response = CreateResponse(csvLine, mimeType, sourceContent, isStub);

        var sourceHash = Hash(sourceContent);
        var images = response.Images?.ToArray() ?? [];

        var externalMetadataFields = metadataTransformer.CsvLineToMetadataFields(csvLine);

        // For files that don't end in docx and aren't stubs, they must have gone through
        // the parser as a docx. Make sure their filename agrees with that.
        var bundleSourceFilename = csvLine.FileName;
        if (!isStub && !csvLine.FileName.EndsWith(".docx", StringComparison.InvariantCultureIgnoreCase))
        {
            bundleSourceFilename = csvLine.FileName + ".docx";
        }

        var trePipelineMetadata = metadataTransformer.CreateFullTreMetadata(bundleSourceFilename, csvLine.FileName,
            mimeType, sourceHash, images, response.Meta, externalMetadataFields, !isStub);

        await tracker.UpdateToParsedAsync(Guid.Parse(csvLine.Uuid), trePipelineMetadata.Parameters.TRE.Reference, response.Meta.Cite, sourceHash, response.Meta.Name);
        
        return Bundle.Make(response, trePipelineMetadata, sourceContent, bundleSourceFilename, images);
    }

    public static string Hash(byte[] content)
    {
        // This is the hash of the source document
        var hash = SHA256.HashData(content);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
    }
}
