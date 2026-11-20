#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

using CsvHelper;

using TRE.Metadata.MetadataFieldTypes;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.CaseLaw.Model;

using Party = UK.Gov.NationalArchives.CaseLaw.Model.Party;

namespace Backlog.Csv;

[CsvRecord]
[AppellantsOrClaimantsPresentValidation]
[CategoryValidation]
internal partial record CsvLine
{
    [CsvIgnore]
    public (string Name, string Hash) CsvProperties { get; set; }

    [CsvIgnore]
    public Dictionary<string, string> FullCsvLineContents { get; set; } = [];

    [Required(AllowEmptyStrings = false)]
    public required string id { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string Court { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string FilePath { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string Extension { get; set; }

    [CsvConverter(typeof(DecisionDateConverter))]
    public required DateTime DecisionDateTime { get; set; }

    [CsvColumn(Optional = true)]
    [CsvConverter(typeof(DelimitedArrayConverter))]
    public string[] CaseNo { get; set; } = [];

    [CsvColumn(Optional = true)]
    [CsvConverter(typeof(DelimitedArrayConverter))]
    public string[] Jurisdictions { get; set; } = [];

    [CsvColumn(Optional = true)]
    public string? Claimants { get; set; }

    [CsvColumn(Optional = true)]
    public string? Appellants { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string Respondent { get; set; }

    [CsvColumn(Optional = true)]
    public string? MainCategory { get; set; }

    [CsvColumn(Optional = true)]
    public string? MainSubcategory { get; set; }

    [CsvColumn(Optional = true)]
    public string? SecCategory { get; set; }

    [CsvColumn(Optional = true)]
    public string? SecSubcategory { get; set; }

    [CsvColumn(Optional = true)]
    public string? Ncn { get; set; }

    [CsvColumn(Optional = true)]
    public string? HeadnoteSummary { get; set; }

    [CsvColumn(Optional = true)]
    public string? WebArchiving { get; set; }

    [Required]
    public required Guid Uuid { get; set; }

    [CsvConverter(typeof(BooleanSkipConverter))]
    public bool Skip { get; set; }

    /// <summary>
    /// NCN with zeros removed
    /// </summary>
    [CsvIgnore]
    internal string? CleanedNcn
    {
        get
        {
            if (Ncn is null)
                return null;

            return field ??= Regex.Replace(Ncn, " 0+", " ");
        }
    }

    /// <summary>
    ///     Gets the name of the first party (either claimants or appellants)
    /// </summary>
    [CsvIgnore]
    internal string FirstPartyName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Claimants))
            {
                return Claimants;
            }

            if (!string.IsNullOrWhiteSpace(Appellants))
            {
                return Appellants;
            }

            throw new InvalidOperationException("No first party (claimants or appellants) is defined");
        }
    }

    /// <summary>
    ///     Gets the role of the first party (either Claimant or Appellant)
    /// </summary>
    [CsvIgnore]
    internal PartyRole FirstPartyRole
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Claimants))
            {
                return PartyRole.Claimant;
            }

            if (!string.IsNullOrWhiteSpace(Appellants))
            {
                return PartyRole.Appellant;
            }

            throw new InvalidOperationException("No first party (claimants or appellants) is defined");
        }
    }

    [CsvIgnore]
    public Party[] Parties =>
    [
        (appellants: Appellants, claimants: Claimants) switch
        {
            (appellants: null, claimants: not null) => new Party { Name = Claimants, Role = PartyRole.Claimant },
            (appellants: not null, claimants: null) => new Party { Name = Appellants, Role = PartyRole.Appellant },
            _ => throw new InvalidOperationException()
        },
        new() { Name = Respondent, Role = PartyRole.Respondent }
    ];

    [CsvIgnore]
    public ICategory[] Categories
    {
        get
        {
            List<ICategory> categories = [];

            if (MainCategory is not null)
            {
                categories.Add(new Category { Name = MainCategory });

                if (MainSubcategory is not null)
                {
                    categories.Add(new Category { Name = MainSubcategory, Parent = MainCategory });
                }
            }

            if (SecCategory is not null)
            {
                categories.Add(new Category { Name = SecCategory });

                if (SecSubcategory is not null)
                {
                    categories.Add(new Category { Name = SecSubcategory, Parent = SecCategory });
                }
            }

            return categories.ToArray();
        }
    }

    /// <summary>
    ///     The original file name extracted from FilePath
    ///     Use this instead of Path.GetFileName for csv lines because it handles both '/' and '\' path separators
    /// </summary>
    [CsvIgnore]
    public string FileName
    {
        get
        {
            var pathSeparator = FilePath.Contains('/') ? '/' : '\\';

            var pathParts = FilePath.Split(pathSeparator);

            return pathParts[^1];
        }
    }
}
