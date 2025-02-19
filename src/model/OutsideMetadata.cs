
using System.Collections.Generic;

using UK.Gov.NationalArchives.CaseLaw.Model;

namespace UK.Gov.Legislation.Judgments {

interface IExternalAttachment {

    string Type { get; }

    string Link { get; }

}

interface IOutsideMetadata {

    string ShortUriComponent { get; }

    bool UriTrumps { get; }

    Court? Court { get; }

    int? Year { get; }

    int? Number { get; }

    string Cite { get; }

    string Date { get; }

    string Name { get; }

    bool NameTrumps { get; }

    IEnumerable<IExternalAttachment> Attachments { get; }

    /* */

    public string SourceFormat { get; }

    public List<string> CaseNumbers { get; }

    public List<UK.Gov.NationalArchives.CaseLaw.Model.Party> Parties { get; }

    public List<ICategory> Categories { get; }


}

}
