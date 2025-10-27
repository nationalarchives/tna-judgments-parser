#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static  UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Date;
public abstract partial record DocDate() : IBuildable<XNode>
{
    public abstract XNode? Build(Document document);


    // Only dates of the format "d MMMM yyyy" with or without an ordinal suffix will parse successfully
    // or just a year "yyyy"
    // e.g. "17th June 2025", "9 October 2021", "2025"
    // Any other format will result in the date attribute being set to "9999-01-01"
    private static readonly string[] locales = ["en-GB", "cy-GB"];
    private static readonly Dictionary<string, string> formats = new() {
            { "d MMMM yyyy", "d'th' MMMM yyyy" },
            { "yyyy", "yyyy"},
    };


    public static DocDate ToDate(string? text, ReferenceKey key)
    {
        if (string.IsNullOrEmpty(text)) return new NoDate();
        // Remove ordinal suffix from date if there is one
        Match match = OrdinalPostfix().Match(text);
        if (match.Success)
        {
            // Extract the numeric day and remove the suffix from the original string
            text = text.Replace(match.Value, match.Groups[1].Value);
        }

        if (PlaceholderRegex().IsMatch(text))
        {
            return new PlaceholderDate();
        }

        foreach (string locale in locales)
        {
            foreach (string format in formats.Keys)
            {
                if (DateTime.TryParseExact(
                    text,
                    format,
                    CultureInfo.GetCultureInfo(locale),
                    DateTimeStyles.None,
                    out DateTime dateTime))
                {
                    return new ValidDate(dateTime, formats[format], key);
                }
            }
        }
        if (string.IsNullOrEmpty(text?.Trim()))
        {
            return new NoDate();
        } else
        {
            return new UnknownDate(text);
        }
    }

    // "***" is a placeholder for dates
    // We check for 2 or more for safety
    [GeneratedRegex( @"\*\*\**")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"(\d+)(st|nd|rd|th)")]
    private static partial Regex OrdinalPostfix();
};

record PlaceholderDate() : DocDate
{
    public override XNode? Build(Document _) =>
        new XElement(akn + "docDate",
            new XAttribute("date", "9999-01-01")
        );
};

record ValidDate(DateTime Date, string Format, ReferenceKey Key) : DocDate
{
    public override XNode Build(Document document)
    {
        Reference dateRef = document.Metadata
            .Register(new Reference(Key, Date.ToString("o", System.Globalization.CultureInfo.InvariantCulture)));
        return new XElement(akn + "docDate",
            new XAttribute("date", Date.ToString("yyyy-MM-dd")),
            new XElement(akn + "ref",
                new XAttribute(ukl + "dateFormat", Format),
                new XAttribute(akn + "class", "#placeholder"),
                new XAttribute("href", $"#{dateRef.EId}")));
    }

};

record NoDate() : DocDate
{
    public override XNode? Build(Document _) =>
        new XElement(akn + "docDate",
            new XAttribute("date", "9999-01-01")
        );
};

record UnknownDate(string Text) : DocDate
{
    public override XNode Build(Document _) =>
        new XElement(akn + "docDate",
            new XAttribute("date", "9999-01-01")
        );
}
