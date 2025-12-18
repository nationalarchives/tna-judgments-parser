#nullable enable

using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Lawmaker.Headers;

namespace UK.Gov.Legislation.Lawmaker.Headers;

record NICoverPage(IEnumerable<IBlock> Blocks)
{
    internal static NICoverPage? Parse(IParser<IBlock> parser)
    {
        List<IBlock> blocks = [];
        if (parser.Match(GenericBillTitle.Parse) is WLine title)
        {
            blocks.Add(title);
        }
        if (parser.Match(BracketedStageVersion.Parse) is BracketedStageVersion stageVersion)
        {
            blocks.Add(stageVersion);
        }
        if (parser.Match(
            TableOfContents.Parse(block => parser.Peek(NIPrimaryPreface.Parse) is not NIPrimaryPreface preface))
            is TableOfContents toc)
        {
            // normally a front cover must have a ToC, but we want to be more permissive here
            blocks.AddRange(toc.Lines.Select(line => line.Line));
        }
        return blocks switch
        {
            [] => null,
            _ => new NICoverPage(blocks),

        };


    }
}
