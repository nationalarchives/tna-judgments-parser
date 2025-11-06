
using System.Collections.Generic;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using static UK.Gov.Legislation.Lawmaker.LanguageService;

namespace UK.Gov.Legislation.Lawmaker
{

    class Mod : IBlock
    {

        internal IList<IBlock> Contents { get; init; }

    }

    internal class QuotedText : IInlineContainer
    {

        internal IList<IInline> Contents { get; init; }

        public string StartQuote { get; internal set; }

        public string EndQuote { get; internal set; }

        IEnumerable<IInline> IInlineContainer.Contents => Contents;


        public static readonly LanguagePatterns AmendingPrefixes = new()
        {
            [Lang.EN] = ["insert", "substitute", "omit", "leave out"],
            [Lang.CY] = ["mewnosoder", "rhodder", "hepgorer"],
        };

    }

    class InlineQuotedStructure : IInline
    {

        public IList<IDivision> Contents { get; internal init; }

        public string StartQuote { get; internal set; }

        public string EndQuote { get; internal set; }

    }

    class AppendText(string text, RunProperties properties) : WText(text, properties) { }

}
