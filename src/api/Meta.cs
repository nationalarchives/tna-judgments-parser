
using System.Collections.Generic;

using UK.Gov.NationalArchives.CaseLaw.Model;

namespace UK.Gov.NationalArchives.Judgments.Api;

public class Meta {

    public string DocumentType { get; set; }

    public string Uri { get; set; }

    public string Court { get; set; }

    public List<string> JurisdictionShortNames { get; set; } = [];

    public string Cite { get; set; }

    public string Date { get; set; }

    public string Name { get; set; }

    public Extensions Extensions { get; set; }

    public IEnumerable<ExternalAttachment> Attachments { get; set; }

}

public class Extensions {

    public string SourceFormat { get; set; }

    public List<string> CaseNumbers { get; set; }

    public List<UK.Gov.NationalArchives.CaseLaw.Model.Party> Parties { get; set; }

    public List<ICategory> Categories { get; set; }

    public string NCN { get; set; }

    public required string WebArchivingLink { get; init; }
}

public class ExternalAttachment {

    public string Name { get; set; }

    public string Link { get; set; }

}
