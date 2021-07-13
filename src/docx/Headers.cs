
using System.Linq;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

class Headers {

   internal static Header GetFirst(MainDocumentPart main) {
        SectionProperties sProps = main.Document.Body.ChildElements.OfType<SectionProperties>().First();
        TitlePage titlePage = sProps.ChildElements.OfType<TitlePage>().FirstOrDefault();
        bool hasFirstPage;
        if (titlePage is null)
            hasFirstPage = false;
        else if (titlePage.Val is null)
            hasFirstPage = true;
        else
            hasFirstPage = titlePage.Val.Value;
        HeaderReference hr;
        if (hasFirstPage)
            hr = sProps.ChildElements.OfType<HeaderReference>().Where(hr => hr.Type.Equals(HeaderFooterValues.First)).FirstOrDefault();
        else
            hr = sProps.ChildElements.OfType<HeaderReference>().Where(hr => hr.Type.Equals(HeaderFooterValues.Default)).FirstOrDefault();
        if (hr is null)
            return null;
        HeaderPart part = (HeaderPart) main.Parts.Where(part => part.RelationshipId == hr.Id).Select(pair => pair.OpenXmlPart).First();
        return part.Header;
    }

}

}
