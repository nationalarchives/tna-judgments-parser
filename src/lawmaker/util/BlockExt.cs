#nullable enable
using System;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
namespace UK.Gov.Legislation.Lawmaker;

public static class BlockExt
{
    // public static WLine? AsLine(this IBlock block) => block as WLine;
    public static Predicate<IBlock> HasStyle(string style) =>
    (IBlock block) =>
        block is WLine line
        && line.Style == style;

    public static bool HasStyle(this IBlock block, string style) =>
        HasStyle(style)(block);
}