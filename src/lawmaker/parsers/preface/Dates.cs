#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using static  UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Preface;
record Dates(IEnumerable<DateBlock> DateBlocks) : IBlock, IBuildable<XNode>, IMetadata
{

    internal static XNode BuildDate(WLine Line, string? name, DateTime? date) => new XElement(
        akn + "block",
        name is null
            ? null
            : new XAttribute("name", name),
        new XText(Line.NormalizedContent)
    );

    public XNode Build() =>
        new XElement(akn + "container",
            new XAttribute("name", "dates"),
            DateBlocks.Select(b => b.Build())
        );

    public IEnumerable<Reference> Metadata => DateBlocks.SelectMany(d => d.Metadata);
    public static Dates? Parse(IParser<IBlock> parser) =>
        parser.MatchWhile(
            l => l is not WLine line || !TableOfContents.IsTableOfContentsHeading(line, parser.LanguageService)
                && !Preamble.IsStart(line),

            DateBlock.Parse
            ) is IEnumerable<DateBlock> dates
            && dates.Any() ? new(dates) : null;

}

abstract record DocDate()
{
    public abstract XNode? Build(ReferenceKey? referenceKey);
};

record DatePlaceholder() : DocDate
{
    public override XNode? Build(ReferenceKey? referenceKey) =>
        new XElement(akn + "docDate",
            new XAttribute("date", "9999-01-01")
        );
};

record ValidDate(DateTime Date, string Format) : DocDate
{
    public override XNode? Build(ReferenceKey? referenceKey) =>
        new XElement(akn + "docDate",
            new XAttribute("date", Date.ToString("yyyy-MM-dd")),
            referenceKey is not null
            ? new XElement(akn + "ref",
                new XAttribute(ukl + "dateFormat", Format),
                new XAttribute(akn + "class", "#placeholder"),
                new XAttribute("href", $"#{referenceKey}"))
            : null);
};

record NoDate() : DocDate
{
    public override XNode? Build(ReferenceKey? referenceKey) => null;
};

record UnkownDate(string Text) : DocDate
{
    public override XNode? Build(ReferenceKey? referenceKey) =>
        new XElement(akn + "docDate",
            new XAttribute("date", "9999-01-01")
        );
}



internal abstract partial record DateBlock(
    // WLine Line,
    string Name,
    string SpanText,
    DocDate Date,
    ReferenceKey? EId = null,
    string? Class = null
) : IBlock, IBuildable<XNode>, IMetadata
{
    private static readonly ILogger Logger = Logging.Factory.CreateLogger<DateBlock>();

    public IEnumerable<Reference> Metadata =>
        EId is ReferenceKey key
            && Date is ValidDate validDate
        ? [new Reference(key, validDate.Date.ToString("o", System.Globalization.CultureInfo.InvariantCulture))]
        : [];

    public XNode Build() =>
        new XElement(akn + "block",
            new XAttribute("name", Name),
            Class is null ? null : new XAttribute(akn + "class", Class),
            new XElement(akn + "span",
                new XAttribute("keep", "true"),
                new XText(SpanText + (Date is UnkownDate(string text)
                    ? " " + text
                    : ""))),
            Date.Build(EId));

    public static DateBlock? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is not WLine line) return null;
        (string? spanText, string? dateText) = ExtractSpanAndDate(line.TextContent);

        if (spanText == null)
        {
            return ByStyle(line, line.TextContent, new NoDate());
        }

        DocDate date = ToDate(dateText);

        if ((ByText(parser.LanguageService, spanText, date, line.Style)
            ?? ByStyle(line, spanText, date, line.Style))
            is DateBlock block)
        {
            return block;
        }

        if (date is NoDate)
        {
            // There may be something in the date section that we didn't
            // parse as a date. to avoid omitting it, we just insert it in the
            // span and rely on users to fix it themselves.
            return new OtherDate(line.TextContent, date);
        } else
        {
            return new OtherDate(spanText, date);
        }

    }

    private static (string?, string?) ExtractSpanAndDate(string text)
    {
        MatchCollection? matches = DateBlockRegex().Matches(text);

        if (matches == null || matches.Count == 0)
        {
            return (null, null);
        }

        if (matches.Count > 1)
        {
            Logger.LogWarning("""
            Multiple matches when matching a date block are unexpected!
            Only the first is taken.
            """);
        }

        Match match = matches.First();
        string? spanText = match.Groups["spanText"]?.Value;
        string? date = match.Groups["date"]?.Value;
        return (spanText, date);
    }


    private static DateBlock? ByText(LanguageService ls, string spanText, DocDate date, string? style = null) => spanText switch
    {
        string t when MadeDate.IsMade(ls, t) => new MadeDate(spanText, date),
        string t when LaidDate.IsLaid(ls, t) => new LaidDate(spanText, date),
        string t when CommenceDate.IsCommence(ls, t) => new CommenceDate(spanText, date, style),
        string t when OtherDate.IsKnownOtherDate(ls, t) => new OtherDate(spanText, date),
        _ => null,
    };

    private static DateBlock? ByStyle(WLine line, string spanText, DocDate date, string? style = null) => line switch
    {
        WLine l when MadeDate.IsStyled(l) => new MadeDate(spanText, date),
        WLine l when LaidDate.IsStyled(l) => new LaidDate(spanText, date),
        WLine l when CommenceDate.IsStyled(l) => new CommenceDate(spanText, date, style),
        WLine l when OtherDate.IsStyled(l) => new OtherDate(spanText, date),
        _ => null,
    };


    // Copied and modified from Builder.AddSigBlock - unsure if we should
    // consolidate them at some point
    // Only dates of the format "d MMMM yyyy" with or without an ordinal suffix will parse successfully
    // e.g. "17th June 2025" and "9 October 2021"
    // Any other format will result in the date attribute being set to "9999-01-01"
    private static readonly string[] locales = ["en-GB", "cy-GB"];
    private static readonly Dictionary<string, string> formats = new() {
            { "d MMMM yyyy", "d'th' MMMM yyyy" },
            { "yyyy", "yyyy"},
    };

    private static DocDate ToDate(string? text)
    {
        if (text == null) return new DatePlaceholder();
        // Remove ordinal suffix from date if there is one
        Match match = OrdinalPostfix().Match(text);
        if (match.Success)
        {
            // Extract the numeric day and remove the suffix from the original string
            text = text.Replace(match.Value, match.Groups[1].Value);
        }

        if (PlaceholderRegex().IsMatch(text))
        {
            return new DatePlaceholder();
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
                    return new ValidDate(dateTime, formats[format]);
                }
            }
        }
        if (string.IsNullOrEmpty(text?.Trim()))
        {
            return new NoDate();
        } else
        {
            return new UnkownDate(text);
        }
    }


    // looks for the rest of a date block
    // may have dashes inbetween first text and date at the end
    // it may be worth writing a regex to find the actual dates,
    // something like this?
    // (?<day>\d\d?(th|rd|st|nd))\s+(?<month>(January|Februrary|March|April|May|June|July|August|September|October|November|December))\s+(?<year>\d{2, 4})
    private const string DATE = @"(?<date>[^\s].*$)";

    // we generally always expect the date to be separated from it's text by
    // at least one tab as it's required in Word to achieve the formatting.
    // There may also be dashes between
    private const string SPACE_BETWEEN = @"[^\t\s\-]*\t[\s\-]*";

    // A date will be "***" if the date is missing or not input yet
    // We check for 2 or more for safety
    [GeneratedRegex( @"\*\*\**")]
    private static partial Regex PlaceholderRegex();


    [GeneratedRegex(@"^(?<spanText>[ \w]+)" + SPACE_BETWEEN + DATE)]
    private static partial Regex DateBlockRegex();

    [GeneratedRegex(@"(\d+)(st|nd|rd|th)")]
    private static partial Regex OrdinalPostfix();
}

internal sealed partial record MadeDate(
    // WLine Line,
    string SpanText,
    DocDate Date
) : DateBlock("madeDate", SpanText, Date, ReferenceKey.varMadeDate)//, IBuildable<XNode>
{
    public static bool IsMade(LanguageService languageService, string text) =>
        languageService.IsMatch(text, LanguagePatterns)?.Count > 0;

    private static readonly string[] STYLES = ["Made"];
    public static bool IsStyled(WLine line)
    {
        return STYLES.Any(line.HasStyle);
    }

    [GeneratedRegex(@"^Made")]
    private static partial Regex EnglishRegex();

    [GeneratedRegex(@"^Gwnaed")]
    private static partial Regex WelshRegex();

    private static readonly Dictionary<LanguageService.Lang, IEnumerable<Regex>> LanguagePatterns = new()
    {
        [LanguageService.Lang.ENG] = [ EnglishRegex() ],
        [LanguageService.Lang.CYM] = [ WelshRegex() ]
    };
}

internal sealed partial record LaidDate(
    string SpanText,
    DocDate Date
) : DateBlock("laidDate", SpanText, Date, ReferenceKey.varLaidDate)//, IBuildable<XNode>
{
    public static bool IsLaid(LanguageService languageService, string text) =>
        languageService.IsMatch(text, LanguagePatterns)?.Count > 0;

    private static readonly string[] STYLES = ["Laid", "Negative"];
    public static bool IsStyled(WLine line)
    {
        return STYLES.Any(line.HasStyle);
    }

    [GeneratedRegex(@"^(to be )?Laid before parliament", RegexOptions.IgnoreCase)]
    private static partial Regex EnglishRegex();

    [GeneratedRegex(@"^Gosodwyd gerbron Senedd Cymru", RegexOptions.IgnoreCase)]
    private static partial Regex WelshRegex();

    private static readonly Dictionary<LanguageService.Lang, IEnumerable<Regex>> LanguagePatterns = new()
    {
        [LanguageService.Lang.ENG] = [ EnglishRegex() ],
        [LanguageService.Lang.CYM] = [ WelshRegex() ]
    };

}

internal sealed partial record CommenceDate(
    string SpanText,
    DocDate Date,
    string? Style
) : DateBlock(
    "commenceDate",
    SpanText,
    Date,
    ReferenceKey.varCommenceDate,
    Style switch
    {
        Coming => null,
        ComingC => "commenceClauses",
        _ => null,
    })//, IBuildable<XNode>
{
    public static bool IsCommence(LanguageService languageService, string text) =>
        languageService.IsMatch(text, LanguagePatterns)?.Count > 0;

    private static readonly string[] STYLES = [Coming, ComingC];
    private const string Coming = "Coming";
    private const string ComingC = "ComingC";
    public static bool IsStyled(WLine line) => STYLES.Any(line.HasStyle);


    [GeneratedRegex(@"^Coming into force", RegexOptions.IgnoreCase)]
    private static partial Regex EnglishRegex();

    [GeneratedRegex(@"^Yn dod i rym", RegexOptions.IgnoreCase)]
    private static partial Regex WelshRegex();

    private static readonly Dictionary<LanguageService.Lang, IEnumerable<Regex>> LanguagePatterns = new()
    {
        [LanguageService.Lang.ENG] = [ EnglishRegex() ],
        [LanguageService.Lang.CYM] = [ WelshRegex() ]
    };

}


internal sealed partial record OtherDate(
    string SpanText,
    DocDate Date
) : DateBlock("otherDate", SpanText, Date, ReferenceKey.varOtherDate)//, IBuildable<XNode>
{
    public static bool IsKnownOtherDate(LanguageService languageService, string text) =>
        languageService.IsMatch(text, LanguagePatterns)?.Count > 0;


    private static readonly string[] STYLES = ["Sifted"];
    public static bool IsStyled(WLine line) => STYLES.Any(line.HasStyle);

    [GeneratedRegex(@"^Sift requirements satisfied", RegexOptions.IgnoreCase)]
    private static partial Regex EngSiftReqRegex();

    private static readonly Dictionary<LanguageService.Lang, IEnumerable<Regex>> LanguagePatterns = new()
    {
        [LanguageService.Lang.ENG] = [ EngSiftReqRegex() ],
    };
}