using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System;
namespace UK.Gov.Legislation.Lawmaker;

/// <summary> 
/// Service for handling multi-language pattern matching operations.
/// Initialise with your chosen set of languages to support.
/// </summary>
public class LanguageService
{

    private List<Lang> languages;

    /// <summary>
    /// Supported language codes following ISO 639-3 standard.
    /// </summary>
    public enum Lang
    {
        ENG,  // English
        CYM,  // Welsh
    }

    /// <summary>
    /// Initializes a new instance of LanguageService with the specified languages.
    /// Defaults to English if no languages are provided.
    /// </summary>
    /// <param name="languages">Collection of supported languages</param>
    public LanguageService(IEnumerable<Lang> languages)
    {
        this.languages = languages.ToList();
        if (this.languages.Count == 0)
            this.languages.Add(Lang.ENG);
    }

    /// <summary>
    /// Initializes a new instance of LanguageService with the specified languages
    /// (represented as ISO 639-3 language strings). Defaults to English if no languages are provided,
    /// </summary>
    /// <param name="languages">Collection of ISO 639-3 language codes as strings</param>
    public LanguageService(IEnumerable<string> languages)
        : this(ParseLanguageStrings(languages))
    {
    }

    /// <summary>
    /// Converts ISO 639-3 language code strings to Lang enum values.
    /// </summary>
    /// <param name="languageStrings">Collection of ISO 639-3 language codes as strings</param>
    /// <returns>Collection of valid Lang enum values</returns>
    private static IEnumerable<Lang> ParseLanguageStrings(IEnumerable<string> languageStrings)
    {
        return languageStrings
            .Select(s => Enum.TryParse<Lang>(s, true, out var lang) ? lang : (Lang?)null)
            .Where(lang => lang.HasValue)
            .Select(lang => lang.Value);
    }

    /// <summary>
    /// Checks if the provided text is matched by any of the language-specific regex patterns
    /// which are currently active.
    /// </summary>
    /// <param name="text">Text to check for matches</param>
    /// <param name="patterns">Dictionary of language-specific regex patterns</param>
    /// <returns>True if the provided text matches any active language-specific regex patterns</returns>
    public bool IsMatch(string text, Dictionary<Lang, string> patterns)
    {
        foreach (Lang language in languages)
        {
            if (!patterns.TryGetValue(language, out var pattern)) continue;
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase)) return true;
        }
        return false;
    }

}