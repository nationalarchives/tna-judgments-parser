#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;

using Backlog.Options;
using Backlog.Tracking;

using CsvHelper;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backlog.Csv;

internal interface ICsvMetadataReader
{
    List<CsvLine> Read();

    List<CsvLine> Read(TextReader textReader);
}

internal class CsvMetadataReader(
    ILogger<CsvMetadataReader> logger,
    IOptions<BacklogParserOptions> backlogParserOptions,
    ITracker tracker)
    : ICsvMetadataReader
{
    private string csvName = "unknown.csv";
    private string csvHash = "unknown";

    public List<CsvLine> Read()
    {
        var csvPath = backlogParserOptions.Value.CourtMetadataFilePath;
        csvName = Path.GetFileName(csvPath);
        csvHash = BacklogParserWorker.Hash(File.ReadAllBytes(csvPath));

        using var streamReader = new StreamReader(csvPath);
        return Read(streamReader);
    }

    public List<CsvLine> Read(TextReader textReader)
    {
        var options = new CsvReaderOptions
        {
            SkipEmptyLines = true,
            NormalizeHeaderName = header => header.ToLower().Replace("_", ""),
            TrimWhitespace = true,
            TrimInsideQuotes = true,
            TrackLineNumbers = true,
            MissingField = CsvMissingFieldHandling.Throw,
            NullValues = [string.Empty],
            MalformedData = CsvMalformedDataHandling.Throw
        };
        using var csv = CsvReader.Create(textReader, options);

        var booleanSkipConverter = new BooleanSkipConverter();

        var records = new List<CsvLine>();
        var numAllLinesInCsv = 0;

        var skipFieldExists = csv.TryGetFieldIndex(nameof(CsvLine.Skip), out var skipFieldIndex);

        while (true)
        {
            if (ProcessCsvLine(csv, skipFieldExists, skipFieldIndex, booleanSkipConverter, records))
            {
                numAllLinesInCsv++;
            }
            else
            {
                break;
            }
        }

        tracker.NumAllLinesInCsv = numAllLinesInCsv;
        return records;
    }

    private bool ProcessCsvLine(CsvReader csv, bool skipFieldExists, int skipFieldIndex,
        BooleanSkipConverter booleanSkipConverter, List<CsvLine> records)
    {
        try
        {
            if (!csv.Read())
            {
                // No more records to process
                return false;
            }

            if (skipFieldExists && skipFieldIndex < csv.FieldCount && booleanSkipConverter.Read(csv, skipFieldIndex))
            {
                tracker.TrackSkipped($"Line {csv.LineNumber}");
                logger.LogInformation("Skipping line {LineNumber} because it was marked to skip in the csv",
                    csv.LineNumber);
                return true;
            }

            var record = csv.GetRecord<CsvLine>() with
            {
                CsvProperties = (Name: csvName, Hash: csvHash),
                FullCsvLineContents = csv.Headers.ToDictionary(h => h, h => csv[h].ToString())
            };

            // Use DataAnnotations validation
            var validationResults = new List<ValidationResult>();

            if (!Validator.TryValidateObject(record, new ValidationContext(record), validationResults, true))
            {
                var validationErrors = string.Join(", ", validationResults.Where(r => r != ValidationResult.Success)
                                                                          .Select(r => r.ErrorMessage));
                RecordCsvParseError(validationErrors, csv);
                return true;
            }

            records.Add(record);
        }
        catch (Exception e) when (e is not IOException and not ObjectDisposedException)
        {
            HandleCsvParseError(e, csv);
        }

        return true;
    }

    private void HandleCsvParseError(Exception e, CsvReader csv)
    {
        switch (e)
        {
            case CsvFormatException { InnerException: CsvValidationException validationException } cfe:
                {
                    RecordCsvParseError(
                        $"\"{csv.GetString(cfe.FieldIndex)}\" failed validation with message: {validationException.Message}",
                        csv);
                    break;
                }
            case CsvFormatException
            {
                Member: not null, FieldIndex: >= 0, InnerException: not IndexOutOfRangeException
            } cfe:
                {
                    var memberName = cfe.Member[(cfe.Member.LastIndexOf('.') + 1)..];
                    var memberType = typeof(CsvLine).GetProperty(memberName)?.PropertyType.Name ?? "unknown";
                    RecordCsvParseError(
                        $"Could not convert field `{memberName}` with value \"{csv.GetString(cfe.FieldIndex)}\" to type `{memberType}`",
                        csv);
                    break;
                }

            case CsvFormatException when e.Message.StartsWith("Malformed CSV"):
                {
                    RecordCsvParseError(e.Message);
                    break;
                }
            default:
                {
                    RecordCsvParseError((e.InnerException ?? e).Message, csv);
                    break;
                }
        }
    }

    private void RecordCsvParseError(string errorMessage, CsvReader csv)
    {
        RecordCsvParseError(
            $"Line {csv.LineNumber}: {errorMessage} [{Encoding.UTF8.GetString(csv.RawRecord).ReplaceLineEndings(string.Empty)}]");
    }

    private void RecordCsvParseError(string errorMessage)
    {
        tracker.TrackCsvParseError(errorMessage);
        logger.LogError(errorMessage);
    }
}
