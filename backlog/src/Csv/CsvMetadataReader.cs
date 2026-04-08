#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Backlog.Src;

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

using Microsoft.Extensions.Logging;

namespace Backlog.Csv;

internal class CsvMetadataReader(ILogger<CsvMetadataReader> logger)
{
    private string csvName = "unknown.csv";
    private string csvHash = "unknown";

    internal List<CsvLine> Read(string csvPath, out List<string> skippedCsvLineIdentifiers,
        out List<string> csvParseErrors)
    {
        csvName = Path.GetFileName(csvPath);
        csvHash = BacklogParserWorker.Hash(File.ReadAllBytes(csvPath));

        using var streamReader = new StreamReader(csvPath);
        return Read(streamReader, out skippedCsvLineIdentifiers, out csvParseErrors);
    }

    internal List<CsvLine> Read(TextReader textReader, out List<string> skippedCsvLineIdentifiers,
        out List<string> csvParseErrors)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            ShouldSkipRecord = args => false,
            IgnoreBlankLines = true,
            PrepareHeaderForMatch = args => args.Header.ToLower().Replace("_", ""),
            TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes
        };
        using var csv = new CsvReader(textReader, config);

        csv.Context.TypeConverterCache.AddConverter<BooleanSkipConverter>(new BooleanSkipConverter());
        csv.Context.RegisterClassMap(new CsvLineMap(csvName, csvHash));

        var records = new List<CsvLine>();
        skippedCsvLineIdentifiers = [];
        csvParseErrors = [];

        // Read the header first
        csv.Read();
        csv.ReadHeader();

        // Now read data rows
        while (csv.Read())
        {
            try
            {
                try
                {
                    var record = csv.GetRecord<CsvLine>();

                    if (record.Skip)
                    {
                        skippedCsvLineIdentifiers.Add($"Line {csv.Context.Parser!.Row}");
                        logger.LogWarning("Skipping {LineId} because it was marked to skip in the csv", record.id);
                        continue;
                    }

                    // Use DataAnnotations validation
                    var validationContext = new ValidationContext(record);
                    var validationResults = new List<ValidationResult>();

                    if (Validator.TryValidateObject(record, validationContext, validationResults, true))
                    {
                        records.Add(record);
                        continue;
                    }

                    var validationErrors = string.Join(", ", validationResults.Where(r => r != ValidationResult.Success)
                                                                              .Select(r => r.ErrorMessage));
                    SkipOrRecordCsvParseError(csvParseErrors, skippedCsvLineIdentifiers, csv, validationErrors);
                }
                catch (FieldValidationException ex) //created by failed `Validate`s in `CsvLineMap`
                {
                    SkipOrRecordCsvParseError(csvParseErrors, skippedCsvLineIdentifiers, csv,
                        $"\"{ex.Field}\" failed validation with message: {GetCsvHelperExceptionMessage(ex)}");
                }
            }
            catch (TypeConverterException ex)
            {
                SkipOrRecordCsvParseError(csvParseErrors, skippedCsvLineIdentifiers, csv,
                    $"Could not convert field `{ex.MemberMapData.Member?.Name ?? "unknown"}` with value \"{ex.Text}\" to type `{ex.MemberMapData.Type.Name}`");
            }
            catch (CsvHelperException ex)
            {
                SkipOrRecordCsvParseError(csvParseErrors, skippedCsvLineIdentifiers, csv,
                    GetCsvHelperExceptionMessage(ex));
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Critical issue: {ParseError}",
                    CreateParseErrorWithRowInformation(csv, ex.Message));
            }
        }

        return records;
    }

    private static void SkipOrRecordCsvParseError(List<string> csvParseErrors, List<string> skippedCsvLineIdentifiers,
        CsvReader csv, string errorMessage)
    {
        var successfullyRetrievedSkipField = csv.TryGetField<bool>(
            nameof(CsvLine.Skip),
            csv.Context.TypeConverterCache.GetConverter<BooleanSkipConverter>(),
            out var skipFieldValue
        );

        if (successfullyRetrievedSkipField && skipFieldValue)
        {
            skippedCsvLineIdentifiers.Add($"Line {csv.Context.Parser!.Row}");
        }
        else
        {
            csvParseErrors.Add(CreateParseErrorWithRowInformation(csv, errorMessage));
        }
    }

    /// <summary>
    ///     CsvHelperException.Message contains a lot of irrelevant information on multiple lines, but we only want the first
    ///     part which says why something failed
    /// </summary>
    private static string GetCsvHelperExceptionMessage(CsvHelperException ex)
    {
        return ex.Message.Substring(0, ex.Message.IndexOf(Environment.NewLine, StringComparison.Ordinal));
    }

    private static string CreateParseErrorWithRowInformation(CsvReader csv, string errorMessage)
    {
        return
            $"Line {csv.Context.Parser!.Row}: {errorMessage} [{csv.Context.Parser!.RawRecord.ReplaceLineEndings(string.Empty)}]";
    }

    private sealed class CsvLineMap : ClassMap<CsvLine>
    {
        private readonly string csvName;
        private readonly string csvHash;

        public CsvLineMap(string csvName, string csvHash)
        {
            this.csvName = csvName;
            this.csvHash = csvHash;
            Configure();
        }

        private void Configure()
        {
            AutoMap(CultureInfo.InvariantCulture);

            Map(l => l.DecisionDateTime)
                .TypeConverterOption.DateTimeStyles(DateTimeStyles.AllowWhiteSpaces & DateTimeStyles.AssumeUniversal)
                .Validate(v => Regex.IsMatch(v.Field.Trim(), @"^\d\d\d\d"),
                    v => string.IsNullOrWhiteSpace(v.Field)
                        ? "Decision date must be provided"
                        : "Unsupported decision date. Ensure dates are in yyyy-MM-dd format");
            Map(l => l.Jurisdictions)
                .Optional()
                .Convert(convertFromStringArgs =>
                {
                    // Get value
                    convertFromStringArgs.Row.TryGetField<string>("jurisdictions", out var field);
                    return field?.Split(',').Select(item => item.Trim())
                                .Where(jurisdiction => !string.IsNullOrWhiteSpace(jurisdiction)).ToArray() ?? [];
                });

            Map(l => l.FullCsvLineContents)
                .Convert(convertFromStringArgs =>
                {
                    var headerNames = convertFromStringArgs.Row.HeaderRecord!;
                    return headerNames.ToDictionary(headerName => headerName.Trim(),
                        headerName => convertFromStringArgs.Row[headerName] ?? string.Empty);
                });

            Map(l => l.CsvProperties)
                .Constant((Name: csvName, Hash: csvHash));

            // Ensure every column with a value of "" is Null.
            foreach (var map in MemberMaps)
            {
                map.TypeConverterOption.NullValues(string.Empty);
            }
        }
    }
}
