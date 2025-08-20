#nullable enable

namespace UK.Gov.Legislation.Lawmaker;

using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;


interface IParser
{

    internal int Save();

    void Restore(int save);

    IBlock Current();

    bool IsAtEnd();

    IBlock Peek(int num = 1);

    // Move the parser forward and return the block the parser was on when `Advance()` was called.
    IBlock Advance();

    // Advance the parser forward by `num` and returns to blocks passed.
    IEnumerable<IBlock> Advance(int num);

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

}
