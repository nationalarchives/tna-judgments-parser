#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.Legislation.Judgments.Parse;

internal sealed partial class JudgeEnricher : Enricher
{
    private const string JusticeTitle = @"(?:the\s+)?(?:hon(?:ourable)?\.?\s+)?(?:mrs?|ms|miss|lord|lady)\.?\s+justice";
    private const string HonourJudgeTitle = @"(?:his|her)\s+honour\s+judge|hhj";

    private const string OfficeTitle =
        @"(?:(?:mrs?|ms|miss)\s+)?(?:(?:chief|costs|deputy|insolvency|and|companies|court|resident|senior|specialist|tribunal|upper)\s+)*(?:judge|master|member|recorder)";

    private const string JudicialTitle = $"(?i:{JusticeTitle}|{HonourJudgeTitle}|{OfficeTitle})";

    private const string CounselPostnominal = @"(?:KC|QC|K\.C\.?|Q\.C\.?)(?!\p{L})";
    private const string HonourPostnominal = @"[CDKMO]BE(?!\p{L})";
    private const string HonourPostnominals = $@"(?:,?\s+{HonourPostnominal})*";

    private const string Bracketed = @"\([^)]*\)";
    private const string NotStartingWithSpaceOrConnector = @"(?!\s|(?i:and)(?!\p{L})|&)";
    private const string NotAConnectorLabelOrDigit = $@"(?!{Connector})[^:\d]";
    private const string NameText = $"{NotStartingWithSpaceOrConnector}(?:{Bracketed}|{NotAConnectorLabelOrDigit})+?";
    private const string Name = $@"(?:(?<{TitleMatchGroup}>{JudicialTitle})\s+)?{NameText}";

    private const string PresidentRole = @",\s*(?i:vice[-\s]president|deputy\s+president|president)";
    private const string ParenthesisedRole = @"\s*\((?i:vice[-\s]president|specialist\s+member)[^)]*\)";
    private const string TribunalRole = @",\s*(?i:judge|member)\s+of\s+the\s+[\p{L}\s-]+?";
    private const string CourtRole = @"\s+of\s+the\s+[\p{L}\s]+?Courts?(?!\p{L})";
    private const string Role = $"(?:{PresidentRole}|{ParenthesisedRole}|{TribunalRole}|{CourtRole})";
    private const string SittingAs = @"\s+(?i:sitting\s+as)(?!\p{L}).*|\s*\((?i:sitting\s+as)(?!\p{L})[^)]*\)";

    private const string CounselJudge = $@"{Name}{HonourPostnominals},?\s+(?<{CounselMatchGroup}>{CounselPostnominal}){HonourPostnominals}(?<{RoleMatchGroup}>{Role})*";

    private const string OtherJudge = $"{Name}{HonourPostnominals}(?<{RoleMatchGroup}>{Role})*(?<{RoleMatchGroup}>{SittingAs})?";
    private const string Judge = $"(?<{JudgeMatchGroup}>{CounselJudge}|{OtherJudge})";

    private const string Connector = @"\s*,\s*(?:(?i:and|&)\s+)?|\s+-?\s*(?i:and|&)\s*-?\s+";
    private const string TrailingConnector = @"(?:\s*,\s*|\s+)(?i:and|&)";
    private const string RoleLabel = @"\s*:\s*(?i:judge|member)\s+of\s+the\s+.+";
    private const string Trail = $@"(?:{TrailingConnector}|{RoleLabel}|{SittingAs})?\s*[.;]?";
    private const string Judges = $"{Judge}(?:(?:{Connector}){Judge})*{Trail}";
    private const string Reference = @"\[[^\]]*\]";
    private const string MemberLabel = @"(?i:(?:specialist\s+|tribunal\s+)?member)\s*:\s*";

    private const string Heading =
        @"^\s*(?i:b\s*e\s*t\s*w\s*e\s*e\s*n|decided|heard|hearing|judgment|representation|in\s+the\s+matter\s+of|on\s*:)(?!\p{L})";

    private const string CrownParty =
        @"^\s*(?i:regina|v\.?)\s*$|^\s*(?i:r\s*\(\s*|the\s+(?:queen|king)\s+)(?i:on\s+the\s+application\s+of)";

    private const string PartyRole =
        @"(?<!\p{L})(?i:appellants?|applicants?|claimants?|defendants?|respondents?|petitioners?|interested\s+part(?:y|ies))(?!\p{L})";

    private const string Rule = @"^\P{L}*$";

    private const string JudgeMatchGroup = "judge";
    private const string TitleMatchGroup = "title";
    private const string RoleMatchGroup = "role";
    private const string CounselMatchGroup = "counsel";

    [GeneratedRegex(
        $@"^\s*(?i:b\s*e\s*f\s*o\s*r\s*e|decided\s+by)(?!\p{{L}})(?!\s+(?i:the)(?!\p{{L}}))\s*:?\s*(?:{Judges})?\s*$")]
    private static partial Regex IntroductionLine();

    [GeneratedRegex($@"^\s*(?:{MemberLabel})?(?:\(\s*{Judges}\s*\)|{Judges})(?:\s*{Reference})?\s*$")]
    private static partial Regex JudgesLine();

    [GeneratedRegex(@"^\s*\(?\s*(?i:sitting\s+as|(?:vice[-\s])?president\s+of|(?:judge|member)\s+of)(?!\p{L})")]
    private static partial Regex RoleLine();

    [GeneratedRegex($"{Heading}|{CrownParty}|{PartyRole}|{Rule}")]
    private static partial Regex EndOfJudges();

    [GeneratedRegex(@"^\s*(?i:on\s+appeal\s+from)(?!\p{L})")]
    private static partial Regex CourtAppealedFrom();

    private enum Stage
    {
        LookingForJudgesWithoutIntroduction,
        LookingForJudgesAheadOfIntroduction,
        LookingForIntroduction,
        ReadingJudges,
        Finished
    }

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        var header = blocks.ToList();
        var stage = HasIntroduction(header)
            ? Stage.LookingForJudgesAheadOfIntroduction
            : Stage.LookingForJudgesWithoutIntroduction;
        var enriched = new List<IBlock>();
        foreach (var block in header)
        {
            if (stage != Stage.Finished && block is WLine line)
            {
                enriched.Add(EnrichLine(line, ref stage));
            }
            else
            {
                stage = Stage.Finished;
                enriched.Add(block);
            }
        }

        return enriched;
    }

    private static bool HasIntroduction(IEnumerable<IBlock> blocks)
    {
        return blocks.TakeWhile(block => block is WLine)
                     .Cast<WLine>()
                     .SelectMany(SegmentTexts)
                     .Any(IntroductionLine().IsMatch);
    }

    private static string[] SegmentTexts(WLine line)
    {
        return string.Concat(line.Contents.Select(inline => inline is WLineBreak ? "\n" : IInline.GetText(inline)))
                     .Split('\n');
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        throw new NotSupportedException("Judges can only be found by reading the header from the start");
    }

    private static WLine EnrichLine(WLine line, ref Stage stage)
    {
        var contents = line.Contents.ToArray();
        var enriched = new List<IInline>();
        var segment = new List<IInline>();
        foreach (var inline in contents)
        {
            if (inline is not WLineBreak)
            {
                segment.Add(inline);
                continue;
            }

            enriched.AddRange(EnrichSegment(segment, ref stage));
            enriched.Add(inline);
            segment = [];
        }

        enriched.AddRange(EnrichSegment(segment, ref stage));
        return enriched.SequenceEqual(contents) ? line : WLine.Make(line, enriched);
    }

    private static IEnumerable<IInline> EnrichSegment(List<IInline> segment, ref Stage stage)
    {
        var text = string.Concat(segment.Select(IInline.GetText));
        return !string.IsNullOrWhiteSpace(text) && TryFindJudges(text, ref stage, out var judges)
            ? MarkUpJudges(segment, judges)
            : segment;
    }

    private static bool TryFindJudges(string text, ref Stage stage, out Match judges)
    {
        judges = Match.Empty;

        return stage switch
        {
            Stage.LookingForJudgesWithoutIntroduction => TryLookForJudgesWithoutIntroduction(text, ref stage,
                out judges),
            Stage.LookingForJudgesAheadOfIntroduction => TryLookForJudgesAheadOfIntroduction(text, ref stage,
                out judges),
            Stage.LookingForIntroduction => TryLookForIntroduction(text, ref stage, out judges),
            Stage.ReadingJudges => TryReadJudges(text, ref stage, out judges),
            _ => false
        };
    }

    private static bool TryLookForJudgesWithoutIntroduction(string text, ref Stage stage, out Match judges)
    {
        if (!EndOfJudges().IsMatch(text))
        {
            return TryFindUnmistakableJudge(text, out judges);
        }

        stage = Stage.Finished;
        judges = Match.Empty;
        return false;
    }

    private static bool TryLookForJudgesAheadOfIntroduction(string text, ref Stage stage, out Match judges)
    {
        if (IntroductionLine().IsMatch(text))
        {
            return TryLookForIntroduction(text, ref stage, out judges);
        }

        if (!EndOfJudges().IsMatch(text) && !CourtAppealedFrom().IsMatch(text))
        {
            return TryFindUnmistakableJudge(text, out judges);
        }

        stage = Stage.LookingForIntroduction;
        judges = Match.Empty;
        return false;
    }

    private static bool TryLookForIntroduction(string text, ref Stage stage, out Match judges)
    {
        var introduction = IntroductionLine().Match(text);
        if (introduction.Success)
        {
            stage = Stage.ReadingJudges;
        }

        judges = introduction.Groups[JudgeMatchGroup].Success ? introduction : Match.Empty;
        return judges.Success;
    }

    private static bool TryReadJudges(string text, ref Stage stage, out Match judges)
    {
        judges = Match.Empty;
        if (EndOfJudges().IsMatch(text))
        {
            stage = Stage.Finished;
            return false;
        }

        if (RoleLine().IsMatch(text))
        {
            return false;
        }

        judges = JudgesLine().Match(text);
        return judges.Success;
    }

    private static bool TryFindUnmistakableJudge(string text, out Match judges)
    {
        var match = JudgesLine().Match(text);

        if (match.Success
            && match.Groups[JudgeMatchGroup].Captures.Count == 1
            && (match.Groups[TitleMatchGroup].Success || match.Groups[RoleMatchGroup].Success || match.Groups[CounselMatchGroup].Success))
        {
            judges = match;
        }
        else
        {
            judges = Match.Empty;
        }

        return judges.Success;
    }

    private static IEnumerable<IInline> MarkUpJudges(List<IInline> segment, Match match)
    {
        if (!segment.All(IsPlainTextOrTab))
        {
            return segment;
        }

        var enriched = segment.ToArray();
        foreach (var judge in match.Groups[JudgeMatchGroup].Captures.Reverse())
        {
            enriched = EnrichFromBeginning.Enrich(enriched, judge.Index, judge.Index + judge.Length, MakeJudge);
        }

        return enriched;
    }

    private static bool IsPlainTextOrTab(IInline inline)
    {
        return inline is ITab || inline.GetType() == typeof(WText);
    }

    private static WJudge MakeJudge(IList<IInline> inlines)
    {
        return new WJudge(IInline.ToString(inlines), inlines.OfType<WText>().First().properties);
    }
}
