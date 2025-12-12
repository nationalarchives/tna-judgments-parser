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

namespace UK.Gov.Legislation.Lawmaker.Date;
public record DatesContainer(IEnumerable<DateBlock> DateBlocks) : IBlock, IBuildable<XNode>
{
    // temporary workaround for the broken eId logic in Lawmaker
    internal static int commenceDateOC = 0;
    public XNode? Build(Document document)
    {
        commenceDateOC = 0;
        return new XElement(akn + "container",
            new XAttribute("name", "dates"),
            DateBlocks.Select(b => b.Build(document))
        );
    }

    public static DatesContainer? Parse(IParser<IBlock> parser) =>
        parser.MatchWhile(
            l => l is not WLine line || !TableOfContents.IsTableOfContentsHeading(line, parser.LanguageService)
                && !Preamble.IsStart(line),

            DateBlock.Parse
            ) is IEnumerable<DateBlock> dates
            && dates.Any() ? new(dates) : null;

}



public abstract partial record DateBlock(
    string Name,
    string SpanText,
    DocDate Date,
    string? Class = null
) : IBlock, IBuildable<XNode>
{
    private static readonly ILogger Logger = Logging.Factory.CreateLogger<DateBlock>();

    public XNode? Build(Document document)
    {
        return new XElement(akn + "block",
            this is CommenceDate ? new XAttribute("eId", GetCommenceEId()) : null,
            new XAttribute("name", Name),
            Class is null ? null : new XAttribute(akn + "class", Class),
            new XElement(akn + "span",
                new XAttribute("keep", "true"),
                new XText(SpanText)),
            Date.Build(document));
    }

    // Lamakers eId generation is broken, this is a workaround to ensure the
    // eIds are correct for Lawmaker (until Lawmaker can be fixed)
    public string GetCommenceEId() => "fnt__dates___commenceDate" + this switch
    {
        CommenceDate(_, _, UnknownDate _, _) => "Description",
        CommenceDate(_, _, NoDate _, _) => "Description",
        _ => IncrementCommenceOC(),
    } + (DatesContainer.commenceDateOC > 1 ? $"__oc_{DatesContainer.commenceDateOC}" : "");

    private string IncrementCommenceOC()
    {
        DatesContainer.commenceDateOC++;
        return "";
    }

    private static string GetEId()
    {
        DatesContainer.commenceDateOC++;
        return "Description";
    }

    public static DateBlock? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is not WLine line) return null;
        (string? spanText, string? dateText) = ExtractSpanAndDate(line.TextContent);

        if (spanText == null)
        { // we have no text to go on, if the style matches
            return ByStyle(line, line.TextContent);
        }


        if ((ByText(parser.LanguageService, spanText, dateText, line.Style)
            ?? ByStyle(line, spanText, dateText, line.Style))
            is DateBlock block)
        {
            return block;
        }

        if (string.IsNullOrEmpty(dateText))
        {
            // There may be something in the date section that we didn't
            // parse as a date. to avoid omitting it, we just insert it in the
            // span and rely on users to fix it themselves.
            return new OtherDate(line.TextContent, dateText);
        } else
        {
            return new OtherDate(spanText, dateText);
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


    private static DateBlock? ByText(LanguageService ls, string spanText, string? date, string? style = null) => spanText switch
    {
        string t when MadeDate.IsMade(ls, t) => new MadeDate(spanText, date),
        string t when LaidDate.IsLaid(ls, t) => new LaidDate(spanText, date),
        string t when CommenceDate.IsCommence(ls, t) => new CommenceDate(spanText, date, style),
        string t when OtherDate.IsKnownOtherDate(ls, t) => new OtherDate(spanText, date),
        _ => null,
    };

    private static DateBlock? ByStyle(WLine line, string spanText, string? date = null, string? style = null) => line switch
    {
        WLine l when MadeDate.IsStyled(l) => new MadeDate(spanText, date),
        WLine l when LaidDate.IsStyled(l) => new LaidDate(spanText, date),
        WLine l when CommenceDate.IsStyled(l) => new CommenceDate(spanText, date, style),
        WLine l when OtherDate.IsStyled(l) => new OtherDate(spanText, date),
        _ => null,
    };



    // looks for the rest of a date block
    // may have dashes inbetween first text and date at the end
    // it may be worth writing a regex to find the actual dates,
    // something like this?
    // (?<day>\d\d?(th|rd|st|nd))\s+(?<month>(January|Februrary|March|April|May|June|July|August|September|October|November|December))\s+(?<year>\d{2, 4})
    private const string DATE = @"(?<date>[^\s-].*$|$)";

    // we generally always expect the date to be separated from it's text by
    // at least one tab as it's required in Word to achieve the formatting.
    // There may also be dashes between
    private const string SPACE_BETWEEN = @"[^\t\s\-]*\t[\s\-]*";



    [GeneratedRegex(@"^(?<spanText>[ \w]+)" + SPACE_BETWEEN + DATE)]
    private static partial Regex DateBlockRegex();
}

internal sealed partial record MadeDate(
    string SpanText,
    string? DateText
) : DateBlock(
    "madeDate",
    SpanText,
    DocDate.ToDate(DateText, ReferenceKey.varMadeDate))
{
    public const ReferenceKey KEY = ReferenceKey.varMadeDate;
    public static bool IsMade(LanguageService languageService, string text) =>
        languageService.IsMatch(text, LanguagePatterns)?.Count > 0;

    // public static MadeDate? ParseByText(IParser<IBlock>) => MadeDate.IsMade(ls, t) => new MadeDate(spanText, ToDate(date)),
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
        [LanguageService.Lang.EN] = [ EnglishRegex() ],
        [LanguageService.Lang.CY] = [ WelshRegex() ]
    };
}

internal sealed partial record LaidDate(
    string SpanText,
    string? DateText
) : DateBlock(
    "laidDate",
    SpanText,
    DocDate.ToDate(DateText, ReferenceKey.varLaidDate))
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
        [LanguageService.Lang.EN] = [ EnglishRegex() ],
        [LanguageService.Lang.CY] = [ WelshRegex() ]
    };

}

internal sealed partial record CommenceDate(
    string SpanText,
    string? DateText,
    string? Style
) : DateBlock(
    "commenceDate",
    SpanText,
    DocDate.ToDate(DateText, ReferenceKey.varCommenceDate),
    Style switch
    {
        Coming => null,
        ComingC => "commenceClauses",
        _ => null,
    })
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
        [LanguageService.Lang.EN] = [ EnglishRegex() ],
        [LanguageService.Lang.CY] = [ WelshRegex() ]
    };

}


internal sealed partial record OtherDate(
    string SpanText,
    string? DateText
) : DateBlock("otherDate",
    SpanText,
    DocDate.ToDate(DateText, ReferenceKey.varOtherDate))
{
    public static bool IsKnownOtherDate(LanguageService languageService, string text) =>
        languageService.IsMatch(text, LanguagePatterns)?.Count > 0;


    private static readonly string[] STYLES = ["Sifted"];
    public static bool IsStyled(WLine line) => STYLES.Any(line.HasStyle);

    [GeneratedRegex(@"^Sift requirements satisfied", RegexOptions.IgnoreCase)]
    private static partial Regex EngSiftReqRegex();

    private static readonly Dictionary<LanguageService.Lang, IEnumerable<Regex>> LanguagePatterns = new()
    {
        [LanguageService.Lang.EN] = [ EngSiftReqRegex() ],
    };
}