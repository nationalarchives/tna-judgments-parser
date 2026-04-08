#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using CsvHelper.Configuration.Attributes;

using TRE.Metadata.MetadataFieldTypes;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.CaseLaw.Model;

using Party = UK.Gov.NationalArchives.CaseLaw.Model.Party;

namespace Backlog.Csv;

[AppellantsOrClaimantsPresentValidation]
[CategoryValidation]
internal record CsvLine
{
    public (string Name, string Hash) CsvProperties { get; set; }
    public Dictionary<string, string> FullCsvLineContents { get; set; } = [];

    [Required(AllowEmptyStrings = false)]
    public required string id { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string Court { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string FilePath { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string Extension { get; set; }

    public required DateTime DecisionDateTime { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string CaseNo { get; set; }

    [Optional]
    public string[] Jurisdictions { get; set; } = [];

    [Optional]
    public string? Claimants { get; set; }

    [Optional]
    public string? Appellants { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string Respondent { get; set; }

    [Optional]
    public string? MainCategory { get; set; }

    [Optional]
    public string? MainSubcategory { get; set; }

    [Optional]
    public string? SecCategory { get; set; }

    [Optional]
    public string? SecSubcategory { get; set; }

    [Optional]
    public string? Ncn { get; set; }

    [Optional]
    public string? HeadnoteSummary { get; set; }

    [Optional]
    public string? WebArchiving { get; set; }

    [Optional]
    public string? Uuid { get; set; }

    [Default(false)]
    [TypeConverter(typeof(BooleanSkipConverter))]
    public bool Skip { get; set; }

    /// <summary>
    ///     Gets the name of the first party (either claimants or appellants)
    /// </summary>
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
