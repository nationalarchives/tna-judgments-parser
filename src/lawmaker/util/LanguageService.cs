#nullable enable
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System;
using static UK.Gov.Legislation.Lawmaker.LanguageService;

namespace UK.Gov.Legislation.Lawmaker;

/// <summary>
/// Service for handling multi-language pattern matching operations.
/// Initialise with your chosen set of languages to support.
/// </summary>
public class LanguageService
{

    private List<Lang> languages;

    /// <summary>
    /// Supported language codes following ISO 639-1 standard.
    /// </summary>
    public enum Lang
    {
        EN,  // English
        CY,  // Welsh
    }

    /// <summary>
    /// Initializes a new instance of LanguageService with the specified languages.
    /// Defaults to English if no languages are provided.
    /// </summary>
    /// <param name="languages">Collection of supported languages</param>
    public LanguageService(IEnumerable<Lang> languages)
    {
        this.languages = languages?.Any() == true ? languages.ToList() : [Lang.EN];
    }

    /// <summary>
    /// Initializes a new instance of LanguageService with the specified languages
    /// (represented as ISO 639-1 language strings). Defaults to English if no languages are provided,
    /// </summary>
    /// <param name="languages">Collection of ISO 639-1 language codes as strings</param>
    public LanguageService(IEnumerable<string> languages)
        : this(ParseLanguageStrings(languages))
    {
    }

    /// <summary>
    /// Converts ISO 639-1 language code strings to Lang enum values.
    /// </summary>
    /// <param name="languageStrings">Collection of ISO 639-1 language codes as strings</param>
    /// <returns>Collection of valid Lang enum values</returns>
    private static IEnumerable<Lang> ParseLanguageStrings(IEnumerable<string> languageStrings)
    {
        return languageStrings?
            .Select(s => Enum.TryParse<Lang>(s, true, out var lang) ? lang : (Lang?)null)
            .Where(lang => lang.HasValue)
            .Select(lang => lang.Value)
            ?? [];
    }

    /// <summary>
    /// Checks if the provided text is matched by any of the language-specific regex patterns
    /// which are currently active.
    /// </summary>
    /// <param name="text">Text to check for matches</param>
    /// <param name="languagePatterns">Dictionary of language-specific regex patterns</param>
    /// <returns>True if the provided text matches any active language-specific regex patterns</returns>
    public bool IsMatch(string text, LanguagePatterns languagePatterns) =>
        IsMatch(
            text,
            languagePatterns.Patterns
                .Select(it =>
                    (it.Key, it.Value.Select(pattern =>
                        new Regex(pattern, RegexOptions.IgnoreCase))))
                .ToDictionary())
        ?.Count > 0;

    public MatchCollection? IsMatch(
        string text,
        IReadOnlyDictionary<Lang, IEnumerable<Regex>> languagePatterns
    ) => languagePatterns
        .Where(it => languages.Contains(it.Key))
        .SelectMany(it => it.Value)
        .Select(it => it.Matches(text))
        .Where(it => it.Count > 0)
        .FirstOrDefault();

    /// <summary>
    /// Creates a copy of <paramref name="languagePatterns"/> with all inactive language entries removed. 
    /// </summary>
    /// <param name="languagePatterns">Dictionary of language-specific regex patterns</param>
    /// <returns>A copy of <paramref name="languagePatterns"/> with all inactive language entries removed</returns>
    public Dictionary<Lang, IEnumerable<string>> GetActive(LanguagePatterns languagePatterns)
    {
        return languagePatterns.Patterns.Where(lp => languages.Contains(lp.Key)).ToDictionary();
    }

}

/// <summary>
/// A <c>Dictionary</c> which stores a collection of string patterns for each given <c>Lang</c>.
/// </summary>
public record LanguagePatterns
{
    private readonly Dictionary<Lang, IEnumerable<string>> _patterns = [];

    public IEnumerable<string> this[Lang lang]
    {
        get => _patterns[lang];
        set => _patterns[lang] = value;
    }

    public IReadOnlyDictionary<Lang, IEnumerable<string>> Patterns => _patterns;
}