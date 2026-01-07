using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Vml;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker;

public static partial class Parsers
{
    public static IParser<T>.ParseStrategy<List<R>> StrictSequence<T, R>(params IParser<T>.ParseStrategy<R>[] strategies) =>
    (IParser<T> parser) =>
    {
        List<R> matches = [];
        foreach(var strategy in strategies)
        {
            if (parser.Match(strategy) is not R match)
            {
                return null;
            }
            matches.Add(match);
        }

        return matches;
    };

    public static IParser<T>.ParseStrategy<List<R>> OptionalSequence<T, R>(params IParser<T>.ParseStrategy<R>[] strategies) =>
    (IParser<T> parser) =>
    {
        List<R> matches = [];
        foreach(var strategy in strategies)
        {
            if (parser.Match(strategy) is not R match)
            {
                return null;
            }
            matches.Add(match);
        }

        return matches;
    };

    internal static IParser<IBlock>.ParseStrategy<WLine> TextContent(Predicate<string> condition) =>
    (IParser<IBlock> parser) =>
        parser.Match(p =>
            p.Advance() is WLine line
            && condition(line.TextContent)
            ? line
            : null);

    internal static IParser<IBlock>.ParseStrategy<WLine> TextContent(Regex regex) =>
    (IParser<IBlock> parser) =>
        parser.Match(p =>
            p.Advance() is WLine line
            && regex.IsMatch(line.TextContent)
            ? line
            : null);

    internal static IParser<IBlock>.ParseStrategy<WLine> TextContent(string content) => (IParser<IBlock> parser) =>
        parser.Match(p =>
            p.Advance() is WLine line
            && (Space().Replace(line
                .TextContent, "")?
                .Equals(content, System.StringComparison.InvariantCultureIgnoreCase) ?? false)
            ? line
            : null);

    internal static IParser<IBlock>.ParseStrategy<WLine> WLine(Predicate<WLine> predicate) =>
    (IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && predicate(line)
        ? line
        : null;

    [GeneratedRegex(@"\s")]
    private static partial Regex Space();
}