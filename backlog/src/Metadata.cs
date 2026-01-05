
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace Backlog.Src
{
    /// <summary>
    ///     Custom validation attribute to ensure subcategories can only exist if their parent category is defined
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CategoryValidationAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is not Metadata.Line line)
            {
                throw new InvalidOperationException(
                    $"{nameof(CategoryValidationAttribute)} can only be used on a {nameof(Metadata.Line)}");
            }

            // Check if main_subcategory exists without main_category
            if (!string.IsNullOrWhiteSpace(line.main_subcategory) && string.IsNullOrWhiteSpace(line.main_category))
            {
                return new ValidationResult(
                    $"Id {line.id} - main_subcategory '{line.main_subcategory}' cannot exist without main_category being defined");
            }

            // Check if sec_subcategory exists without sec_category
            if (!string.IsNullOrWhiteSpace(line.sec_subcategory) && string.IsNullOrWhiteSpace(line.sec_category))
            {
                return new ValidationResult(
                    $"Id {line.id} - sec_subcategory '{line.sec_subcategory}' cannot exist without sec_category being defined");
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    ///     Custom validation attribute to ensure one of appellants or claimants are provided
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AppellantsOrClaimantsPresentValidationAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is not Metadata.Line line)
            {
                throw new InvalidOperationException(
                    $"{nameof(AppellantsOrClaimantsPresentValidationAttribute)} can only be used on a {nameof(Metadata.Line)}");
            }

            // Check that exactly one of claimants or appellants is provided
            var hasClaimants = !string.IsNullOrWhiteSpace(line.claimants);
            var hasAppellants = !string.IsNullOrWhiteSpace(line.appellants);

            return (hasClaimants, hasAppellants) switch
            {
                { hasClaimants: true, hasAppellants: true } => new ValidationResult(
                    $"Id {line.id} - Cannot have both claimants and appellants. Please provide only one."),
                { hasClaimants: false, hasAppellants: false } => new ValidationResult(
                    $"Id {line.id} - Must have either claimants or appellants. At least one is required."),
                _ => ValidationResult.Success
            };
        }
    }

    class Metadata(ILogger<Metadata> logger)
    {
        internal class LineMap : ClassMap<Line>
        {
            public LineMap()
            {
                AutoMap(CultureInfo.InvariantCulture);
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
                        convertFromStringArgs.Row.TryGetField<string>(nameof(Line.Skip), out var field);
                        return field?.Trim().ToLower() switch
                        {
                            null or "" or "n" or "no" or "f" or "false" or "0" => false,
                            _ => true // return true when there is any value that is not explicitly negative
                        };
                    });
            }
        }

        [AppellantsOrClaimantsPresentValidation]
        [CategoryValidation]
        internal class Line
        {
            public string id { get; set; }
            public string court { get; set; }
            public string FilePath { get; set; }
            public string Extension { get; set; }
            public string decision_datetime { get; set; }
            public string CaseNo { get; set; }

            [Optional]
            public IEnumerable<string> Jurisdictions { get; set; } = [];
            
            [Optional]
            public string claimants { get; set; }
            
            [Optional]
            public string appellants { get; set; }
            
            public string respondent { get; set; }

            [Optional]
            public string main_category { get; set; }

            [Optional]
            public string main_subcategory { get; set; }

            [Optional]
            public string sec_category { get; set; }

            [Optional]
            public string sec_subcategory { get; set; }
            
            [Optional]
            public string ncn { get; set; }
            
            [Optional]
            public string headnote_summary { get; set; }

            [Optional]
            public string webarchiving { get; set; }
            
            [Optional]
            public string Uuid { get; set; }

            [Optional]
            [Default(false)]
            public bool Skip { get; set; }

            private readonly string DateFormat = "yyyy-MM-dd HH:mm:ss";
            internal string DecisionDate { get => System.DateTime.ParseExact(decision_datetime, DateFormat, CultureInfo.InvariantCulture).ToString("yyyy-MM-dd"); }

            /// <summary>
            /// Gets the name of the first party (either claimants or appellants)
            /// </summary>
            internal string FirstPartyName
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(claimants))
                        return claimants;
                    if (!string.IsNullOrWhiteSpace(appellants))
                        return appellants;
                    throw new InvalidOperationException("No first party (claimants or appellants) is defined");
                }
            }

            /// <summary>
            /// Gets the role of the first party (either Claimant or Appellant)
            /// </summary>
            internal PartyRole FirstPartyRole
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(claimants))
                        return PartyRole.Claimant;
                    if (!string.IsNullOrWhiteSpace(appellants))
                        return PartyRole.Appellant;
                    throw new InvalidOperationException("No first party (claimants or appellants) is defined");
                }
            }
        }

        internal List<Line> Read(string csvPath, out List<string> csvParseErrors)
        {
            using var streamReader = new StreamReader(csvPath);
            return Read(streamReader, out csvParseErrors);
        }

        internal List<Line> Read(TextReader textReader, out List<string> csvParseErrors)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                ShouldSkipRecord = args => false,
                IgnoreBlankLines = true,
                PrepareHeaderForMatch = args => args.Header.ToLower()
            };
            using var csv = new CsvReader(textReader, config);
            
            csv.Context.RegisterClassMap<LineMap>();
            
            var records = new List<Line>();
            csvParseErrors = [];
            
            // Read the header first
            csv.Read();
            csv.ReadHeader();

            // Now read data rows
            while (csv.Read())
            {
                try
                {
                    var record = csv.GetRecord<Line>();

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

        internal static List<Line> FindLines(List<Line> lines, uint id)
        {
            return lines.Where(line => line.id == id.ToString()).ToList();
        }

        internal static ExtendedMetadata MakeMetadata(Line line) {
            // Validation is now handled during CSV reading
            List<ExtendedMetadata.Category> categories = [];
            
            // Only add categories if they exist and are not empty
            if (!string.IsNullOrWhiteSpace(line.main_category)) {
                categories.Add(new ExtendedMetadata.Category { Name = line.main_category });
                
                if (!string.IsNullOrWhiteSpace(line.main_subcategory)) {
                    categories.Add(new ExtendedMetadata.Category { Name = line.main_subcategory, Parent = line.main_category });
                }
            }
            
            if (!string.IsNullOrWhiteSpace(line.sec_category)) {
                categories.Add(new ExtendedMetadata.Category { Name = line.sec_category });
                
                if (!string.IsNullOrWhiteSpace(line.sec_subcategory)) {
                    categories.Add(new ExtendedMetadata.Category { Name = line.sec_subcategory, Parent = line.sec_category });
                }
            }
            string sourceFormat;
            if (line.Extension == ".doc" || line.Extension == ".docx")
                sourceFormat = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            else if (line.Extension == ".pdf")
                sourceFormat = "application/pdf";
            else
                throw new Exception($"Unexpected extension {line.Extension}");

            Court court = Courts.ByCode[line.court];

            var jurisdictions = line.Jurisdictions
                .Where(jurisdiction => !string.IsNullOrWhiteSpace(jurisdiction))
                .Select(jurisdiction => new OutsideJurisdiction { ShortName = jurisdiction });

            string webArchivingLink;
            if (!string.IsNullOrWhiteSpace(line.webarchiving))
            {
                webArchivingLink = line.webarchiving;
            }
            else
            {
                webArchivingLink = null;
            }

            ExtendedMetadata meta = new()
            {
                Type = JudgmentType.Decision,
                Court = court,
                Jurisdictions = jurisdictions,
                Date = new WNamedDate { Date = line.DecisionDate, Name = "decision" },
                Name = line.FirstPartyName + " v " + line.respondent,
                CaseNumbers = [line.CaseNo],
                Parties = [
                    new UK.Gov.NationalArchives.CaseLaw.Model.Party { Name = line.FirstPartyName, Role = line.FirstPartyRole },
                    new UK.Gov.NationalArchives.CaseLaw.Model.Party { Name = line.respondent, Role = PartyRole.Respondent }
                ],
                SourceFormat = sourceFormat,
                Categories = [.. categories],
                NCN = line.ncn,
                WebArchivingLink = webArchivingLink
            };
            return meta;
        }

    }

}
