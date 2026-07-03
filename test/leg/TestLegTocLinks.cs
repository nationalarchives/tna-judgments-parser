using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

using Xunit;

using test;

namespace UK.Gov.Legislation.AssociatedDocuments.Test {

/// <summary>
/// Asserts that every <c>#fragment</c> href in an AKN's <c>&lt;toc&gt;</c>
/// resolves to an <c>eId="..."</c> on some element in the same AKN. This is
/// the source-of-truth check: downstream renderers (legislation.gov.uk,
/// search indexes, anyone consuming the AKN) all expect TOC fragments to
/// match body eIds. The XSL deliberately suppresses inline TOC rendering
/// in our local HTML preview, so we check at the AKN layer.
///
/// Companion test that runs at the HTML layer (TocFragmentLinksResolveInHtml
/// below) catches the rarer XSL-side regression where eIds in the body
/// fail to surface as HTML ids — needed for footnote refs and any other
/// in-page hrefs the XSL does emit.
///
/// Runs on every leg AKN fixture — IA, EM, EN, CoP, OD, TN.
/// </summary>
public class TestLegTocLinks {

    public static readonly IEnumerable<object[]> AllLegFixtures = GetAllLegFixtures();

    private static IEnumerable<object[]> GetAllLegFixtures() {
        var assembly = Assembly.GetExecutingAssembly();
        // Match any embedded .akn under test.leg.* (each leg doc type uses its own subfolder).
        var regex = new Regex(@"^(test\.leg\.[a-z_]+(?:\.original_filenames)?\.[a-zA-Z0-9_]+)\.akn$");
        return assembly.GetManifestResourceNames()
            .Select(name => regex.Match(name))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .OrderBy(name => name)
            .Select(name => new object[] { name });
    }

    // href="urlPart#frag" — captures (urlPart, frag) so we can distinguish
    // self-links from external citations.
    private static readonly Regex HrefFragmentRegex = new(
        @"href\s*=\s*""([^""#]*)#([^""]+)""", RegexOptions.Compiled);
    // id="value"
    private static readonly Regex IdRegex = new(
        @"id\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    [Theory]
    [MemberData(nameof(AllLegFixtures))]
    public void TocFragmentsResolveInAkn(string resourceBase) {
        string aknText = DocumentHelpers.ReadXml($"{resourceBase}.akn");
        var akn = new XmlDocument();
        akn.LoadXml(aknText);
        var ns = new XmlNamespaceManager(akn.NameTable);
        ns.AddNamespace("akn", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");

        // Every fragment referenced from a tocItem href.
        var fragments = new HashSet<string>();
        foreach (XmlElement item in akn.SelectNodes("//akn:tocItem[@href]", ns)) {
            string href = item.GetAttribute("href");
            int hash = href.IndexOf('#');
            if (hash < 0) continue;
            string frag = href.Substring(hash + 1);
            if (frag.Length > 0)
                fragments.Add(frag);
        }

        // Every eId in the AKN.
        var eIds = new HashSet<string>();
        foreach (XmlElement el in akn.SelectNodes("//*[@eId]"))
            eIds.Add(el.GetAttribute("eId"));

        var unresolved = fragments.Where(f => !eIds.Contains(f)).OrderBy(s => s).ToList();
        Assert.True(unresolved.Count == 0,
            $"{resourceBase}: {unresolved.Count} TOC fragment(s) without matching eId: " +
            $"{string.Join(", ", unresolved.Take(10))}" +
            (unresolved.Count > 10 ? $" (+{unresolved.Count - 10} more)" : ""));
    }

    [Theory]
    [MemberData(nameof(AllLegFixtures))]
    public void TocFragmentsResolveInHtml(string resourceBase) {
        if (!HtmlBuilder.IsAvailable())
            Assert.Skip("HtmlBuilder unavailable (Oxygen/Saxon not installed). Set OXYGEN_HOME to enable.");

        string aknText = DocumentHelpers.ReadXml($"{resourceBase}.akn");
        var akn = new XmlDocument();
        akn.LoadXml(aknText);

        string html = HtmlBuilder.Build(akn);

        // The document's own canonical URL — anything matching it is a self-link.
        var ns = new XmlNamespaceManager(akn.NameTable);
        ns.AddNamespace("akn", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
        string selfUri = (akn.SelectSingleNode("//akn:FRBRExpression/akn:FRBRuri/@value", ns) as XmlAttribute)?.Value ?? "";

        // Scan rendered HTML for any self-referencing <a href="...#frag"> and
        // assert each fragment lands on an id="...". External citation URLs
        // (e.g. https://www.ons.gov.uk/.../article#economic-activity) are
        // skipped because their fragments belong to a different document.
        var fragments = HrefFragmentRegex.Matches(html)
            .Where(m => {
                string urlPart = m.Groups[1].Value;
                // Self-link: empty URL part or matches our own canonical URL.
                return urlPart.Length == 0 || urlPart == selfUri;
            })
            .Select(m => m.Groups[2].Value)
            .Where(f => !string.IsNullOrEmpty(f))
            .ToHashSet();
        var ids = IdRegex.Matches(html)
            .Select(m => m.Groups[1].Value)
            .ToHashSet();

        var unresolved = fragments.Where(f => !ids.Contains(f)).OrderBy(s => s).ToList();
        Assert.True(unresolved.Count == 0,
            $"{resourceBase}: {unresolved.Count} HTML fragment(s) without matching id: " +
            $"{string.Join(", ", unresolved.Take(10))}" +
            (unresolved.Count > 10 ? $" (+{unresolved.Count - 10} more)" : ""));
    }
}

}
