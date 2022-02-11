
using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.NationalArchives.Judgments.Api {

internal class MetaWrapper : IOutsideMetadata {

    internal Meta Meta { get; init; }

    public string Id => Meta.Uri;

    public bool IdTrumps => Meta.Uri is not null;

    public Court? Court { get {
        if (Meta.Court is null)
            return null;
        if (!Courts.ByCode.ContainsKey(Meta.Court))
            return null;
        return Courts.ByCode[Meta.Court];
    } }

    public static int? ExtractYearFromUri(string uri) {
        if (uri is null)
            return null;
        string[] components = uri.Split('/');
        try {
            string year = components[components.Length - 2];
            return int.Parse(year);
        } catch (Exception) {
            return null;
        }
    }
    public static int? ExtractNumberFromUri(string uri) {
        if (uri is null)
            return null;
        string[] components = uri.Split('/');
        try {
            string year = components[components.Length - 1];
            return int.Parse(year);
        } catch (Exception) {
            return null;
        }
    }

    public int? Year { get {
        if (Meta.Uri is null)
            return null;
        string[] components = Meta.Uri.Split('/');
        try {
            string year = components[components.Length - 2];
            return int.Parse(year);
        } catch (Exception) {
            return null;
        }
    } }

    public int? Number { get {
        if (Meta.Uri is null)
            return null;
        string[] components = Meta.Uri.Split('/');
        try {
            string year = components[components.Length - 1];
            return int.Parse(year);
        } catch (Exception) {
            return null;
        }
    } }

    public string Cite => Meta.Cite;

    public string Date => Meta.Date;

    public string Name => Meta.Name;

    public bool NameTrumps => Meta.Name is not null;

    public IEnumerable<IExternalAttachment> Attachments => Meta.Attachments?.Select(a => new ExternalAttachmentWrapper(a) );

}

internal class ExternalAttachmentWrapper : IExternalAttachment {

    private ExternalAttachment Attachment { get; init; }

    internal ExternalAttachmentWrapper(ExternalAttachment attachment) {
        Attachment = attachment;
    }

    public string Type => Attachment.Name;

    public string Link => Attachment.Link;

}

}
