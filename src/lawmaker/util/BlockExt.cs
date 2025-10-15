#nullable enable
using System;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
namespace UK.Gov.Legislation.Lawmaker;

/// <summary>
/// Lawmaker extensions members for the <c>IBlock</c> class.
/// </summary>
public static class BlockExt
{
    /// <summary>
    /// Determines if a block has <paramref name="style"/>.
    /// </summary>
    /// <param name="style">The to check for.</param>
    /// <returns>A predicate taking an <c>IBlock</c></returns>
    public static Predicate<IBlock> HasStyle(string style) =>
    (IBlock block) =>
        block is WLine line
        && line.Style == style;

    /// <summary>
    /// Calls <see cref="HasStyle(string)"/> on <paramref name="block"/>
    /// with <paramref name="style"/>.
    /// </summary>
    public static bool HasStyle(this IBlock block, string style) =>
        HasStyle(style)(block);
}