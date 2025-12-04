
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace Backlog.Src
{
    /// <summary>
    /// Custom validation attribute to ensure subcategories can only exist if their parent category is defined
    /// </summary>
    public class CategoryValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value is Metadata.Line line)
            {
                try
                {
                    line.ValidateCategoryRules();
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
            return true;
        }

        public override string FormatErrorMessage(string name)
        {
            return "Subcategory columns can only exist if their main category is defined, and exactly one of claimants or appellants must be provided";
        }
    }

    class Metadata
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
            }
        }

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
            
            private readonly string DateFormat = "yyyy-MM-dd HH:mm:ss";
            internal string DecisionDate { get => System.DateTime.ParseExact(decision_datetime, DateFormat, CultureInfo.InvariantCulture).ToString("yyyy-MM-dd"); }

            /// <summary>
            /// Validates that subcategory columns can only exist if their main category is defined.
            /// Also validates that only one of claimants or appellants is provided, but not both.
            /// </summary>
            /// <exception cref="ArgumentException">Thrown when a subcategory exists without its parent category or when both claimants and appellants are provided</exception>
            internal void ValidateCategoryRules()
            {
                // Check if main_subcategory exists without main_category
                if (!string.IsNullOrWhiteSpace(main_subcategory) && string.IsNullOrWhiteSpace(main_category))
                {
                    throw new ArgumentException($"Line {id}: main_subcategory '{main_subcategory}' cannot exist without main_category being defined");
                }

                // Check if sec_subcategory exists without sec_category
                if (!string.IsNullOrWhiteSpace(sec_subcategory) && string.IsNullOrWhiteSpace(sec_category))
                {
                    throw new ArgumentException($"Line {id}: sec_subcategory '{sec_subcategory}' cannot exist without sec_category being defined");
                }

                // Check that exactly one of claimants or appellants is provided
                bool hasClaimants = !string.IsNullOrWhiteSpace(claimants);
                bool hasAppellants = !string.IsNullOrWhiteSpace(appellants);

                if (hasClaimants && hasAppellants)
                {
                    throw new ArgumentException($"Line {id}: Cannot have both claimants and appellants. Please provide only one.");
                }

                if (!hasClaimants && !hasAppellants)
                {
                    throw new ArgumentException($"Line {id}: Must have either claimants or appellants. At least one is required.");
                }
            }

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

        internal static List<Line> Read(string csvPath)
        {
            using var streamReader = new StreamReader(csvPath);
            return Read(streamReader);
        }

        internal static List<Line> Read(TextReader textReader)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                ShouldSkipRecord = args => false
            };
            using var csv = new CsvReader(textReader, config);
            
            csv.Context.RegisterClassMap<LineMap>();
            
            var records = new List<Line>();
            
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
                    
                    if (!Validator.TryValidateObject(record, validationContext, validationResults, true))
                    {
                        var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
                        throw new ArgumentException($"Validation failed: {errors}");
                    }
                    
                    records.Add(record);
                }
                catch (ArgumentException ex)
                {
                    throw new CsvHelper.CsvHelperException(csv.Context, $"CSV validation error at row {csv.Context.Parser.Row}: {ex.Message}", ex);
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
