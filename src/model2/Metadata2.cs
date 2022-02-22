

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Judgments.Parse {

class WMetadata2 : WMetadata {

    private readonly IOutsideMetadata meta2;

    internal WMetadata2(MainDocumentPart main, Judgment judgment, IOutsideMetadata meta2) : base(main, judgment, meta2 is null ? Enumerable.Empty<IExternalAttachment>() : meta2.Attachments) {
        this.meta2 = meta2;
    }

    override public Court? Court() {
        return base.Court() ?? meta2.Court;
    }

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

    override public string Date() {
        return base.Date() ?? meta2.Date;
    }

    override public string CaseName { get {
        if (meta2.NameTrumps)
            return meta2.Name;
        return base.CaseName ?? meta2.Name;
    } }

}

class WMetadata3 : WMetadata {

    private readonly IOutsideMetadata outside;

    internal WMetadata3(MainDocumentPart main, Judgment judgment, IOutsideMetadata outside) : base(main, judgment, outside is null ? Enumerable.Empty<IExternalAttachment>() : outside.Attachments) {
        this.outside = outside;
    }

    override public Court? Court() {
        return outside.Court ?? base.Court();
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

    override public string Date() {
        return outside.Date ?? base.Date();
    }

    override public string CaseName { get {
        return outside.Name ?? base.CaseName;
    } }

}

}
