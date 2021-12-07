

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Judgments.Parse {

class WMetadata2 : WMetadata {

    private readonly IOutsideMetadata meta2;

    internal WMetadata2(MainDocumentPart main, Judgment judgment, IOutsideMetadata meta2) : base(main, judgment) {
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

    override public string DocumentId() {
        if (meta2.IdTrumps)
            return meta2.Id;
        return base.DocumentId() ?? meta2.Id;
    }

    override public string Date() {
        return base.Date() ?? meta2.Date;
    }

    override public string CaseName { get {
        if (meta2.NameTrumps)
            return meta2.Name;
        return base.CaseName ?? meta2.Name;
    } }

    override public IEnumerable<IExternalAttachment> ExternalAttachments { get => meta2.Attachments; }

}

}
