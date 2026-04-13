
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Common;
using UK.Gov.Legislation.Models;
using CaseLaw = UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

partial class Parser : BaseLegislativeDocumentParser {

    private readonly string _filename;

    internal static IDocument Parse(WordprocessingDocument doc, string filename = null) {
        CaseLaw.WordDocument preParsed = new CaseLaw.PreParser().Parse(doc);
        Parser instance = new Parser(doc, preParsed, filename);
        return instance.Parse();
    }

    private static ILogger logger = Logging.Factory.CreateLogger<Parser>();

    private Parser(WordprocessingDocument doc, CaseLaw.WordDocument preParsed, string filename) 
        : base(doc, preParsed, LegislativeDocumentConfig.ForExplanatoryMemoranda()) {
        _filename = filename;
    }

    // All parsing logic is now inherited from BaseLegislativeDocumentParser

    // In EMs, a bullet paragraph (•) can have nested sub-bullets (◦) but never a
    // numbered N.M sub-paragraph — yet without this guard, when a numbered paragraph
    // (e.g. "2.2") follows a bullet list, its higher indent causes the parent-child
    // absorption logic to nest 2.2 inside the last bullet. We track the current parent
    // paragraph number via a stack and reject numbered-inside-bullet nesting.

    [GeneratedRegex(@"^\d+\.\d+\.?$")]
    private static partial Regex EMBodyParagraphNumberRegex();

    private readonly System.Collections.Generic.Stack<string> _parentNumbers = new();

    protected override IDivision ParseParagraphAndSubparagraphs(WLine line, bool sub = false, bool quote = false) {
        string num = (line is WOldNumberedParagraph np) ? np.Number?.Text?.Trim() : null;
        _parentNumbers.Push(num);
        try {
            return base.ParseParagraphAndSubparagraphs(line, sub, quote);
        } finally {
            _parentNumbers.Pop();
        }
    }

    override protected bool CannotBeSubparagraph(WLine line) {
        if (base.CannotBeSubparagraph(line))
            return true;
        if (_parentNumbers.TryPeek(out string parentNum) && IsBulletGlyph(parentNum)
            && line is WOldNumberedParagraph np && EMBodyParagraphNumberRegex().IsMatch(np.Number.Text.Trim()))
            return true;
        return false;
    }

    private static bool IsBulletGlyph(string text) {
        if (string.IsNullOrEmpty(text)) return false;
        return text == "•" || text == "◦" || text == "▪" || text == "·" || text == "o" || text == "-";
    }

    protected override List<IBlock> Header() {
        List<IBlock> header = BaseHeaderSplitter.Split(PreParsed.Body, Config);
        i = header.Count;
        return header;
    }

    protected override DocumentMetadata MakeMetadata(List<IBlock> header) {
        return EMMetadata.Make(header, doc, Config, _filename);
    }

}

}
