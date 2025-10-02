using System.Xml;
using System.Xml.Linq;

namespace UK.Gov.Legislation.Lawmaker;
public static class XmlExt
{

    public static readonly XNamespace AknNamespace = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";
    public static readonly XNamespace HtmlNamespace = "http://www.w3.org/1999/xhtml";

    public static XmlNode ToXmlNode(this XNode el, XmlDocument ownerDocument)
    {
        return ownerDocument.ReadNode(el.CreateReader());
    }
}