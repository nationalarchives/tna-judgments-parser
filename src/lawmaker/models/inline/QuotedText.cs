
using System.Collections.Generic;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    class Mod : IInlineContainer
    {

        internal IList<IInline> Contents { get; init; }

        IEnumerable<IInline> IInlineContainer.Contents => Contents;

    }

    internal class QuotedText : IInlineContainer
    {

        internal IList<IInline> Contents { get; init; }

        public string StartQuote { get; internal set; }

        public string EndQuote { get; internal set; }

        IEnumerable<IInline> IInlineContainer.Contents => Contents;

    }

    class InlineQuotedStructure : IInline
    {

        public IList<IDivision> Contents { get; internal init; }

        public string StartQuote { get; internal set; }

        public string EndQuote { get; internal set; }

    }

    class AppendText(string text, RunProperties properties) : WText(text, properties) { }

}
