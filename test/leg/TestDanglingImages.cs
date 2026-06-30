using System.Linq;
using System.Xml;

using Xunit;

using test;

namespace UK.Gov.Legislation.ImpactAssessments.Test {

/// <summary>
/// Regression guard for dangling image references (LEG-162). ukia_20250013 carries a
/// sign-off WMF in its header that cannot be converted to a web format; the image must
/// be dropped cleanly rather than left as a raw <c>&lt;img src="image3.wmf"&gt;</c>.
/// Every surviving <c>&lt;img&gt;</c> in the AKN must point at an uploaded
/// legislation.gov.uk image URL.
/// </summary>
public class TestDanglingImages {

    [Theory]
    [InlineData("ukia_20250013_en")]  // unconvertible sign-off WMF in the header
    public void EveryImageRefIsAnUploadedUrl(string filename) {
        var docx = DocumentHelpers.ReadDocx($"test.leg.ia.original_filenames.{filename}.docx");
        var parsed = Helper.Parse(docx, filename + ".docx",
            renderer: UK.Gov.Legislation.Test.LocalRendererHelper.GetOrNull());

        var xml = new XmlDocument();
        xml.LoadXml(parsed.Serialize());
        var ns = new XmlNamespaceManager(xml.NameTable);
        ns.AddNamespace("akn", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");

        var srcs = xml.SelectNodes("//akn:img/@src", ns).Cast<XmlAttribute>()
            .Select(a => a.Value).ToList();

        Assert.NotEmpty(srcs);  // the document genuinely has images, so the guard is meaningful
        Assert.All(srcs, src => Assert.StartsWith("http://www.legislation.gov.uk/", src));
    }
}

}
