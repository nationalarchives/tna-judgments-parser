using System.Xml;
using System.Xml.Linq;

namespace UK.Gov.Legislation.Lawmaker;

/// <summary>
/// Extension methods for XML handling.
/// </summary>
/// <remarks>
/// Also includes some useful utilities such as commonly used namespaces.
/// </remarks>
public static class XmlExt
{

    /// <summary>
    /// Converts an <c>Xml.Linq.XNode</c> to an <x>Xml.XmlNode</c>, using <c>ownerDocument</c>
    /// </summary>
    /// <param name="el">The <c>XNode</c> to convert.</param>
    /// <param name="ownerDocument">The document the new <c>XmlNode</c> is in.</param>
    /// <returns>The imported <c>XmlNode</c></returns>
    /// <remarks>
    /// This method does not add the the node to DOM, but does allow it to be added.
    /// </remarks
    public static XmlNode ToXmlNode(this XNode el, XmlDocument ownerDocument)
    {
        return ownerDocument.ReadNode(el.CreateReader());
    }
}

public static class XmlNamespaces
{
    public static readonly XNamespace akn = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";
    public static readonly XNamespace ukl = "https://www.legislation.gov.uk/namespaces/UK-AKN";
    public static readonly XNamespace html = "http://www.w3.org/1999/xhtml";
}