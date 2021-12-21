using System;
using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.Legislation.Judgments {

class Util {

    private static Func<IBlock, IEnumerable<ILine>> GetLines = (block) => {
        if (block is ILine line)
            return new List<ILine>(1) { line };
        if (block is IOldNumberedParagraph np)
            return new List<ILine>(1) { np };
        if (block is ITable table) {
            var cells = table.Rows.SelectMany(row => row.Cells);
            var blocks = cells.SelectMany(cell => cell.Contents);
            return blocks.SelectMany(GetLines);
        }
        throw new Exception();
    };

    public static IEnumerable<T> Descendants<T>(IEnumerable<IBlock> blocks) {
        return blocks.SelectMany(GetLines).SelectMany(line => line.Contents).OfType<T>();
    }

}

}
