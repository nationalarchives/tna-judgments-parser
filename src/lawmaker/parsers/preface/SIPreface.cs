#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Lawmaker.Date;
using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Preface;

// Note: eIds are set here due to a shortcoming in Lawmaker's eId
// population logic. Ideally the parser doesn't have to worry about eIds!
internal partial record SIPreface(IEnumerable<IBlock> Blocks) : IBlock, IBuildable<XNode>
{

    public XNode Build(Document document) => !Blocks
        .All(b => b is IBuildable<XNode>)
        ? throw new System.Exception("IBuildable<XNode> not implemented for all children!")
        : new XElement(
            akn + "preface",
            new XAttribute("eId", "fnt"),
            Blocks.OfType<IBuildable<XNode>>()
            .Select(it => it.Build(document))
        );

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
        parser.MatchWhile(
            block => !TableOfContents.IsTableOfContentsHeading(block, parser.LanguageService),
            PrefaceLine
        ) is IEnumerable<IBlock> lines
        && lines.Any(block => block is DatesContainer) // always expect a date contianer in the preface
        ? new(lines) // skip over DateBlock for now - handled in later ticket
        : null;


    // for now, order of the preface elements doesn't matter as we only
    // rely on the MS Word Style, however it may be more useful to enforce
    // the order of preface elements
    private static IBlock? PrefaceLine(IParser<IBlock> parser) =>
        parser.Match<IBlock>(
            CorrectionRubric.Parse,
            ProceduralRubric.Parse,
            Banner.Parse,
            Number.Parse,
            Subjects.Parse,
            Title.Parse,
            Approval.Parse,
            LaidDraft.Parse,
            DatesContainer.Parse
        );
}

partial record CorrectionRubric(WLine Line) : IBlock, IBuildable<XNode>
{
    public XNode Build(Document _) => new XElement(
        akn + "block",
        new XAttribute("name", "correctionRubric"),
        new XText(Line.NormalizedContent)
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
    public XNode Build(Document _) => new XElement(
        akn + "block",
        new XAttribute("name", "proceduralRubric"),
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
    public XNode Build(Document _) => new XElement(
        akn + "block",
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
    ) : IBlock, IBuildable<XNode>
{
    public const string STYLE = "Number";

    private static readonly ILogger Logger = Logging.Factory.CreateLogger<Number>();

    public XNode Build(Document document)
    {
        document.Metadata.Register(new Reference(ReferenceKey.varSIYear, Year ?? ""));
        document.Metadata.Register(new Reference(ReferenceKey.varSINoComp, SINumber ?? ""));
        document.Metadata.Register(new Reference(ReferenceKey.varSISubsidiaryNos, SISubsidiaryNumbers ?? ""));
        return new XElement(
            akn + "block",
            new XAttribute("name", "number"),
            IsWellFormed()
            ? new XElement(
                akn + "docNumber",
                new XElement(
                    akn + "ref",
                    // class needs the namespace to prevent removal in the Simplifier
                    new XAttribute(akn + "class", "placeholder"),
                    // varSINo is a combination of the other 3 refs
                    // so hard-coding it here *should* be fine
                    new XAttribute("href", "#varSINo")))
            : new XText(Line.NormalizedContent)

        );
    }

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
        [LanguageService.Lang.EN] = [ EnglishRegex() ],
        [LanguageService.Lang.CY] = [ WelshRegex() ]
    };
}

partial record Subjects(IEnumerable<Subject> subjects) : IBlock, IBuildable<XNode>
{
    public XNode Build(Document document) => new XElement(
        akn + "container",
        new XAttribute("name", "subjects"),
        subjects.Select((subject, i) => subject.Build(document))
    );

    public static Subjects? Parse(IParser<IBlock> parser) =>
        parser.MatchWhile(Subject.Parse) is IEnumerable<Subject> subjects
        && subjects.Any()
        ? new(subjects)
        : null;
}
partial record Subject(WLine Line, IEnumerable<Subsubject>? Subsubjects) : IBlock
{
    public const string STYLE = "subject";
    public XNode Build(Document document) => new XElement(
        akn + "container",
        new XAttribute("name", "subject"),
        new XElement(
            akn + "block",
            new XAttribute("name", "subject"),
            new XElement(akn + "concept",
                new XAttribute(akn + "class", "title"),
                new XAttribute("refersTo", ""),
                new XText(Line.NormalizedContent))),
        Subsubjects?.Select(s => s.Build(document))
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
    public XNode Build(Document _) => new XElement(
        akn + "block",
        new XAttribute("name", "subsubject"),
            new XElement(akn + "concept",
                new XAttribute(akn + "class", "subtitle"),
                new XAttribute("refersTo", ""),
                new XText(Line.NormalizedContent))
    );

    public static Subsubject? Parse(IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && line.Style == STYLE
            ? new Subsubject(line)
            : null;
}
partial record Title(WLine Line) : IBlock, IBuildable<XNode>
{
    public const string STYLE = "Title";

    public XNode Build(Document document) => new XElement(
        akn + "block",
        new XAttribute("name", "title"),
        new XElement(
            akn + "docTitle",
            new XElement(akn + "ref",
                new XAttribute(akn + "class", "placeholder"),
                new XAttribute("href", $"#{document.Metadata.Register(new Reference(ReferenceKey.varSITitle, Line.NormalizedContent)).EId}"))));

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
    public XNode Build(Document _) => new XElement(
        akn + "block",
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
    public XNode Build(Document _) => new XElement(
        akn + "block",
        new XAttribute("name", "laidInDraft"),
        new XText(Line.NormalizedContent)
    );

    public static LaidDraft? Parse(IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && line.Style == STYLE
            ? new LaidDraft(line)
            : null;
}