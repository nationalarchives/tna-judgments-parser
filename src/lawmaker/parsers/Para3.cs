
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker;


public partial class LegislationParser
{

    // Mirrors ParsePara2 one level down: a Para3 item (e.g. a sub-sub-paragraph
    // like (aa)) can itself have children, namely a further roman-numeral
    // sub-paragraph such as (iii). It gives Para2 priority when looking for the
    // next division for the same reason Para1 does when looking for its own
    // Para2 children: a roman numeral is also a valid run of lowercase letters,
    // so without this, Para1's broader pattern would wrongly claim it first.
    private HContainer ParsePara3(WLine line)
    {
        if (line is not WOldNumberedParagraph np)
            return null;
        var numText = IgnoreQuotedStructureStart(np.Number.Text, quoteDepth);
        if (!Para3.IsValidNumber(numText))
            return null;

        i += 1;

        IFormattedText num = np.Number;
        List<IBlock> intro = HandleParagraphs(np, l => ParseAndMemoize(l, "Para2", ParsePara2));

        if (IsEndOfQuotedStructure(intro))
            return new Para3Leaf { Number = num, Contents = intro };

        List<IDivision> children = [];
        List<IBlock> wrapUp = [];

        var finalChildStart = i;
        while (i < Body.Count)
        {
            if (BreakFromProv1())
                break;

            var save = i;
            IDivision next = ParseNextBodyDivision(l => ParseAndMemoize(l, "Para2", ParsePara2));
            if (!Para3.IsValidChild(next))
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
            return new Para3Leaf { Number = num, Contents = intro };
        else
            return new Para3Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
    }

}
