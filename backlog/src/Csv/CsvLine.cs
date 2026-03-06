using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using CsvHelper.Configuration.Attributes;

using UK.Gov.Legislation.Judgments;

namespace Backlog.Csv;

[AppellantsOrClaimantsPresentValidation]
[CategoryValidation]
internal record CsvLine
{
    public Dictionary<string, string> FullCsvLineContents { get; set; }

    public string id { get; set; }
    public string court { get; set; }
    [Required(AllowEmptyStrings = false)]
    public string FilePath { get; set; }
    [Required(AllowEmptyStrings = false)]
    public string Extension { get; set; }
    public DateTime decision_datetime { get; set; }
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
