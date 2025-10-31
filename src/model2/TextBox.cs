
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Packaging;
using Vml = DocumentFormat.OpenXml.Vml;

using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.NationalArchives.WordImpl {

internal class WTextBox {

    internal List<WLine> Lines { get; init; }

    internal static WTextBox Extract(MainDocumentPart main, Picture pict) {
        Vml.Shape shape = pict.ChildElements.OfType<Vml.Shape>().FirstOrDefault();
        if (shape is not null)
            return Extract(main, shape);
        return null;
    }

    internal static WTextBox Extract(MainDocumentPart main, Vml.Shape shape) {
        if (shape.FirstChild is Vml.TextBox textbox)
            return Extract(main, textbox);
        return null;
    }

    internal static WTextBox Extract(MainDocumentPart main, Vml.TextBox textbox) {
        if (textbox.FirstChild is TextBoxContent txbxContent)
            return Extract(main, txbxContent);
        return null;
    }

    internal static WTextBox Extract(MainDocumentPart main, TextBoxContent txbxContent) {
        IEnumerable<WLine> lines = txbxContent.ChildElements.OfType<Paragraph>().Select(p => PreParser.ParseParagraph(null, null, null)); // TODO
        if (!lines.Any())
            return null;
        return new WTextBox { Lines = lines.ToList() };
    }

}

}
