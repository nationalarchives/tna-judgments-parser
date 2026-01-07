#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker.Headers;

partial record UKCoverPage(IEnumerable<IBlock> Blocks)
{
    internal static UKCoverPage? Parse(IParser<IBlock> parser)
    {
        List<IBlock> blocks = [];
        if (parser.Match(GenericBillTitle.Parse) is WLine title)
        {
            blocks.Add(title);
        }
        // we ignore the following elements but we parse them in
        // case we want to use them later
        var note = parser.Match(UKHeader.Note);
        var explanatoryNote = parser.Match(ExplanatoryNote.Parse);
        var europeanConvention = parser.Match(EuropeanConvention);
        if (parser.Match(
            TableOfContents.Parse(block => parser.Peek(UKPreface.Parse) is not UKPreface preface))
            is TableOfContents toc)
        {
            // normally a front cover must have a ToC, but we want to be more permissive here
            blocks.AddRange(toc.Lines.Select(line => line.Line));
        }
        return blocks switch
        {
            [] => null,
            _ => new UKCoverPage(blocks),

        };
    }

    private static List<IBlock>? EuropeanConvention(IParser<IBlock> parser) {
        var heading = parser.Match(Parsers.WLine(line => EuropeanConventionTitleRegex().IsMatch(line.NormalizedContent)));
        if (heading is null)
        {
            return null;
        }
        var blocks = parser.AdvanceWhile(block => block is not WLine line || !line.IsCenterAligned());
        if (blocks is null || blocks.Count == 0)
        {
            return null;
        }
        return [heading, ..blocks];
    }


    [GeneratedRegex(@"\s*\[[\w\s]*\]\s*")]
    private static partial Regex NoteRegex();

    [GeneratedRegex(@"\s*EUROPEAN\s*CONVENTION\s*ON\s*HUMAN\s*RIGHTS", RegexOptions.IgnoreCase)]
    private static partial Regex EuropeanConventionTitleRegex();
}
