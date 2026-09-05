
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker;


public partial class LegislationParser
{

    private HContainer ParseCurrentAsPara2()
    {
        if (Current() is not WLine line)
            return null;
        return ParsePara2(line);
    }

    private HContainer ParsePara2(WLine line)
    {
        if (line is not WOldNumberedParagraph np)
            return null;
        var numText = IgnoreQuotedStructureStart(np.Number.Text, quoteDepth);
        if (!Para2.IsValidNumber(numText))
            return null;

        i += 1;

        IFormattedText num = np.Number;
        List<IBlock> intro = HandleParagraphs(np, l => ParseAndMemoize(l, "Para2", ParsePara2));

        if (IsEndOfQuotedStructure(intro))
            return new Para2Leaf { Number = num, Contents = intro };

        List<IDivision> children = [];
        List<IBlock> wrapUp = [];

        var finalChildStart = i;
        while (i < Body.Count)
        {
            if (BreakFromProv1())
                break;

            var save = i;
            // Try Para3 (e.g. a sub-sub-paragraph like (aa)) in addition to the
            // existing Para2 check, so it is recognised as this item's child before
            // the generic dispatch order reaches Para1, whose broader lowercase-letter
            // pattern would otherwise wrongly claim it as a sibling of the outer
            // lettered paragraph.
            IDivision next = ParseNextBodyDivision(l => ParseAndMemoize(l, "Para2", ParsePara2) ?? ParseAndMemoize(l, "Para3", ParsePara3));
            // A Para3 candidate is only genuinely a child of this Para2 item if it is
            // actually indented further than it. Otherwise it is a sibling at the same
            // level that happens to use a disjoint format.
            if (next is Para3 && Body[save] is WLine candidateLine && !LineIsIndentedMoreThan(candidateLine, line))
            {
                i = save;
                break;
            }
            if (!Para2.IsValidChild(next))
            {
                i = save;
                break;
            }
            children.Add(next);
            finalChildStart = save;

            if (IsEndOfQuotedStructure(next))
                break;
        }
        wrapUp.AddRange(HandleWrapUp(children, finalChildStart));

        if (children.Count == 0)
            return new Para2Leaf { Number = num, Contents = intro };
        else
            return new Para2Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
    }

}
