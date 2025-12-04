
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

    List<string> JurisdictionShortNames { get; }

    int? Year { get; }

    int? Number { get; }

    string Cite { get; }

    string Date { get; }

    string Name { get; }

    bool NameTrumps { get; }

    IEnumerable<IExternalAttachment> Attachments { get; }

    /* */

    string SourceFormat { get; }

    List<string> CaseNumbers { get; }

    List<UK.Gov.NationalArchives.CaseLaw.Model.Party> Parties { get; }

    List<ICategory> Categories { get; }

    string NCN { get; }


}

}
