#nullable enable

namespace UK.Gov.Legislation.Lawmaker;

using System;
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;


class BlockParser : IParser
{
    private int i = 0;
    private readonly List<IBlock> Body;

    public BlockParser(IEnumerable<IBlock> contents)
    {
        Body = contents.ToList();
    }

    public int Save() => i;

    public void Restore(int save) => i = save;

    // Get the current block the parser is at
    public IBlock Current() => Body[i];

    public bool IsAtEnd() => i >= Body.Count;


    // Get the block that is `num` positions away.
    // `Peek(0)` will show the current block without advancing (same as `Current()`).
    // At the moment num can be negative to look behind. There are currently no safeguards
    // for checking within the bounds of the Document Body.
    public IBlock? Peek(int num = 1)
    {
        int peekIndex = i + num;
        if (peekIndex < 0 || peekIndex >= Body.Count)
            return null;
        return Body[peekIndex];
    }

    // Advance the parser forward by `num` and returns to blocks passed.
    public IEnumerable<IBlock> Advance(int num)
    {
        if (num <= 0) return [];
        var slice = Body[i..(i + num)];
        i += slice.Count;
        return slice;
    }

    // Move the parser forward while `condition` is true and return everything advanced over
    public List<IBlock> AdvanceWhile(Predicate<IBlock> condition)
    {
        IEnumerable<IBlock> list = Body[i..]
            .TakeWhile(block => condition(block) && !IsAtEnd());
        Advance(list.Count());
        return list.ToList();
    }

}
