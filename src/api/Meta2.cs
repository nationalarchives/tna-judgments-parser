
using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.CaseLaw.Model;

namespace UK.Gov.NationalArchives.Judgments.Api;

internal class MetaWrapper : IOutsideMetadata {

    internal Meta Meta { get; init; }

    public string ShortUriComponent => URI.ExtractShortURIComponent(Meta.Uri);

    public bool UriTrumps => Meta.Uri is not null;

    public Court? Court { get {
        if (Meta.Court is null)
            return null;
        if (!Courts.Exists(Meta.Court))
            return null;
        return Courts.GetByCode(Meta.Court);
    } }

    public List<string> JurisdictionShortNames => Meta.JurisdictionShortNames;

    public int? Year { get {
        if (ShortUriComponent is null)
            return null;
        try { // necessary ?
            return Citations.YearFromUriComponent(ShortUriComponent);
        } catch (Exception) {
            return null;
        }
    } }

    public int? Number { get {
        if (ShortUriComponent is null)
            return null;
        try { // necessary ?
            return Citations.NumberFromUriComponent(ShortUriComponent);
        } catch (Exception) {
            return null;
        }
    } }

    public string Cite => Meta.Cite; // validation?

    public string Date => Meta.Date; // validation?

    public string Name => Meta.Name;

    public bool NameTrumps => Meta.Name is not null;

    public IEnumerable<IExternalAttachment> Attachments => Meta.Attachments?.Select(a => new ExternalAttachmentWrapper(a) );

    /* */

    public string SourceFormat => Meta.Extensions?.SourceFormat;

    public List<string> CaseNumbers => Meta.Extensions?.CaseNumbers ?? [];

    public List<UK.Gov.NationalArchives.CaseLaw.Model.Party> Parties => Meta.Extensions?.Parties ?? [];

    public List<ICategory> Categories => Meta.Extensions?.Categories ?? [];

    public string NCN => Meta.Extensions?.NCN;

    public string WebArchivingLink => Meta.Extensions?.WebArchivingLink;
}

internal class ExternalAttachmentWrapper : IExternalAttachment {

    private ExternalAttachment Attachment { get; init; }

    internal ExternalAttachmentWrapper(ExternalAttachment attachment) {
        Attachment = attachment;
    }

    public string Type => Attachment.Name;

    public string Link => Attachment.Link;

}
