#nullable enable
namespace UK.Gov.Legislation.Lawmaker;

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;
using static UK.Gov.Legislation.Lawmaker.LanguageService;

// This class currently breaks from the convention of putting all the parsing in partial class LegislationParser.
// I think it's ultimately a mistake to have such a big class spread over so many different files and I believe
// partial classes weren't designed for that sort of thing.
record LdappTableBlock(
    LdappTableNumber? TableNumber,
    WTable Table
) : IBlock, ILineable/*, IBuildable */
{

    // TODO: move this out - doesn't belong here, just testing out using Xml.Linq support
    public static readonly XNamespace HtmlNamespace = "http://www.w3.org/1999/xhtml";
    public static readonly XNamespace AknNamespace = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";

    // public ILine? Heading => TableNumber?.Captions?.First();
    // public ILine? Heading => null;

    // public IFormattedText? Number => new WText(TableNumber?.Number is null ? "" : TableNumber.Number?.NormalizedContent,null);
    // public IFormattedText? Number => null;
    // public string Name => "tblock";

    public IEnumerable<WLine> Lines => TableNumber is null
        ? Table.Lines
        : TableNumber.Lines.Concat(Table.Lines);

    // public XElement Build()
    // {
    //     XElement tblock = new("tblock",
    //         new XAttribute("class", "table"),
    //         new XAttribute("xmlns", Builder.AknNamespace),
    //         TableNumber != null ? new XElement("num", TableNumber?.Number.NormalizedContent) : null,
    //         new XElement("foreign",
    //             BuildTable(Table)
    //         )
    //     );
    // }

    // private XElement BuildTable(ITable model)
    // {
    //     return new("table",
    //         new XAttribute("xmlns", HtmlNamespace),
    //         new XAttribute("xmlns:akn", AknNamespace),
    //         new XAttribute(HtmlNamespace + "class", "allBorders tableleft width100"),
    //         new XAttribute("cols", model.ColumnWidthsIns.Count.ToString())
    //     );
    // }
    internal static LdappTableBlock? Parse(IParser<IBlock> parser)
    {
        // We can have a table on it's own *or* a table with a table num
        LdappTableNumber? number = parser.Match(LdappTableNumber.Parse);
        if (parser.Match(ParseTable) is WTable table)
        {
            return new LdappTableBlock(number, table);
        }
        return null;
    }

    private static WTable? ParseTable(IParser<IBlock> parser)
    {
        if (parser.Advance() is WTable table)
        {
            // Identify lines with leading numbers in each table cell.
            WTable extracted = WTable.Enrich(table, HardNumbers.ExtractTableCell);
            // Parse any structured content in each table cell.
            return WTable.Enrich(extracted, ParseTableCell(parser.LanguageService));
        }
        return null;
    }

    // Creates BlockLists from structured content inside table cells (if any).
    private static System.Func<WCell, WCell> ParseTableCell(LanguageService languageService) =>
    (WCell cell) =>
    {
        BlockParser parser = new(cell.Contents) { LanguageService = languageService};
        IEnumerable<IBlock> enriched = BlockList.ParseFrom(parser);
        return new WCell(cell.Row, cell.Props, enriched);
    };

}

// There can optionally be an arbitrary number of text blocks between
// the table number and the table itself.
// The first text block is the caption, the rest are arbitrary paragraphs
// Here they are all referred to as captions
partial record LdappTableNumber(
    WLine Number,
    List<WLine>? Captions
) : ILineable {

    internal static readonly LanguagePatterns TableNumberPatterns = new()
    {
        [Lang.EN] = [@"^Table\s+\w+$"],
        [Lang.CY] = [@"^Tabl\s+\w+$"]
    };

    public IEnumerable<WLine> Lines => Captions is null ? [Number] : Captions.Prepend(Number);

    internal static LdappTableNumber? Parse(IParser<IBlock> parser)
    {
        IBlock? block = parser.Advance();
        if (block is not WLine line) return null;
        if (!parser.LanguageService.IsMatch(line.NormalizedContent, TableNumberPatterns)) return null;
        return new LdappTableNumber(line, parser.Match(LdappTableCaptions.Parse));
    }
}

class LdappTableCaptions
{

    private static readonly ILogger Logger = Logging.Factory.CreateLogger<Builder>();

    internal static List<WLine>? Parse(IParser<IBlock> parser)
    {
        // Everything between the table num and the table itself is considered a caption
        List<WLine> captions = parser
            .AdvanceWhile(block => block is not WTable)
            .OfType<WLine>()
            .ToList();

        return captions switch
        {
            null or [] => null,
            _ => captions,
        };
    }
}
