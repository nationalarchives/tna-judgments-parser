using System.Linq;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments.Parse;

namespace test.parsers;

public abstract class ParserTestBase
{
    private protected static readonly WLine LineTemplate = new(null, new Paragraph());

    private protected static WLine TextLine(params string[] text)
    {
        return new WLine(LineTemplate, text.Select(t => new WText(t, null)));
    }

    private protected static WRow RowOf(params string[] cells)
    {
        return new WRow(TableOf(), null, null, cells.Select(c => CellOf(c)));
    }

    private protected static WRow RowOf(WCell[] cells)
    {
        return new WRow(TableOf(), null, null, cells);
    }

    private protected static WCell CellOf(params string[] lines)
    {
        return new WCell(RowOf(), null, lines.Select(l => TextLine(l)));
    }

    private protected static WCell CellWithOneLineOf(params string[] text)
    {
        return new WCell(RowOf(), null, [TextLine(text)]);
    }

    private protected static WTable TableOf(params string[][] rows)
    {
        return new WTable(null, null, null, rows.Select(RowOf));
    }

    private protected static WTable TableOf(WRow[] rows)
    {
        return new WTable(null, null, null, rows);
    }

    private protected static WTab Tab()
    {
        return new WTab(new TabChar());
    }

    private protected static WText Text(string s)
    {
        return new WText(s, new RunProperties());
    }
}
