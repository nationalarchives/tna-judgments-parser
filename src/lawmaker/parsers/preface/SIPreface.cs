#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker.Preface;

internal partial record SIPreface(IEnumerable<IBlock> Blocks) : IBlock, IBuildable<XNode>, IMetadata
{

    public XNode Build() => !Blocks
        .Where(block => block is not DateBlock)
        .All(b => b is IBuildable<XNode>)
        ? throw new System.Exception("IBuildable<XNode> not implemented for all children!")
        : new XElement(
            XmlExt.AknNamespace + "preface",
            from block in Blocks.Where(block => block is not DateBlock)
            where block is IBuildable<XNode>
            select (block as IBuildable<XNode>)!.Build()
        );

    public IEnumerable<Reference> Metadata =>
        Blocks.OfType<IMetadata>().SelectMany(b => b.Metadata);

    /*
    A grammar for preface might look something like:
    Preface = [CorrectionRubric]
            , [ProceduralRubric]
            , Banner
            , Number
            , Subjects
            , Title
            , ApprovalRubric
            , LaidInDraftRubric
            , DateBlock, { DateBlock }
            ;

    Subjects = SubjectBlock, { SubjectBlock } ;
    SubjectBlock = Subject, { Subsubject } ;

    CorrectionRubric = text w/ style: "Correction"
    ProceduralRubric = text w/ style: "Draft"
    Banner = text w/ style: "Banner"
    etc...

    */
    internal static SIPreface? Parse(IParser<IBlock> parser) =>
        parser.MatchWhile(PrefaceLine)
        is IEnumerable<IBlock> lines
        && lines.Any(block => block is DateBlock) // always expect a date block in the preface
        ? new(lines) // skip over DateBlock for now - handled in later ticket
        : null;


    internal static bool HasStyle(WLine line, string style) => line.Style == style ||
        // why do signatures check the first WText??
        (line.Contents.OfType<WText>().FirstOrDefault() is WText text
        && text.Style.Equals(style));

    private static IBlock? PrefaceLine(IParser<IBlock> parser)
    {
        // for now, order of the preface elements doesn't matter as we only
        // rely on the MS Word Style, however it may be more useful to enforce
        // the order of preface elements
        if (parser.Match<IBlock>(
            CorrectionRubric.Parse,
            ProceduralRubric.Parse,
            Banner.Parse,
            Number.Parse,
            Subjects.Parse,
            Title.Parse,
            Approval.Parse,
            LaidDraft.Parse,
            DateBlock.Parse
        ) is IBlock block)
        {
            return block;
        }

        return null;
    }
}

partial record CorrectionRubric(WLine Line) : IBlock, IBuildable<XNode>
{
    public XNode Build() => new XElement(
        XmlExt.AknNamespace + "block",
        new XAttribute("name", "correctionRubric"),
        new XText(Line.NormalizedContent) // TODO: properly represent inlines
    );

    public const string STYLE = "Correction";

    public static CorrectionRubric? Parse(IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && BlockExt.HasStyle("Correction")(line)
            ? new CorrectionRubric(line)
            : null;
}
partial record ProceduralRubric(WLine Line) : IBlock, IBuildable<XNode>
{
    public const string STYLE = "Draft";
    public XNode Build() => new XElement(
        XmlExt.AknNamespace + "block",
        new XAttribute("name", "proceduralRubric"),
        // new XAttribute("eId", "fnt__rubric"),
        new XText(Line.NormalizedContent)
    );

    public static ProceduralRubric? Parse(IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && line.Style == STYLE
            ? new ProceduralRubric(line)
            : null;
}

internal partial record Banner(WLine Line) : IBlock, IBuildable<XNode>
{
    public const string STYLE = "Banner";
    public XNode Build() => new XElement(
        XmlExt.AknNamespace + "block",
        new XAttribute("name", "banner"),
        new XText(Line.NormalizedContent)
    );

    public static Banner? Parse(IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && IsBanner(line)
            ? new Banner(line)
            : null;

    public static bool IsBanner(WLine? line) => line?.Style == STYLE;

}
partial record Number(
    WLine Line,
    string? Year = null,
    string? SINumber = null,
    string? SISubsidiaryNumbers = null
    ) : IBlock, IBuildable<XNode>, IMetadata
{
    public const string STYLE = "Number";

    private static readonly ILogger Logger = Logging.Factory.CreateLogger<Number>();

    public IEnumerable<Reference> Metadata =>
        new List<(ReferenceKey, string?)> {
            ( ReferenceKey.varSIYear, Year ),
            ( ReferenceKey.varSINoComp, SINumber ),
            ( ReferenceKey.varSISubsidiaryNos, SISubsidiaryNumbers ),
            // varSINo should always be this, but Lawmaker sets it for us:
            // { ReferenceKey.varSINo, "#varSIYear No. #varSINoComp #varSISubsidiaryNos"}
        }.Where(it => it.Item2 is not null)
        .Select(it => new Reference(it.Item1, it.Item2));

    public XNode Build() => new XElement(
            XmlExt.AknNamespace + "block",
            new XAttribute("name", "number"),
            IsWellFormed()
            ? new XElement(
                XmlExt.AknNamespace + "docNumber",
                new XElement(
                    XmlExt.AknNamespace + "ref",
                    // class needs the namespace to prevent removal in the Simplifier
                    new XAttribute(XmlExt.AknNamespace + "class", "placeholder"),
                    new XAttribute("href", "#varSINo")))
            : new XText(Line.NormalizedContent)

        );

    public static Number? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is not WLine line)
        {
            return null;
        }
        if (!line.HasStyle(STYLE))
        {
            return null;
        }
        // parser.LanguageService.IsMatch
        MatchCollection? matches = parser.LanguageService.IsMatch(
            line.NormalizedContent,
            LanguagePatterns);
        if (matches?.Count > 1)
        {
            Logger.LogWarning("""
                Regular expression for SI Preface Number parsing had\
                multiple matches. This is unexpected! Only the first\
                will be taken
                """);
        }
        Match? match = matches?.FirstOrDefault();
        if (match == null)
        {
            // Not well-formed number, so just put the line to avoid losing information
             return new(line);
        }
        return new(line,
            match.Groups["year"]?.Value,
            match.Groups["number"]?.Value,
            match.Groups["subsidiary"]?.Value);


    }
    // Subsidary numbers may be null
    private bool IsWellFormed() => Year is not null && SINumber is not null;

    [GeneratedRegex(@" *(?<year>\d+)? *No\. *(?<number>\d+)? *(?<subsidiary>.+)?")]
    private static partial Regex EnglishRegex();

    [GeneratedRegex(@" *(?<year>\d+)? *Rhif *(?<number>\d+)? *(?<subsidiary>.+)?")]
    private static partial Regex WelshRegex();

    private static readonly Dictionary<LanguageService.Lang, IEnumerable<Regex>> LanguagePatterns = new()
    {
        [LanguageService.Lang.ENG] = [ EnglishRegex() ],
        [LanguageService.Lang.CYM] = [ WelshRegex() ]
    };
}

partial record Subjects(IEnumerable<IBlock> subjects) : IBlock, IBuildable<XNode>
{
    public XNode Build() => new XElement(
        XmlExt.AknNamespace + "container",
        new XAttribute("name", "subjects"),
        from subject in subjects
        where subject is IBuildable<XNode>
        select (subject as IBuildable<XNode>)!.Build()
    );

    public static Subjects? Parse(IParser<IBlock> parser) =>
        parser.MatchWhile(Subject.Parse) is IEnumerable<IBlock> subjects
        && subjects.Any()
        ? new(subjects)
        : null;
}
partial record Subject(WLine Line, IEnumerable<Subsubject>? Subsubjects) : IBlock, IBuildable<XNode>
{
    public const string STYLE = "subject";
    public XNode Build() => new XElement(
        XmlExt.AknNamespace + "container",
        new XAttribute("name", "subject"),
        new XElement(
            XmlExt.AknNamespace + "block",
            new XAttribute("name", "subject"),
            new XElement(XmlExt.AknNamespace + "concept",
                new XAttribute(XmlExt.AknNamespace + "class", "title"),
                new XAttribute("refersTo", ""),
                new XText(Line.NormalizedContent))),
        Subsubjects?.Select(s => s.Build())
    );

    public static Subject? Parse(IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && line.Style == STYLE
            ? new Subject(line, parser.MatchWhile(Subsubject.Parse))
            : null;
}
partial record Subsubject(WLine Line) : IBlock, IBuildable<XNode>
{
    public const string STYLE = "Subsub";
    public XNode Build() => new XElement(
        XmlExt.AknNamespace + "block",
        new XAttribute("name", "subsubject"),
            new XElement(XmlExt.AknNamespace + "concept",
                new XAttribute(XmlExt.AknNamespace + "class", "subtitle"),
                new XAttribute("refersTo", ""),
                new XText(Line.NormalizedContent))
    );

    public static Subsubject? Parse(IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && line.Style == STYLE
            ? new Subsubject(line)
            : null;
}
partial record Title(WLine Line) : IBlock, IBuildable<XNode>, IMetadata
{
    public const string STYLE = "Title";

    public XNode Build() => new XElement(
        XmlExt.AknNamespace + "block",
        new XAttribute("name", "title"),
        new XElement(
            XmlExt.AknNamespace + "docTitle",
            new XElement(XmlExt.AknNamespace + "ref",
                new XAttribute(XmlExt.AknNamespace + "class", "placeholder"),
                new XAttribute("href", "#varSITitle"))));

    public IEnumerable<Reference> Metadata => [
        new Reference(ReferenceKey.varSITitle, Line.NormalizedContent)
    ];
    public static Title? Parse(IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && line.Style == STYLE
            ? new Title(line)
            : null;
}
partial record Approval(WLine Line) : IBlock, IBuildable<XNode>
{
    public const string STYLE = "Approval";
    public XNode Build() => new XElement(
        XmlExt.AknNamespace + "block",
        new XAttribute("name", "approval"),
        new XText(Line.NormalizedContent)
    );

    public static Approval? Parse(IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && line.Style == STYLE
            ? new Approval(line)
            : null;
}
partial record LaidDraft(WLine Line) : IBlock, IBuildable<XNode>
{
    public const string STYLE = "LaidDraft";
    public XNode Build() => new XElement(
        XmlExt.AknNamespace + "block",
        new XAttribute("name", "laidInDraft"),
        new XText(Line.NormalizedContent)
    );

    public static LaidDraft? Parse(IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && line.Style == STYLE
            ? new LaidDraft(line)
            : null;
}
partial record DateBlock(WLine Line) : IBlock, IBuildable<XNode>
{
    public static List<string> STYLES = ["Made", "Laid", "Coming"];

    public XNode Build() => new XElement(
        XmlExt.AknNamespace + "block",
        new XText(Line.NormalizedContent)
    );

    public static DateBlock? Parse(IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && STYLES.Contains(line.Style)
            ? new DateBlock(line)
            : null;
}