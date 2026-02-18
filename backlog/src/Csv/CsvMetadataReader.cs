using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using CsvHelper;
using CsvHelper.Configuration;

using Microsoft.Extensions.Logging;

namespace Backlog.Csv;

class CsvMetadataReader(ILogger<CsvMetadataReader> logger)
{
    internal List<CsvLine> Read(string csvPath, out List<string> csvParseErrors)
    {
        using var streamReader = new StreamReader(csvPath);
        return Read(streamReader, out csvParseErrors);
    }

    internal List<CsvLine> Read(TextReader textReader, out List<string> csvParseErrors)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            ShouldSkipRecord = args => false,
            IgnoreBlankLines = true,
            PrepareHeaderForMatch = args => args.Header.ToLower()
        };
        using var csv = new CsvReader(textReader, config);
            
        csv.Context.RegisterClassMap<CsvLineMap>();
            
        var records = new List<CsvLine>();
        csvParseErrors = [];
            
        // Read the header first
        csv.Read();
        csv.ReadHeader();

        // Now read data rows
        while (csv.Read())
        {
            try
            {
                var record = csv.GetRecord<CsvLine>();

                // Use DataAnnotations validation
                var validationContext = new ValidationContext(record);
                var validationResults = new List<ValidationResult>();

                if (Validator.TryValidateObject(record, validationContext, validationResults, true))
                {
                    records.Add(record);
                }
                else
                {
                    var errors = string.Join(", ", validationResults.Where(r => r != ValidationResult.Success)
                                                                    .Select(r => r.ErrorMessage));

                    csvParseErrors.Add($"Line {csv.Context.Parser!.Row}: {errors}");
                    logger.LogError("CSV validation errors [{Errors}] at row {ParserRow}", errors,
                        csv.Context.Parser?.Row);
                }
            }
            catch (Exception ex)
            {
                var exceptionMessage = ex is CsvHelperException
                    ? ex.Message.Substring(0, ex.Message.IndexOf(Environment.NewLine, StringComparison.Ordinal))
                    : ex.Message;

                var rawLine = csv.Context!.Parser!.RawRecord.ReplaceLineEndings(string.Empty);
                csvParseErrors.Add(
                    $"Line {csv.Context.Parser!.Row}: {exceptionMessage} [{rawLine}]");
                logger.LogError(ex, "Error parsing row {ParserRow}", csv.Context.Parser?.Row);
            }
        }

        return records;
    }
    
    private sealed class CsvLineMap : ClassMap<CsvLine>
    {
        public CsvLineMap()
        {
            Configure();
        }

        private void Configure()
        {
            AutoMap(CultureInfo.InvariantCulture);
            Map(l => l.decision_datetime)
                .TypeConverterOption.DateTimeStyles(DateTimeStyles.AllowWhiteSpaces & DateTimeStyles.AssumeUniversal)
                .Validate(v => Regex.IsMatch(v.Field.Trim(), @"^\d\d\d\d")); // Ensure dates start with the year
            Map(l => l.Jurisdictions)
                .Optional()
                .Convert(convertFromStringArgs =>
                {	
                    // Get value
                    convertFromStringArgs.Row.TryGetField<string>("jurisdictions", out var field);
                    return field?.Split(',').Select(item => item.Trim()) ?? [];
                });
            Map(l => l.Skip)
                .Optional()
                .Convert(convertFromStringArgs =>
                {
                    convertFromStringArgs.Row.TryGetField<string>(nameof(CsvLine.Skip), out var field);
                    return field?.Trim().ToLower() switch
                    {
                        null or "" or "n" or "no" or "f" or "false" or "0" => false,
                        _ => true // return true when there is any value that is not explicitly negative
                    };
                });

            Map(l => l.FullCsvLineContents)
                .Convert(convertFromStringArgs =>
                {
                    var headerNames = convertFromStringArgs.Row.HeaderRecord!;
                    return headerNames.ToDictionary(headerName => headerName.Trim(),
                        headerName => convertFromStringArgs.Row[headerName]);
                });
        }
    }
}
