#nullable enable

namespace UK.Gov.Legislation.Lawmaker;

using System;
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;


interface IParser
{

    internal int Save();

    void Restore(int save);

    IBlock Current();

    bool IsAtEnd();

    IBlock Peek(int num = 1);

    // Move the parser forward and return the block the parser was on when `Advance()` was called.
    public IBlock Advance()
    {
        IBlock current = Current();
        int i = Save();
        Restore(i+1);
        return current;
    }

    // Advance the parser forward by `num` and returns to blocks passed.
    IEnumerable<IBlock> Advance(int num);

    // Move the parser forward while `condition` is true and return everything advanced over
    internal List<IBlock> AdvanceWhile(Predicate<IBlock> condition);

    delegate T? ParseStrategy<T>(IParser parser);

    // Attempts to match the current block with the supplied strategy.
    // If the strategy successfully matches then the result is returned.
    // If the strategy returns null (indicating the matching was unsuccessful) then the parser position is reset to before the `strategy` was called.
    // `strategy` is expected to update the state itself using `Advance` and `AdvanceWhile`
    public T? Match<T>(ParseStrategy<T> strategy)
    {
        // TODO: memoize here if needed
        int save = this.Save();
        T? block = strategy(this);
        if (block == null) this.Restore(save);
        return block;
    }

    // Continues matching blocks with the supplied `strategy` until, for a particular block:
    // a) the `predicate` evaluates to false
    // b) the `strategy` fails to match
    // Returns a list of concurrent blocks which were successfully matched.
    // The resulting position of the parser is at the end of the final matched block.
    internal List<T> MatchWhile<T>(Predicate<IBlock> condition, ParseStrategy<T> strategy)
    {
        List<T> matches = [];
        while (condition(Current()) && Match(strategy) is T match && !IsAtEnd())
            matches.Add(match);
        return matches;
    }


}
