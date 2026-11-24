#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

/// <summary>
/// Works out the type label (e.g. "Appeal No", "UTLC Case Number") that introduces a proceedings identifier
/// from the text that precedes it.
/// </summary>
internal interface IProceedingsIdentifierTypeLabelParser
{
    /// <summary>
    /// Returns the label that the text ends with, with runs of whitespace collapsed to single spaces and any plural
    /// made singular. Returns null if the text does not end with a "label:" or "label." phrase, or if that phrase is
    /// only a conjunction such as "and".
    /// </summary>
    string? ParseFromPrecedingText(string textBeforeIdentifier);
}

internal partial class ProceedingsIdentifierTypeLabelParser : IProceedingsIdentifierTypeLabelParser
{
    private static readonly HashSet<string> NotLabels = new(["and", "or", "and/or"], StringComparer.OrdinalIgnoreCase);

    public string? ParseFromPrecedingText(string textBeforeIdentifier)
    {
        var trimmedText = textBeforeIdentifier.Trim();

        var labelMatch = LabelRegex().Match(LastPhrase(trimmedText));
        if (!labelMatch.Success)
        {
            return null;
        }

        var label = labelMatch.Groups["label"].Value.CleanWhitespace();
        if (NotLabels.Contains(label))
        {
            return null;
        }

        var endsWithWhitespace = trimmedText.Length < textBeforeIdentifier.Length;
        var hasTerminator = labelMatch.Groups["terminator"].Success;
        if (!hasTerminator && !endsWithWhitespace)
        {
            return null;
        }

        return PluralSuffixRegex().Replace(label, "");
    }

    private static string LastPhrase(string trimmedText)
    {
        var lastBoundary = PhraseBoundaryRegex().Matches(trimmedText).LastOrDefault();

        if (lastBoundary is null)
        {
            return trimmedText;
        }

        return trimmedText[(lastBoundary.Index + lastBoundary.Length)..];
    }

    private const string TerminatorPunctuation = @"[:.\-–—]";
    private const string LabelTerminator = $@"\s*(?:{TerminatorPunctuation}\s*)+";
    private const string Tab = @"\t";
    private const string WhitespaceGap = $@"\s{{3,}}(?!{LabelTerminator}$)";
    private const string ClosingBracket = @"\)";
    private const string CommaOrSemicolon = @"[,;]\s*";

    [GeneratedRegex($"{Tab}|{WhitespaceGap}|{ClosingBracket}|{CommaOrSemicolon}", RegexOptions.IgnoreCase)]
    private static partial Regex PhraseBoundaryRegex();

    [GeneratedRegex($@"^(?<label>[^:.]*[\p{{L}}\p{{N}}](?:\.['’]?s)?)(?<terminator>{LabelTerminator})?$")]
    private static partial Regex LabelRegex();

    [GeneratedRegex(@"\.?(?:['’]s|(?<!s)s)$", RegexOptions.IgnoreCase)]
    private static partial Regex PluralSuffixRegex();
}
