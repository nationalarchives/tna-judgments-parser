#nullable enable
namespace UK.Gov.Legislation.Lawmaker;

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using DocumentFormat.OpenXml.Spreadsheet;
using UK.Gov.NationalArchives.CaseLaw.PressSummaries;
using UK.Gov.NationalArchives.CaseLaw.Parse;
using static UK.Gov.Legislation.Lawmaker.LanguageService;

// This class currently breaks from the convention of putting all the parsing in partial class LegislationParser.
// I think it's ultimately a mistake to have such a big class spread over so many different files and I believe
// partial classes weren't designed for that sort of thing.
record LdappTableBlock(
    LdappTableNumber? TableNumber,
    WTable Table
) : IBlock/*, IBuildable */
{

    // TODO: move this out - doesn't belong here, just testing out using Xml.Linq support
    public static readonly XNamespace HtmlNamespace = "http://www.w3.org/1999/xhtml";
    public static readonly XNamespace AknNamespace = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";

    // public ILine? Heading => TableNumber?.Captions?.First();
    // public ILine? Heading => null;

    // public IFormattedText? Number => new WText(TableNumber?.Number is null ? "" : TableNumber.Number?.NormalizedContent,null);
    // public IFormattedText? Number => null;
    // public string Name => "tblock";

    public IEnumerable<IBlock> Contents => ToList();

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
    internal static LdappTableBlock? Parse(LegislationParser parser)
    {
        // We can have a table on it's own *or* a table with a table num
        {
            if (parser.Match(ParseTable) is WTable table)
            {
                return new LdappTableBlock(null, table);
            }
        }

        if (parser.Match(LdappTableNumber.Parse) is LdappTableNumber number)
        {
            if (parser.Match(ParseTable) is WTable table)
            {
                return new LdappTableBlock(number, table);
            }
        }
        return null;
    }

    private static WTable? ParseTable(LegislationParser parser)
    {
        if (parser.Advance() is WTable table)
        {
            // Identify lines with leading numbers in each table cell.
            WTable extracted = WTable.Enrich(table, HardNumbers.ExtractTableCell);
            // Parse any structured content in each table cell.
            return WTable.Enrich(extracted, ParseTableCell);
        }
        return null;
    }

    // Creates BlockLists from structured content inside table cells (if any).
    private static WCell ParseTableCell(WCell cell)
    {
        IEnumerable<IBlock> enriched = BlockList.ParseFrom(cell.Contents);
        return new WCell(cell.Row, cell.Props, enriched);
    }

    private List<IBlock> ToList()
    {
        List<IBlock> list = [];
        if (TableNumber?.Number is not null) list.Add(TableNumber.Number);
        if (TableNumber?.Captions is not null) list.AddRange(TableNumber.Captions);
        list.Add(Table);
        return list;
    }

}

// There can optionally be an arbitrary number of text blocks between
// the table number and the table itself.
// The first text block is the caption, the rest are arbitrary paragraphs
// Here they are all referred to as captions
partial record LdappTableNumber(
    WLine Number,
    List<WLine>? Captions
) {

    private static readonly Dictionary<Lang, string> TableNumberPatterns = new()
    {
        [Lang.ENG] = @"^Table\s+\w+$",
        [Lang.CYM] = @"^Tabl\s+\w+$"
    };

    internal static LdappTableNumber? Parse(LegislationParser parser)
    {
        IBlock block = parser.Advance();
        if (block is not WLine line) return null;
        if (!parser.langService.IsMatch(line.NormalizedContent, TableNumberPatterns)) return null;
        return new LdappTableNumber(line, parser.Match(LdappTableCaptions.Parse));
    }
}

class LdappTableCaptions
{

    private static readonly ILogger Logger = Logging.Factory.CreateLogger<Builder>();

    internal static List<WLine>? Parse(LegislationParser parser)
    {
        // Everything between the table num and the table itself is considered a caption
        List<WLine> captions = parser
            .AdvanceWhile(block => block is not WTable)
            .Where(block => block is WLine)
            .Select(block => block as WLine)
            .Where(block => block is not null)
            .Select(block => block!)
            .ToList();

        return captions switch
        {
            null or [] => null,
            _ => captions,
        };
    }
}