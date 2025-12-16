
#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Headers;

partial record SPPreface(
    WLine? AccompanyingDocumentsStatementHeading,
    WLine? AccomanyingDocumentsContent,
    Reference? BillTitle,
    BracketedStageVersion? StageVersion,
    WLine LongTitle
) : IBuildable<XNode> {

    internal static SPPreface? Parse(IParser<IBlock> parser)
    {
        (WLine?, WLine?)? accompanyingDocumentsStatement = parser.Match(AccompanyingDocumentsStatement);
        WLine? billTitle = parser.Match(GenericBillTitle.Parse);
        BracketedStageVersion? stageVersion = parser.Match(BracketedStageVersion.Parse);
        // we always expect a long title since it indicates the end of a preface
        WLine? longTitle =  parser.Match(ParseLongTitle);
        if (longTitle is null)
        {
            return null;
        }

        return new SPPreface(
            accompanyingDocumentsStatement?.Item1,
            accompanyingDocumentsStatement?.Item2,
            new Reference(ReferenceKey.varBillTitle) { ShowAs = billTitle?.TextContent ?? "" },
            stageVersion,
            longTitle
        );
    }

    internal static (WLine?, WLine?)? AccompanyingDocumentsStatement(IParser<IBlock> parser)
    {
        WLine? heading = default;
        if (parser.Advance() is WLine line
            && line.IsAllBold()
            && line.IsCenterAligned()
            && AccompanyingDocumentsStart().IsMatch(line.NormalizedContent))
        {
            heading = line;
        }

        if (heading is null)
        {
            return default;
        }

        if (parser.Advance() is WLine line2
            && line2.IsCenterAligned()
        ) {
            return (heading, line2);
        }

        return default;


    }

    private static WLine? ParseLongTitle(IParser<IBlock> parser) =>
        Parsers.TextContent(LongTitleStart())(parser) as WLine;


    [GeneratedRegex(@"^THE\s*FOLLOWING\s*ACCOMPANYING\s*DOCUMENTS", RegexOptions.IgnoreCase)]
    private static partial Regex AccompanyingDocumentsStart();

    [GeneratedRegex(@"^An\s*Act\s*of\s*the\s*Scottish\s*Parliament", RegexOptions.IgnoreCase)]
    private static partial Regex LongTitleStart();

    public XNode? Build(Document Document) =>
    new XElement(akn + "preface",
        // new XElement(akn + "tblock",
        //     new XAttribute(akn + "class", "explanatoryNotesStatement"),
        //     AccompanyingDocumentsStatementHeading is not null
        //         ? new XElement(akn + "heading",
        //             new XText(AccompanyingDocumentsStatementHeading.TextContent))
        //         : null,
        //     AccomanyingDocumentsContent is not null
        //         ? new XElement(akn + "p",
        //             new XText(AccomanyingDocumentsContent.TextContent))
        //         : null),

        new XElement(akn + "block",
            new XAttribute("name", "title"),
            new XElement(akn + "docTitle",
                new XElement(akn + "ref",
                    new XAttribute(akn + "class", "placeholder"),
                    new XAttribute("href", $"#{Document.Metadata.Register(BillTitle)?.EId ?? "varBillTitle"}")))),
        StageVersion?.Build(Document),
        new XElement(akn + "longTitle",
            new XText(LongTitle?.TextContent ?? "")));
}