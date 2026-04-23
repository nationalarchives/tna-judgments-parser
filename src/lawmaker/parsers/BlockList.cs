
#nullable enable
namespace UK.Gov.Legislation.Lawmaker;

using System;
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;

/*
 * There are certain locations in Lawmaker documents where we do not expect provisions
 * (i.e. parts, sections, sub-paragraphs) but rather, a simpler markup in which all
 * 'structured' content is to be parsed using the BlockList model.
 * Such locations include:
 *  - In the Explanatory Notes of Statutory Instruments
 *  - Inside table cells
 *
 *  BlockList markup is as follows, where the listIntroduction is optional:
 *
 *  <blockList>
 *      <listIntroduction>Text</listIntroduction>
 *      <item>
 *          <num>(1)</num>
 *          <p>Text</p>
 *      </item>
 *      ...
 *  </blockList>
 */
class BlockList : IBlock
{
    public WLine? Intro { get; internal init; }

    public IList<IBlock>? Children { get; internal init; }

    /// <summary>
    /// Identifies and parses any and all <c>BlockList</c> elements present in <paramref name="blocks"/>.
    /// </summary>
    /// <param name="blocks">The collection of <c>IBlock</c> in which to parse.</param>
    /// <returns>The transformed collection.</returns>
    public static IEnumerable<IBlock> ParseFrom(IParser<IBlock> parser)
    {
        List<IBlock> enrichedBlocks = new List<IBlock>();
        // save first for later
        IBlock? first = parser.Current();

        while (!parser.IsAtEnd())
        {
            // Remove empty lines from the collection to ensure they
            // don't throw off the parsing of BlockLists.
            IBlock current = parser.Current();
            if (parser.Current()?.IsEmptyLine() ?? false)
            {
                parser.Advance();
                continue;
            }

            if (parser.Match(BlockList.Parse) is BlockList blockList)
                enrichedBlocks.Add(blockList);
            else if (parser.Advance() is IBlock block)
                enrichedBlocks.Add(block);
        }
        // If there are no enriched blocks at this point, they must all have been empty lines.
        // We wish to leave at least 1 block in the collection, so we re-add an empty line
        if (enrichedBlocks.Count == 0 && first is not null)
            enrichedBlocks.Add(first);

        return enrichedBlocks;
    }

    /// <summary>
    /// Attempts to parse a <c>BlockList</c> from the current line of the <paramref name="parser"/>.
    /// </summary>
    /// <param name="parser">The parser.</param>
    /// <returns>The parsed <c>BlockList</c>, if successful. Otherwise <c>null</c>.</returns>
    public static BlockList? Parse(IParser<IBlock> parser)
    {
        IBlock? block = parser.Current();
        if (block is null || block.IsEmptyLine())
            return null;
        if (block is not WLine line)
            return null;
        // We expect that BlockLists are always left-aligned.
        if (line.GetEffectiveAlignment() == AlignmentValues.Center)
            return null;
        if (line.GetEffectiveAlignment() == AlignmentValues.Right)
            return null;

        // A BlockList can begin with a listIntroduction, which is a single unnumbered line.
        WLine? intro = null;
        if (!BlockListItem.IsNumbered(line))
        {
            intro = line;
            parser.Advance();
        }

        // Identify the 'leader' - the line immediately preceding the first BlockList item.
        // We expect all BlockList items to be indented further than the leader.
        WLine? leader = null;
        float leaderIndent = 0f;
        if (parser.Peek(-1) is WLine prevLine)
        {
            leader = prevLine;
            leaderIndent = OptimizedParser.GetEffectiveIndent(leader);
        }

        List<IBlock> children = [];
        while (!parser.IsAtEnd())
        {
            // Skip over empty lines to ensure they do not influence parsing.
            parser.AdvanceWhile(block => block.IsEmptyLine());
            if (parser.IsAtEnd())
                break;

            IBlock? currentBlock = parser.Current();
            if (currentBlock is not WLine currentLine)
                break;

            // BlockListItem child must have greater indent than leader.
            float threshold = 0.1f;
            float childIndent = OptimizedParser.GetEffectiveIndent(currentLine);
            if (leader is not null && !(childIndent - leaderIndent > threshold))
                break;

            // Attempt to parse BlockListItem child.
            if (parser.Match(BlockListItem.Parse) is BlockListItem blockListItem)
                children.Add(blockListItem);
            else
                break;
        }

        if (children.Count == 0)
            return null;
        return new BlockList { Intro = intro, Children = children };
    }
}

/*
 * A BlockListItem typically has a num, though not always.
 * They must have at least one <p> element. This serves as the 'intro'.
 * They sometimes contain nested BlockListItems, which serve as 'children'.
 *
 * <item>
 *     <num>(1)</num>
 *     <p>Text</p>
 *     <item>
 *         <num>
 *         <p>Text</p>
 *     </item>
 *     ...
 * </item>
 *
 * Note that in Lawmaker, nested BlockListItems are actually wrapped in BlockLists,
 * which is at odds with the above. We perform this wrapping in the Builder, as it
 * is much easier to do in post.
 *
 * A BlockListItem's <num> can take one of many forms, which is why we
 * rely upon indentation for tracking nesting, rather than numbering scheme:
 *
 * 1, 2, 3
 * 1., 2., 3.
 * (1), (2), (3)
 * (a), (b), (c)
 * (A), (B), (C)
 * (i), (ii), (iii)
 * (aa), (bb), (cc)
 * Bullet points
 * Em-dashes
 * No number at all
 */
internal class BlockListItem : IBlock
{
    public IFormattedText? Number { get; internal init; }

    public required IEnumerable<IBlock> Intro { get; internal init; }

    public required IEnumerable<BlockListItem> Children { get; internal init; }

    /// <summary>
    /// Attempts to parse a <c>BlockListItem</c> from the current line of the <paramref name="parser"/>.
    /// </summary>
    /// <param name="parser">The parser.</param>
    /// <returns>The parsed <c>BlockListItem</c>, if successful. Otherwise <c>null</c>.</returns>
    public static BlockListItem? Parse(IParser<IBlock> parser)
    {
        IFormattedText? number;
        IList<IBlock> intro = [];

        IBlock? firstBlock = parser.Advance();
        if (firstBlock is not WLine firstLine)
            return null;

        // Extract the number and intro from the first line
        IBlock introLine;
        if (firstLine is WOldNumberedParagraph np)
        {
            number = np.Number;
            introLine = WLine.RemoveNumber(np);
        }
        else if (IsNumbered(firstLine))
        {
            // Special cases such as bullets and em-dashes
            number = firstLine.Contents.First() as IFormattedText;
            introLine = new WLine(firstLine, firstLine.Contents.Skip(2));
        }
        else
        {
            // Unnumbered case
            number = null;
            introLine = firstLine;
        }

        if (introLine.IsEmptyLine())
            return null;
        intro.Add(introLine);

        // Handle subsequent lines belonging to this BlockListItem.
        // Currently, this is limited to nested BlockListItems.
        // Eventually, we should handle additional blocks belonging to the intro
        // such as extra paragraphs, quoted structures, tables, and images.
        IList<BlockListItem> children = [];
        float leaderIndent = OptimizedParser.GetEffectiveIndent(firstLine);

        while (!parser.IsAtEnd())
        {
            IBlock? nextBlock = parser.Current();
            if (nextBlock is not WLine nextLine)
                break;

            // Nested BlockListItems must be indented further than their parent,
            // or have a greater numbering level.
            float threshold = 0.1f;
            float currentIndent = OptimizedParser.GetEffectiveIndent(nextLine);
            bool isRightOfLeader = currentIndent - leaderIndent > threshold;
            if (!isRightOfLeader && !nextLine.HasGreaterNumberingLevelThan(firstLine))
                break;

            if (parser.Match(BlockListItem.Parse) is BlockListItem blockListItem)
                children.Add(blockListItem);
            else
                break;
        }

        if (intro.Count == 0)
            return null;
        return new BlockListItem { Number = number, Intro = intro, Children = children };
    }

    /// <summary>
    /// Determines whether <paramref name="line"/> is numbered per the <c>BlockListItem</c> numbering format.
    /// </summary>
    /// <param name="line">The line to check.</param>
    /// <returns><c>True</c> if <paramref name="line"/> is numbered.</returns>
    internal static bool IsNumbered(WLine line)
    {
        if (line is WOldNumberedParagraph)
            return true;
        // This is a hack to recognise lines starting with arbitrary characters such as bullets
        // and em-dashes as 'numbered' without altering the definition of WOldNumberedParagraph.
        IEnumerable<IInline> contents = line.Contents;
        return contents.Count() >= 3 && contents.First() is IFormattedText && contents.Skip(1).First() is WTab;
    }

}
