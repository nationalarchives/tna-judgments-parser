#nullable enable

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

namespace test.Docx;

// LCO-5119: Word documents (e.g. MMO/DCO coordinate schedules) encode the minute (′) and second (″) signs used
// in geographical coordinates as Symbol-font <w:sym> characters rather than plain Unicode text. Before this fix,
// SpecialCharacter.Make passed the raw Symbol-font code point (0xF0A2 / 0xF0B2, in the Private Use Area) straight
// through, so it rendered as a square placeholder in the Editor and PDF, since no ordinary font has a glyph there.
public class TestSpecialCharacter
{
    [Fact]
    public void MinuteSymbolCharIsMappedToUnicodePrime()
    {
        var sym = new SymbolChar { Font = "Symbol", Char = "F0A2" };

        var special = SpecialCharacter.Make(sym, null);

        Assert.Equal("′", special.Text);
    }

    [Fact]
    public void SecondSymbolCharIsMappedToUnicodeDoublePrime()
    {
        var sym = new SymbolChar { Font = "Symbol", Char = "F0B2" };

        var special = SpecialCharacter.Make(sym, null);

        Assert.Equal("″", special.Text);
    }

    [Fact]
    public void MappedMinuteSecondCharsDoNotKeepTheSymbolFontAttribution()
    {
        // The Symbol font is a custom 8-bit encoding and isn't guaranteed to have a glyph at the real Unicode
        // code points U+2032/U+2033, so the font attribution should be cleared once the character is remapped
        // (matching the existing precedent for the 0x1E/"Arial Unicode MS" case in the same method).
        var minute = SpecialCharacter.Make(new SymbolChar { Font = "Symbol", Char = "F0A2" }, null);
        var second = SpecialCharacter.Make(new SymbolChar { Font = "Symbol", Char = "F0B2" }, null);

        Assert.NotEqual("Symbol", minute.FontName);
        Assert.NotEqual("Symbol", second.FontName);
    }

    [Fact]
    public void UnrelatedSymbolFontCharactersAreUnaffected()
    {
        // A sanity check that the fix is scoped to the two specific code points and doesn't disturb other
        // Symbol-font characters passed through unchanged by the existing behaviour.
        var sym = new SymbolChar { Font = "Symbol", Char = "F0B7" }; // bullet (•) in the Symbol font
        var special = SpecialCharacter.Make(sym, null);
        Assert.Equal("Symbol", special.FontName);
    }
}
