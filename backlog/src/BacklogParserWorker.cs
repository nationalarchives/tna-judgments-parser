#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Backlog.Csv;

using Microsoft.Extensions.Logging;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog.Src;

/// <summary>
///     This is the main entry point to the bulk backlog parsing process
/// </summary>
internal class BacklogParserWorker(
    ILogger<BacklogParserWorker> logger,
    Api.Parser parser,
    BacklogFiles backlogFiles,
    CsvMetadataReader csvMetadataReader,
    Tracker tracker,
    Bucket bucket,
    MetadataTransformer metadataTransformer)
{
    public int Run(bool isDryRun, uint? id, string pathToCourtMetadataFile, bool autoPublish, string pathToOutputFolder)
    {
        var parserRunId = Guid.NewGuid();
        var manifestRows = new List<BatchManifestRow>();
        var lines = csvMetadataReader.Read(pathToCourtMetadataFile, out var skippedCsvLineIdentifiers,
            out var csvParseErrors, out var numAllLinesInCsv);
        if (lines.Count == 0)
        {
            logger.LogCritical("No valid records found in the metadata file");
            return 1;
        }

        if (id.HasValue)
        {
            // Process only the specific ID
            lines = lines.Where(line => line.id == id.Value.ToString()).ToList();
            if (!lines.Any())
            {
                logger.LogCritical("No valid records found for id {SuppliedId}", id.Value);
                return 1;
            }
        }

        var alreadyDoneLines = new List<CsvLine>();
        var successfulNewLines = new List<CsvLine>();
        var failedToProcessLines = new List<(CsvLine line, Exception exception)>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            try
            {
                if (tracker.WasDone(line))
                {
                    logger.LogInformation("Skipping {LineId} because it was previously processed", line.id);
                    alreadyDoneLines.Add(line);
                    continue;
                }

                logger.LogInformation("Processing file: {FilePath}", line.FilePath);
                var bundle = GenerateBundle(line, autoPublish, parserRunId);

                var bundleFileName = bundle.Uuid + ".tar.gz";
                var output = Path.Combine(pathToOutputFolder, bundleFileName);
                logger.LogInformation("  Writing to output: {Output}", output);
                File.WriteAllBytes(output, bundle.TarGz);
                manifestRows.Add(bundle.ManifestRow);

                if (isDryRun)
                {
                    logger.LogInformation("  This is a dry run - not uploading to S3");
                }
                else
                {
                    logger.LogInformation("  Uploading {BundleFileName} to S3", bundleFileName);
                    bucket.UploadBundle(bundleFileName, bundle.TarGz).Wait();
                }

                tracker.MarkDone(line, bundle.Uuid);
                successfulNewLines.Add(line);

                logger.LogInformation("  success");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing line {LineId}:", line.id);
                failedToProcessLines.Add((line, ex));
            }
            finally
            {
                logger.LogInformation("{Percent}% done", 100 * (i + 1) / lines.Count);
            }
        }

        LogFinalStatistics(logger, alreadyDoneLines, successfulNewLines, failedToProcessLines, csvParseErrors,
            skippedCsvLineIdentifiers, numAllLinesInCsv);

        if (manifestRows.Count > 0)
        {
            var manifestPath = Path.Combine(pathToOutputFolder, $"batch-manifest-{parserRunId}.csv");
            WriteManifest(manifestPath, manifestRows);
            logger.LogInformation("Wrote batch manifest to: {ManifestPath}", manifestPath);

            var parserRunIdPath = Path.Combine(pathToOutputFolder, $"parser-run-id-{parserRunId}.txt");
            WriteParserRunId(parserRunIdPath, parserRunId);
            logger.LogInformation("Wrote parser run id to: {ParserRunIdPath}", parserRunIdPath);

            var bundleReferencesPath = Path.Combine(pathToOutputFolder, $"bundle-references-{parserRunId}.txt");
            WriteBundleReferences(bundleReferencesPath, manifestRows);
            logger.LogInformation("Wrote bundle references to: {BundleReferencesPath}", bundleReferencesPath);
        }

        if (failedToProcessLines.Count > 0 || csvParseErrors.Count > 0)
        {
            return 1;
        }

        return 0;
    }

    private static void LogFinalStatistics(ILogger logger, List<CsvLine> alreadyDoneLines,
        List<CsvLine> successfulNewLines, List<(CsvLine line, Exception exception)> failedToProcessLines,
        List<string> csvParseErrors, List<string> skippedCsvLineIdentifiers, int numAllLinesInCsv)
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
                    Cite = csvLine.Ncn,
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

    private Bundle GenerateBundle(CsvLine csvLine, bool autoPublish, Guid parserRunId)
    {
        var tdrUuid = !string.IsNullOrWhiteSpace(csvLine.Uuid)
            ? csvLine.Uuid
            : backlogFiles.FindUuidInTransferMetadata(csvLine.FilePath);

        var sourceContent = backlogFiles.ReadFile(tdrUuid);
        var mimeType = MetadataTransformer.GetMimeType(csvLine.Extension);

        var isStub = string.Equals(mimeType, "application/pdf", StringComparison.InvariantCultureIgnoreCase);
        var response = CreateResponse(csvLine, mimeType, sourceContent, isStub);

        var contentHash = Hash(sourceContent);
        var images = response.Images?.ToArray() ?? [];

        var externalMetadataFields = metadataTransformer.CsvLineToMetadataFields(csvLine);

        var trePipelineMetadata = metadataTransformer.CreateFullTreMetadata(parserRunId, csvLine.FileName, mimeType, contentHash, autoPublish, images, response.Meta, externalMetadataFields, !isStub);

        return Bundle.Make(response, trePipelineMetadata, sourceContent, csvLine.FileName, tdrUuid, images);
    }

    private static void WriteManifest(string manifestPath, IEnumerable<BatchManifestRow> manifestRows)
    {
        var lines = new List<string>
        {
            "parser_run_id,bundle_reference,bundle_filename,source_filename,source_uuid,parser_uri,parser_cite"
        };

        lines.AddRange(manifestRows.Select(row => string.Join(",", new[]
        {
            EscapeCsv(row.ParserRunId.ToString()),
            EscapeCsv(row.BundleReference),
            EscapeCsv(row.BundleFileName),
            EscapeCsv(row.SourceFilename),
            EscapeCsv(row.SourceUuid),
            EscapeCsv(row.ParserUri),
            EscapeCsv(row.ParserCite)
        })));

        File.WriteAllLines(manifestPath, lines, Encoding.UTF8);
    }

    private static void WriteParserRunId(string parserRunIdPath, Guid parserRunId)
    {
        File.WriteAllText(parserRunIdPath, parserRunId + Environment.NewLine, Encoding.UTF8);
    }

    private static void WriteBundleReferences(string bundleReferencesPath, IEnumerable<BatchManifestRow> manifestRows)
    {
        var lines = manifestRows
            .Select(row => row.BundleReference)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        File.WriteAllLines(bundleReferencesPath, lines, Encoding.UTF8);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        return needsQuoting ? $"\"{escaped}\"" : escaped;
    }

    public static string Hash(byte[] content)
    {
        var hash = SHA256.HashData(content);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
    }
}
