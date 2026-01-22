
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.NationalArchives.CaseLaw.Model;

namespace UK.Gov.Legislation.Judgments.Parse {

class WMetadata2 : WMetadata {

    private readonly IOutsideMetadata meta2;

    internal WMetadata2(MainDocumentPart main, Judgment judgment, IOutsideMetadata meta2) : base(main, judgment, meta2 is null ? Enumerable.Empty<IExternalAttachment>() : meta2.Attachments) {
        this.meta2 = meta2;
    }

    override public Court? Court => base.Court ?? meta2.Court;

    override public int? Year { get {
        return base.Year ?? meta2.Year;
    } }

    override public int? Number { get {
        return base.Number ?? meta2.Number;
    } }

    override public string Cite { get {
        return base.Cite ?? meta2.Cite;
    } }

    override public string ShortUriComponent { get {
        if (meta2.UriTrumps)
            return meta2.ShortUriComponent;
        return base.ShortUriComponent ?? meta2.ShortUriComponent;
    } }

    override public INamedDate Date { get {
        return base.Date ?? new WNamedDate { Date = meta2.Date, Name = MakeDateName(Court) };
    } }

    internal static string MakeDateName(Court? court) {
        if (!court.HasValue)
            return "judgment";
        if (court.Value.Code.StartsWith("UKUT"))
            return "decision";
        if (court.Value.Code.StartsWith("UKFTT") || court.Value.Code.StartsWith("FTT"))
            return "decision";
        return "judgment";
    }

    override public string Name { get {
        if (meta2.NameTrumps)
            return meta2.Name;
        return base.Name ?? meta2.Name;
    } }

}

class WMetadata3 : WMetadata, IMetadataExtended {

    private readonly IOutsideMetadata outside;

    internal WMetadata3(MainDocumentPart main, Judgment judgment, IOutsideMetadata outside) : base(main, judgment, outside is null ? Enumerable.Empty<IExternalAttachment>() : outside.Attachments) {
        this.outside = outside;
    }

    override public Court? Court { get => outside.Court ?? base.Court; }

    override public IEnumerable<IDocJurisdiction> Jurisdictions
    {
        get
        {
            if (outside.JurisdictionShortNames.Count == 0)
                return base.Jurisdictions;

            // Enumerate array to speed up searches by preventing downstream calculations reoccurring
            var jurisdictionsFoundInDoc = base.Jurisdictions.ToArray();

            var areAllJurisdictionsFoundInDocInOutsideMetadata = jurisdictionsFoundInDoc
                .All(jurisdictionFromDoc => outside.JurisdictionShortNames.Contains(jurisdictionFromDoc.ShortName));

            if (!areAllJurisdictionsFoundInDocInOutsideMetadata)
                throw new MetadataConflictException("Jurisdictions found in document are missing in supplied outside metadata");

            // Combine outside and doc-provided metadata, returning jurisdictions found in doc where possible
            var combinedJurisdictions = new List<IDocJurisdiction>();
            foreach (var outsideJurisdictionShortName in outside.JurisdictionShortNames) {
                var match = jurisdictionsFoundInDoc.SingleOrDefault(j => j.ShortName == outsideJurisdictionShortName);

                combinedJurisdictions.Add(match ?? new OutsideJurisdiction{ShortName = outsideJurisdictionShortName});
            }
            return combinedJurisdictions;
        }
    }

    override public int? Year { get {
        return outside.Year ?? base.Year;
    } }

    override public int? Number { get {
        return outside.Number ?? base.Number;
    } }

    override public string Cite { get {
        return outside.Cite ?? base.Cite;
    } }

    override public string ShortUriComponent { get => outside.ShortUriComponent ?? base.ShortUriComponent; }

    override public INamedDate Date { get {
        if (outside.Date is null)
            return base.Date;
        return new WNamedDate { Date = outside.Date, Name = WMetadata2.MakeDateName(Court) };
    } }

    override public string Name { get {
        return outside.Name ?? base.Name;
    } }

    public string SourceFormat => outside.SourceFormat;

    public List<string> CaseNumbers => outside.CaseNumbers;

    override public IEnumerable<string> CaseNos() {
        if (outside?.CaseNumbers != null && outside.CaseNumbers.Count != 0)
            return outside.CaseNumbers;
        return base.CaseNos();
    }

    public List<UK.Gov.NationalArchives.CaseLaw.Model.Party> Parties => outside.Parties;

    public List<ICategory> Categories => outside.Categories;

    public string NCN => outside.NCN;

    public string WebArchivingLink => outside.WebArchivingLink;
}

internal class OutsideJurisdiction : IDocJurisdiction
{
    public IEnumerable<IInline> Contents => throw new NotSupportedException();
    public string Id => throw new NotSupportedException();
    public string LongName => throw new NotSupportedException();
    public string ShortName { get; init; }
    
    public bool Overridden => true;
}
}
