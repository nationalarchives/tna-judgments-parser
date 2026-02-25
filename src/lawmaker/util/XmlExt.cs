using System;
using System.Xml;
using System.Xml.Linq;

using UK.Gov.Legislation.Judgments;

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

    /// <summary>
    /// Converts an <see cref="AlignmentValues"/> value to the corresponding
    /// XML <c>class</c> attribute value used in Lawmaker AKN output.
    /// </summary>
    /// <remarks>
    /// The returned value is lowercase. Note that
    /// <see cref="AlignmentValues.Center"/> is serialised as <c>"centre"</c>
    ///  to match the required spelling in the Lawmaker AKN schema.
    /// </remarks>
    /// <param name="alignment">
    /// The alignment value to convert.
    /// </param>
    /// <returns>
    /// A lowercase string suitable for use as an XML <c>class</c> attribute value.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="alignment"/> is not a defined <see cref="AlignmentValues"/> value.
    /// </exception>
    public static string ToXmlClassValue(this AlignmentValues alignment)
    {
        return alignment switch
        {
            AlignmentValues.Left => "left",
            AlignmentValues.Right => "right",
            AlignmentValues.Center => "centre",
            AlignmentValues.Justify => "justify",
            _ => throw new ArgumentOutOfRangeException(nameof(alignment))
        };
    }

}

public static class XmlNamespaces
{
    public static readonly XNamespace akn = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";
    public static readonly XNamespace ukl = "https://www.legislation.gov.uk/namespaces/UK-AKN";
    public static readonly XNamespace html = "http://www.w3.org/1999/xhtml";
}
