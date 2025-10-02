#nullable enable

namespace UK.Gov.Legislation.Lawmaker;

using System;
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;


/// <summary>
/// The interface for parsing, similar to an Iterator.
/// </summary>
/// <remarks>
/// Allows arbitrary backtracking/lookahead with the <c>Match*</c> functions
/// which could be a drain on performance.
/// </remarks>
/// <typeparam name="T">
/// The type being parsed. This might be <c>string</c> if you're parsing a
/// string input or more likely <c>IBlock</c>.
/// </typeparam>
public interface IParser<T>
{

    // This doesn't really belong here
    LanguageService LanguageService { get; }

    internal int Save();

    void Restore(int save);

    T? Current();

    bool IsAtEnd();

    T? Peek(int num = 1);

    R? Peek<R>(ParseStrategy<R> strategy);

    // Move the parser forward and return the block the parser was on when `Advance()` was called.
    public T? Advance();

    // Advance the parser forward by `num` and returns to blocks passed.
    IEnumerable<T> Advance(int num);

    // Move the parser forward while `condition` is true and return everything advanced over
    internal List<T> AdvanceWhile(Predicate<T> condition);

    public delegate R? ParseStrategy<out R>(IParser<T> parser);

    // Attempts to match the current block with the supplied strategy.
    // If the strategy successfully matches then the result is returned.
    // If the strategy returns null (indicating the matching was unsuccessful) then the parser position is reset to before the `strategy` was called.
    // `strategy` is expected to update the state itself using `Advance` and `AdvanceWhile`
    // public T? Match<T>(ParseStrategy<T> strategy);

    /// <summary>
    /// Attempts to match parser input with any of the strategies from left to right.
    /// The first matching strategy is the type that is returned. See <seealso cref="Match(ParseStrategy)"/>
    /// </summary>
    /// <remarks>
    /// The first matching strategy is the the output returned. This is the same as a
    /// "left-most" derivation in a grammar where each strategy is a disjunction i.e.
    /// evaluating the grammar rule
    /// A := B | C
    /// is similar to <c>Match(B, C)</c> if B and C were parse strategies.
    /// </remarks>
    /// <param name="strategies">List of strategies. <see>ParseStrategy</see></param>
    ///
    public R? Match<R>(params ParseStrategy<R>[] strategies);

    // Continues matching blocks with the supplied `strategy` until, for a particular block:
    // a) the `predicate` evaluates to false or
    // b) the `strategy` fails to match
    // Returns a list of concurrent blocks which were successfully matched.
    // The resulting position of the parser is at the end of the final matched block.
    public List<R>? MatchWhile<R>(params ParseStrategy<R>[] strategies) => MatchWhile(_ => true, strategies);
    public List<R>? MatchWhile<R>(Predicate<T> condition, params ParseStrategy<R>[] strategies);
}
