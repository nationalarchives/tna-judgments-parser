#nullable enable

using System.Globalization;
using System.Xml.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Headers;

record BracketedStageVersion(Reference Stage) : IBlock, IBuildable<XNode>
{
    // We may want to parse metadata here
    internal static BracketedStageVersion? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is not WLine line)
        {
            return null;
        };
        if (line.IsCenterAligned()
            && line.NormalizedContent.StartsWith('[')
            && line.NormalizedContent.EndsWith(']'))
        {
            return new BracketedStageVersion(
                new Reference(ReferenceKey.varStageVersion,
                CultureInfo.CurrentCulture.TextInfo.ToTitleCase(line.NormalizedContent[1..^1].ToLower())));
        }
        return null;



    }

    public XNode? Build(Document Document) =>
    new XElement(akn + "block",
        new XAttribute("name", "stageVersion"),
        new XText("["),
        new XElement(akn + "docStage",
            new XElement(akn + "ref",
                new XAttribute(akn + "class", "placeholder"),
                new XAttribute("href", $"{Document.Metadata.Register(Stage)?.EId ?? ""}")
            )),
        new XText("]"));
}
