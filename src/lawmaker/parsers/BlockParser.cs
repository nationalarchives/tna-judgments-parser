#nullable enable

namespace UK.Gov.Legislation.Lawmaker;

using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;


public class BlockParser : IParser<IBlock>
{
    // TODO: Make private - ideally, i is never manipulated directly!
    protected int i = 0;
    internal readonly List<IBlock> Body;

    public required LanguageService LanguageService { get; init; }

    public BlockParser(IEnumerable<IBlock> contents)
    {
        Body = contents.ToList();
    }

    public int Save() => i;

    public void Restore(int save) => i = save;

    // Get the current block the parser is at
    public IBlock? Current() => IsInRange(i) ? Body[i] : null;

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

    public R? Peek<R>(IParser<IBlock>.ParseStrategy<R> strategy)
    {
        int save = Save();
        R? result = strategy(this);
        Restore(save);
        return result;

    }

    public IBlock? Advance()
    {
        IBlock? current = Current();
        i++;
        return current;
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

    public R? Match<R>(IParser<IBlock>.ParseStrategy<R> strategy)
    {
        // TODO: memoize here if needed
        int save = this.Save();
        R? block = strategy(this);
        if (block == null) this.Restore(save);
        return block;
    }

    public R? Match<R>(params IParser<IBlock>.ParseStrategy<R>[] strategies)
    {
        foreach (var strategy in strategies)
        {
            if (Match(strategy) is R matched)
            {
                return matched;
            }
        }
        return default;
    }

    public List<R> MatchWhile<R>(Predicate<IBlock> condition, params IParser<IBlock>.ParseStrategy<R>[] strategies)
    {
        List<R> matches = [];
        while (Current() is IBlock r
            && condition(r)
            && Match(strategies) is R match
            && !IsAtEnd())
        {
            matches.Add(match);
        }
        return matches;
    }

    private bool IsInRange(int i) => i >= 0 && i < Body.Count;

    public List<R>? MatchWhile<R>(Predicate<R> condition, params IParser<IBlock>.ParseStrategy<R>[] strategies)
    {
        List<R> matches = [];
        while (Current() is R r
            && condition(r)
            && Match(strategies) is R match
            && !IsAtEnd())
        {
            matches.Add(match);
        }
        return matches.Count != 0 ? matches : null;
    }
}
