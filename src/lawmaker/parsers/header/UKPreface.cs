
#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Headers;

partial record UKPreface(
    BracketedStageVersion? StageVersion,
    BillLongTitle LongTitle
) : IPreface {

    internal static UKPreface? Parse(IParser<IBlock> parser)
    {
        BracketedStageVersion? stageVersion = parser.Match(BracketedStageVersion.Parse);
        // we always expect a long title since it indicates the end of a preface
        var _notes = parser.MatchWhile(UKHeader.Note);
        var longTitle = parser.Match(Headers.BillLongTitle.BigABillTo);


        if (longTitle is null)
        {
            return null;
        }

        return new UKPreface(
            stageVersion,
            longTitle
        );
    }

    [GeneratedRegex(@"^THE\s*FOLLOWING\s*ACCOMPANYING\s*DOCUMENTS", RegexOptions.IgnoreCase)]
    private static partial Regex AccompanyingDocumentsStart();

    [GeneratedRegex(@"^An\s*Act\s*of\s*the\s*Scottish\s*Parliament", RegexOptions.IgnoreCase)]
    private static partial Regex LongTitleStart();

    public XNode? Build(Document Document) =>
        new XElement(akn + "preface",
            new XAttribute("eId", "preface"),
            (StageVersion ?? BracketedStageVersion.Default()).Build(Document),
            LongTitle?.Build(Document));
}