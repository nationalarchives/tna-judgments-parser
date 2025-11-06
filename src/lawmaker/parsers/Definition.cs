
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.Enrichment;
using Lang = UK.Gov.Legislation.Lawmaker.LanguageService.Lang;

namespace UK.Gov.Legislation.Lawmaker;

public partial class LegislationParser
{

    private bool PeekDefinition(IBlock block)
    {
        if (block is not WLine line)
            return false;
        if (line is WOldNumberedParagraph)
            return false;
        if (!line.IsLeftAligned())
            return false;
        if (!Regex.IsMatch(line.NormalizedContent, DefinitionPattern(), RegexOptions.IgnoreCase))
            return false;
        return true;
    }

    private HContainer ParseDefinition(WLine line)
    {
        if (!PeekDefinition(line))
            return null;

        WLine enriched = EnrichFromBeginning.Enrich(line, DefinedTermPattern(), DefinedTermConstructor);
        if (ReferenceEquals(enriched, line))
            return null;

        i += 1;

        List<IBlock> intro = [enriched];

        if (i == Body.Count)
            return new DefinitionLeaf { Contents = intro };

        List<IDivision> children = [];

        while (i < Body.Count)
        {
            if (PeekDefinition(Current()))
                break;
            if (CurrentLineIsIndentedLessThan(line))
                break;

            int save = i;
            IDivision next = ParseNextBodyDivision();
            if (next is not Para1)
            {
                i = save;
                break;
            }
            children.Add(next);
        }
        if (children.Count == 0)
            return new DefinitionLeaf { Contents = intro };
        return new DefinitionBranch { Intro = intro, Children = children };
    }

    private static string _definedTermPattern;

    /// <summary>
    /// Returns a regular expression string which matches a 'defined term'. 
    /// That is, a portion of text surrounded by a pair of opening and closing quotes.
    /// </summary>
    private static string DefinedTermPattern()
    {
        if (_definedTermPattern is not null)
            return _definedTermPattern;

        string startQuote = "[\u201C]";
        string endQuote = "[\u201D]";
        _definedTermPattern = $@"({startQuote}(?:(?!{startQuote}|{endQuote}).)*{endQuote})";
        return _definedTermPattern;
    }

    private string _definitionPrefixes;

    /// <summary>
    /// Returns a regular expression string which matches special phrases that are allowed to 
    /// appear before the 'defined term' at the beginning of a definition.
    /// </summary>
    private string DefinitionPrefixes()
    {
        if (_definitionPrefixes is not null)
            return _definitionPrefixes;

        LanguagePatterns prefixes = new()
        {
            [Lang.CY] = ["mae", "mae i", "nid yw", "o ran", "ystyr"],
        };

        // Flatten all prefixes for all active languages into a single regular expression
        Dictionary<Lang, IEnumerable<string>> activePrefixes = LanguageService.GetActive(prefixes);
        _definitionPrefixes = $@"(?:(?:{string.Join("|", activePrefixes.SelectMany(it => it.Value))})\s*)";
        return _definitionPrefixes;
    }

    private string _definitionPattern;

    /// <summary>
    /// Returns a regular expression string which matches a 'definition'.
    /// </summary>
    /// <remarks>
    /// A 'definition' is a line beginning with a 'defined term'. Although the line IS allowed to begin with 
    /// certain specific words (known as 'prefixes') or quoted structure opening quotes.
    /// </remarks>
    private string DefinitionPattern()
    {
        if (_definitionPattern is not null)
            return _definitionPattern;

        _definitionPattern = $@"^{QuotedStructureStartPattern()}?{DefinitionPrefixes()}?{DefinedTermPattern()}.*\w+.*$";
        return _definitionPattern;
    }

    /// <summary>
    /// Constructs a <c>Def</c> element from a collection of <paramref name="inlines"/> 
    /// representing a 'defined term'. Separates the <c>StartQuote</c> and <c>EndQuote</c>
    /// from the <c>Contents</c>. 
    /// </summary>
    /// <param name="inlines">A collection of <paramref name="inlines"/>representing a 'defined term'</param>
    private static IInline DefinedTermConstructor(IEnumerable<IInline> inlines)
    {
        string startQuote = null;
        string endQuote = null;
        if (inlines.FirstOrDefault() is WText first)
        {
            startQuote = first.Text[..1];
            WText fixedFirst = new(first.Text[1..], first.properties);
            inlines = inlines.Skip(1).Prepend(fixedFirst);
        }
        if (inlines.LastOrDefault() is WText last)
        {
            endQuote = last.Text[^1..];
            WText fixedLast = new(last.Text[..^1], last.properties);
            inlines = inlines.SkipLast(1).Append(fixedLast);
        }
        return new Def() { Contents = inlines.ToList(), StartQuote = startQuote, EndQuote = endQuote };
    }
}
