#nullable enable

namespace UK.Gov.Legislation.Lawmaker;

using System;
using System.Collections.Generic;


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

    // TODO: make private
    internal int Save();

    // TODO: make private
    void Restore(int save);

    /// <returns>The current token the parser is at.</returns>
    T? Current();

    /// <returns>If the parser has reached the end of input</returns>
    bool IsAtEnd();

    /// <summary>
    /// Look at the next token without advancing the parser.
    /// </summary>
    /// <remarks>
    /// <c>Peek(0)</c> is equivalent to calling <seealso cref="Current()"/><br/>
    /// There is no guarentee an implementer of this interface has handled
    /// negative values of <paramref name="num"/>
    /// </remarks>
    /// <param name="num">The num to look-ahead</param>
    /// <returns>The token at the parser position + <paramref name="num"/></returns>
    T? Peek(int num = 1);

    /// <summary>
    /// Peek the next token using <paramref name="strategy"/>
    /// </summary>
    /// <typeparam name="R">The type being checked for</typeparam>
    /// <param name="strategy">The strategy to use</param>
    /// <returns>The value <b>without advancing the parser</b> if successful.
    /// <c>null</c> otherwise</returns>
    R? Peek<R>(ParseStrategy<R> strategy);

    /// <summary>
    /// Move the parser forward.
    /// </summary>
    /// <returns>the block the parser was on when `Advance()` was called.</returns>
    public T? Advance();

    /// <summary>
    /// Move the parser forward by <paramref name="num"/>
    /// </summary>
    /// <param name="num"></param>
    /// <returns>The tokens passed</returns>
    IEnumerable<T> Advance(int num);

    /// <summary>
    /// Move the parser forward while <paramref name="condition"/> is true and
    /// </summary>
    /// <param name="condition"></param>
    /// <returns>return everything advanced over</returns>
    internal List<T> AdvanceWhile(Predicate<T> condition);

    /// <summary>
    /// A "strategy" to use to parse at a particular position for the parser.
    /// </summary>
    /// <typeparam name="R">The type of output being parsed for</typeparam>
    /// <param name="parser">A parser to advance if successful</param>
    /// <returns>The matched ouput <c>R</c>, or null if no match</returns>
    /// <remarks>
    /// Designed to be used in conjunction with <seealso cref="Match"/> and
    /// its overloads.<br/>
    /// </remarks>
    public delegate R? ParseStrategy<out R>(IParser<T> parser);

    /// <summary>
    /// Attempts to match parser input with any of the strategies from left to right.
    /// The first matching strategy is the type that is returned. See <seealso cref="Match(ParseStrategy)"/>
    /// </summary>
    /// <remarks>
    /// The first matching strategy is the the output returned. This is the same as a
    /// "left-most" derivation in a grammar where each strategy is a disjunction i.e.
    /// evaluating the grammar rule
    /// A := B | C
    /// is similar to <c>Match<A>(B, C)</c> if B and C were parse strategies.
    /// </remarks>
    /// <param name="strategies">List of strategies. <see>ParseStrategy</see></param>
    public R? Match<R>(params ParseStrategy<R>[] strategies);

    /// <summary>
    /// MatchWhile where predicate is always true.
    /// See <seealso cref="MatchWhile(Predictate, params ParseStrategy)"/>
    /// </summary>
    public List<R>? MatchWhile<R>(params ParseStrategy<R>[] strategies) => MatchWhile(_ => true, strategies);

    /// <summary>
    /// Continues matching blocks with the supplied `strategy` until,
    /// for a particular block:<br/>
    /// a) the `predicate` evaluates to false or<br/>
    /// b) the `strategy` fails to match<br/>
    /// </summary>
    /// <remarks>
    /// The resulting position of the parser is at the end of the final
    /// matched block.
    /// </remarks>
    /// <typeparam name="R">The type being parsed for</typeparam>
    /// <param name="condition">The predicate which indicates the input token
    /// should be parsed</param>
    /// <param name="strategies"></param>
    /// <returns>A list of contiguous tokens which were successfully matched</returns>
    public List<R>? MatchWhile<R>(Predicate<T> condition, params ParseStrategy<R>[] strategies);
}
