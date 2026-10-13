#nullable enable

using System.Linq;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

namespace test.parsers.common.enrich;

public class TestJudgeEnricher : ParserTestBase
{
    private static readonly JudgeEnricher Enricher = new();

    [Theory]
    [InlineData("LORD JUSTICE ALPHA")]
    [InlineData("LADY JUSTICE BRAVO")]
    [InlineData("MR JUSTICE CHARLIE")]
    [InlineData("Mr Justice Delta")]
    [InlineData("MRS JUSTICE ECHO DBE")]
    [InlineData("Mrs Justice o'Echo")]
    [InlineData("THE HONOURABLE MR JUSTICE FOXTROT")]
    [InlineData("THE HONOURABLE MRS JUSTICE GOLF")]
    [InlineData("The Honourable Mr Justice Hotel India")]
    [InlineData("THE HON. MR JUSTICE JULIETT")]
    [InlineData("THE HON MR JUSTICE KILO LIMA CBE")]
    [InlineData("HIS HONOUR JUDGE MIKE")]
    [InlineData("HHJ NOVEMBER")]
    [InlineData("TRIBUNAL JUDGE ALPHA")]
    [InlineData("TRIBUNAL JUDGE BRAVO QC")]
    [InlineData("UPPER TRIBUNAL JUDGE CHARLIE")]
    [InlineData("RESIDENT JUDGE SIERRA")]
    [InlineData("DEPUTY MASTER OSCAR")]
    [InlineData("SENIOR MASTER PAPA")]
    [InlineData("MISS RECORDER QUEBEC ROMEO")]
    [InlineData("Sir Tango Uniform")]
    [InlineData("Lord Victor")]
    [InlineData("Dame Whiskey Xray")]
    [InlineData("Mr Yankee Zulu, VICE PRESIDENT")]
    [InlineData("MR YANKEE ZULU, VICE PRESIDENT")]
    [InlineData("Mr Yankee Zulu (Vice-President)")]
    [InlineData("Mr Yankee Zulu, President")]
    [InlineData("Charlie Delta, Judge of the Upper Tribunal")]
    [InlineData("HER HONOUR JUDGE ALPHA, Judge of the Upper Tribunal")]
    [InlineData("Mr Justice Bravo, Judge of the Upper Tribunal")]
    [InlineData("LORD JUSTICE ROMEO (Vice-President, Court of Appeal, Civil Division)")]
    [InlineData("Deputy Insolvency and Companies Court Judge Alpha Bravo KC")]
    [InlineData("Chief Insolvency and Companies Court Judge Charlie QC")]
    [InlineData("Mr Justice Delta of the Technology and Construction Court")]
    [InlineData("Mr Justice Echo of the Business and Property Courts")]
    [InlineData("MR ECHO FOXTROT sitting as a Deputy Judge of the High Court")]
    [InlineData("HHJ Tango sitting as a s9 Deputy High Court Judge")]
    [InlineData("Mr Golf Hotel")]
    [InlineData("Ms India Juliett")]
    [InlineData("Mr Yankee Zulu, KC")]
    [InlineData("MEMBER CHARLIE DELTA")]
    [InlineData("TRIBUNAL MEMBER ECHO FOXTROT")]
    [InlineData("Specialist Tribunal Member Golf Hotel")]
    [InlineData("TRIBUNAL JUDGE KILO LIMA (Specialist Member)")]
    [InlineData("Juliett India (Specialist member)")]
    [InlineData("Zoë Échaud")]
    [InlineData("ZOË ÉCHAUD")]
    [InlineData("Élodie Échaud")]
    [InlineData("Alpha O'Bravo")]
    [InlineData("Alpha O’Bravo")]
    [InlineData("Charlie d’Delta")]
    [InlineData("Echo McFoxtrot")]
    [InlineData("ECHO MacFOXTROT")]
    [InlineData("Golf Hotel-India")]
    [InlineData("Juliett de Kilo")]
    [InlineData("Lima van der Mike")]
    [InlineData("November ap Oscar")]
    [InlineData("Papa Ó Quebec")]
    [InlineData("J. R. Romeo")]
    [InlineData("J.R. Romeo")]
    [InlineData("Sierra St. Tango")]
    [InlineData("Uniform Victor")]
    [InlineData("Квебек Ромео")]
    [InlineData("whiskey xray")]
    [InlineData("Yankee")]
    [InlineData("ZULU")]
    [InlineData("Alpha bravo-Charlie de la delta")]
    [InlineData("李明")]
    [InlineData("Deputy Upper Tribunal Judge Oscarland")]
    [InlineData("Papa Quebecand")]
    public void Enrich_JudgeOnItsOwnLineAfterIntroduction_IsMarkedUpAsOneJudge(string judge)
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine(judge, BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>(judge, BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Theory]
    [InlineData("Before :")]
    [InlineData("Before:")]
    [InlineData("Before")]
    [InlineData("Decided by:")]
    public void Enrich_EachIntroductionWording_MarksUpTheJudgeOnTheNextLine(string introduction)
    {
        IBlock[] blocks =
        [
            StyledLine(introduction, BoldStyling),
            StyledLine("TRIBUNAL JUDGE DELTA", BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("TRIBUNAL JUDGE DELTA", BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Theory]
    [InlineData("BEFORE ", "DEPUTY INSOLVENCY AND COMPANIES JUDGE GAMMA KC")]
    [InlineData("Before: ", "TRIBUNAL JUDGE DELTA")]
    [InlineData("Before: ", "Oscar Papa")]
    [InlineData("Before: ", "Zulu Alpha Q.C.")]
    [InlineData("Before: ", "Bravo Charlie K.C")]
    public void Enrich_JudgeOnSameLineAsIntroduction_IsMarkedUp(string before, string judgeName)
    {
        IBlock[] blocks =
        [
            StyledLine($"{before}{judgeName}", BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();
        var contents = result[0].ShouldBeOfType<WLine>().Contents.ToArray();

        result.Length.ShouldBe(2);
        contents.Length.ShouldBe(2);
        contents[0].ShouldMatch<WText>(before, BoldStyling);
        contents[1].ShouldMatch<WJudge>(judgeName, BoldStyling);
        result[1].ShouldBeSameAs(blocks[1]);
    }

    [Fact]
    public void Enrich_JudgeOnSameLineAsIntroductionAndFurtherJudgesOnNextLines_MarksUpEveryJudge()
    {
        IBlock[] blocks =
        [
            StyledLine("Before: Lord Yankee", BoldStyling),
            StyledLine("Lady Zulu", BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();
        var contents = result[0].ShouldBeOfType<WLine>().Contents.ToArray();

        result.Length.ShouldBe(3);
        contents.Length.ShouldBe(2);
        contents[0].ShouldMatch<WText>("Before: ", BoldStyling);
        contents[1].ShouldMatch<WJudge>("Lord Yankee", BoldStyling);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("Lady Zulu", BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Fact]
    public void Enrich_SeveralJudgesOnSeparateLines_MarksUpEachJudge()
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine("LORD JUSTICE OSCAR PAPA", BoldStyling),
            StyledLine("Alpha Bravo", BoldStyling),
            StyledLine("and ", BoldStyling),
            StyledLine("Charlie Delta", BoldStyling),
            StyledLine("TRIBUNAL JUDGE MIKE NOVEMBER", BoldStyling),
            StyledLine("- - -", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(7);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("LORD JUSTICE OSCAR PAPA", BoldStyling);
        result[2].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("Alpha Bravo", BoldStyling);
        result[3].ShouldBeSameAs(blocks[3]);
        result[4].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("Charlie Delta", BoldStyling);
        result[5].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("TRIBUNAL JUDGE MIKE NOVEMBER", BoldStyling);
        result[6].ShouldBeSameAs(blocks[6]);
    }

    [Fact]
    public void Enrich_LinesBeforeTheIntroduction_AreLeftUnchanged()
    {
        IBlock[] blocks =
        [
            StyledLine("Date: 15th April 2003", BoldStyling),
            StyledLine("Before :", BoldStyling),
            StyledLine("LORD JUSTICE OSCAR PAPA", BoldStyling),
            StyledLine("- - -", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(4);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeSameAs(blocks[1]);
        result[2].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("LORD JUSTICE OSCAR PAPA", BoldStyling);
        result[3].ShouldBeSameAs(blocks[3]);
    }

    [Fact]
    public void Enrich_LinesAfterTheTerminator_AreLeftUnchanged()
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine("LORD JUSTICE OSCAR PAPA", BoldStyling),
            StyledLine("- - -", BoldStyling),
            StyledLine("LORD JUSTICE QUEBEC", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(4);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("LORD JUSTICE OSCAR PAPA", BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
        result[3].ShouldBeSameAs(blocks[3]);
    }

    [Fact]
    public void Enrich_BeforeInsideTheJudgmentRatherThanAtTheStart_IsLeftUnchanged()
    {
        IBlock[] blocks =
        [
            TableOf(["JUDGMENT"]),
            StyledLine("Before :", BoldStyling),
            StyledLine("LORD JUSTICE OSCAR PAPA", BoldStyling),
            StyledLine("- - -", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(4);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeSameAs(blocks[1]);
        result[2].ShouldBeSameAs(blocks[2]);
        result[3].ShouldBeSameAs(blocks[3]);
    }

    [Fact]
    public void Enrich_BeforeFollowedByWordsThatAreNotAJudge_IsNotAnIntroduction()
    {
        IBlock[] blocks =
        [
            StyledLine("Before the Upper Tribunal", BoldStyling),
            StyledLine("MR JUSTICE OSCAR", BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("MR JUSTICE OSCAR", BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Theory]
    [InlineData("MR JUSTICE CHARLIE")]
    [InlineData("TRIBUNAL JUDGE ALPHA")]
    [InlineData("Lord Alpha, President")]
    [InlineData("Golf Hotel (Sitting as a Deputy High Court Judge)")]
    [InlineData("Specialist Tribunal Member Echo Foxtrot")]
    [InlineData("Charlie Delta KC")]
    [InlineData("Mr Justice Échaud")]
    [InlineData("MR JUSTICE ÉCHAUD")]
    [InlineData("Mr Justice Échaud")]
    [InlineData("Lord Justice O’Foxtrot")]
    [InlineData("MR JUSTICE MacHOTEL")]
    [InlineData("Lady Justice van der Juliett")]
    [InlineData("His Honour Judge Ó Mike")]
    [InlineData("HHJ J.R. November")]
    [InlineData("Mr Justice St. Oscar")]
    [InlineData("Mr Justice Papa Quebec")]
    [InlineData("Mr Justice Квебек")]
    [InlineData("Zoë Brontë-Ålvik KC")]
    [InlineData("Alpha O'Bravo QC")]
    [InlineData("Juliett de Kilo KC")]
    [InlineData("Charlie d’Delta, President")]
    [InlineData("J. R. Romeo (Sitting as a Deputy High Court Judge)")]
    public void Enrich_NoBeforeAndJudgeTitleRoleOrCounsel_MarksUpJudge(string judge)
    {
        IBlock[] blocks =
        [
            StyledLine("Date: 15th April 2003", BoldStyling),
            StyledLine(judge, BoldStyling),
            StyledLine("- - -", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>(judge, BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Theory]
    [InlineData("Mr Alpha Bravo")]
    [InlineData("Sir Tango Uniform")]
    [InlineData("whiskey xray")]
    [InlineData("Zoë Échaud")]
    [InlineData("Élodie Échaud")]
    [InlineData("Alpha O’Bravo")]
    [InlineData("Lima van der Mike")]
    [InlineData("J.R. Romeo")]
    [InlineData("Sierra St. Tango")]
    [InlineData("Квебек Ромео")]
    public void Enrich_NoBeforeAndNoJudgeTitleRoleOrCounsel_LeavesLinesUnchanged(string name)
    {
        IBlock[] blocks =
        [
            StyledLine("Date: 15th April 2003", BoldStyling),
            StyledLine(name, BoldStyling),
            StyledLine("- - -", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeSameAs(blocks[1]);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Theory]
    [InlineData("Mr Justice Charlie")]
    [InlineData("HIS HONOUR JUDGE DELTA")]
    [InlineData("ECHO FOXTROT QC")]
    [InlineData("Golf Hotel sitting as a Deputy High Court Judge")]
    [InlineData("(HIS HONOUR JUDGE INDIA) [12U34567890]")]
    public void Enrich_JudgeOfTheCourtAppealedFrom_IsLeftUnchanged(string lowerCourtJudge)
    {
        IBlock[] blocks =
        [
            StyledLine("ON APPEAL FROM THE HIGH COURT OF JUSTICE", BoldStyling),
            StyledLine(lowerCourtJudge, BoldStyling),
            StyledLine("Before :", BoldStyling),
            StyledLine("LORD JUSTICE INDIA", BoldStyling),
            StyledLine("- - -", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(5);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeSameAs(blocks[1]);
        result[2].ShouldBeSameAs(blocks[2]);
        result[3].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("LORD JUSTICE INDIA", BoldStyling);
        result[4].ShouldBeSameAs(blocks[4]);
    }

    [Fact]
    public void Enrich_BracketedJudgeOfTheCourtAppealedFromWithNoIntroduction_IsMarkedUp()
    {
        IBlock[] blocks =
        [
            StyledLine("ON APPEAL FROM THE CROWN COURT AT NEWCASTLE UPON TYNE.", BoldStyling),
            StyledLine("(HIS HONOUR JUDGE INDIA) [12U34567890]", ItalicStyling),
            TextLine("Case No 2025/01234/A1", "Tuesday 27 October 2026")
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);

        var contents = result[1].ShouldBeOfType<WLine>().Contents.ToArray();
        contents.Length.ShouldBe(3);
        contents[0].ShouldMatch<WText>("(", ItalicStyling);
        contents[1].ShouldMatch<WJudge>("HIS HONOUR JUDGE INDIA", ItalicStyling);
        contents[2].ShouldMatch<WText>(") [12U34567890]", ItalicStyling);

        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Fact]
    public void Enrich_TwoNamesWithoutTitleOnOneLine_AreMarkedUp()
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine("Kilo Lima, Mike November", BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();
        var contents = result[1].ShouldBeOfType<WLine>().Contents.ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        contents.Length.ShouldBe(3);
        contents[0].ShouldMatch<WJudge>("Kilo Lima", BoldStyling);
        contents[1].ShouldMatch<WText>(", ", BoldStyling);
        contents[2].ShouldMatch<WJudge>("Mike November", BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Theory]
    [InlineData("Licence number: OF2043707")]
    [InlineData("Decision date: 1 March 2022")]
    [InlineData("----------------------")]
    [InlineData("1 March 2022")]
    [InlineData("Courtroom 5")]
    public void Enrich_LineWithALabelDigitsOrNoLetters_IsNotAJudge(string line)
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine("Alpha Bravo", BoldStyling),
            StyledLine(line, BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(4);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("Alpha Bravo", BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
        result[3].ShouldBeSameAs(blocks[3]);
    }

    [Theory]
    [InlineData("MEMBER: ", "ALPHA BRAVO")]
    [InlineData("Tribunal Member: ", "Charlie Delta")]
    [InlineData("Specialist Member:", "Echo Foxtrot")]
    public void Enrich_LabelBeforeName_IsLeftOutOfTheJudge(string label, string name)
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine(label + name, BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();
        var contents = result[1].ShouldBeOfType<WLine>().Contents.ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        contents.Length.ShouldBe(2);
        contents[0].ShouldMatch<WText>(label, BoldStyling);
        contents[1].ShouldMatch<WJudge>(name, BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Theory]
    [InlineData("Kilo Lima", ": Judge of the Upper Tribunal")]
    [InlineData("Mike November", ": Member of the Upper Tribunal")]
    public void Enrich_LabelAfterName_IsLeftOutOfTheJudge(string name, string label)
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine(name + label, BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);

        var contents = result[1].ShouldBeOfType<WLine>().Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldMatch<WJudge>(name, BoldStyling);
        contents[1].ShouldMatch<WText>(label, BoldStyling);

        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Theory]
    [InlineData("Member of the Upper Tribunal")]
    [InlineData("(Sitting as a Deputy High Court Judge)")]
    [InlineData("SITTING AS A JUDGE OF THE HIGH COURT")]
    [InlineData("Sitting As A Deputy Judge Of The High Court")]
    [InlineData("PRESIDENT OF THE KING’S BENCH DIVISION")]
    public void Enrich_RoleLineAmongJudges_IsNotMarkedUp(string roleLine)
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine(roleLine, BoldStyling),
            StyledLine("Alpha Bravo", BoldStyling),
            StyledLine("Charlie Delta", BoldStyling),
            StyledLine(roleLine, BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(6);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeSameAs(blocks[1]);
        result[2].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("Alpha Bravo", BoldStyling);
        result[3].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("Charlie Delta", BoldStyling);
        result[4].ShouldBeSameAs(blocks[4]);
        result[5].ShouldBeSameAs(blocks[5]);
    }

    [Theory]
    [InlineData(" and ")]
    [InlineData(" And ")]
    [InlineData(" & ")]
    [InlineData(", ")]
    [InlineData(", and ")]
    [InlineData(", & ")]
    [InlineData(" -and- ")]
    [InlineData(" - and - ")]
    public void Enrich_JudgesSeparatedByConnector_MarksUpEachJudge(string connector)
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine($"LORD JUSTICE KILO{connector}LADY JUSTICE LIMA", BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();
        var contents = result[1].ShouldBeOfType<WLine>().Contents.ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        contents.Length.ShouldBe(3);
        contents[0].ShouldMatch<WJudge>("LORD JUSTICE KILO", BoldStyling);
        contents[1].ShouldMatch<WText>(connector, BoldStyling);
        contents[2].ShouldMatch<WJudge>("LADY JUSTICE LIMA", BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Fact]
    public void Enrich_SeveralJudgesWithTrailingAnd_MarksUpEachJudge()
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine("LORD JUSTICE MIKE, LORD JUSTICE NOVEMBER, LADY JUSTICE OSCAR and", BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();
        var contents = result[1].ShouldBeOfType<WLine>().Contents.ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        contents.Length.ShouldBe(6);
        contents[0].ShouldMatch<WJudge>("LORD JUSTICE MIKE", BoldStyling);
        contents[1].ShouldMatch<WText>(", ", BoldStyling);
        contents[2].ShouldMatch<WJudge>("LORD JUSTICE NOVEMBER", BoldStyling);
        contents[3].ShouldMatch<WText>(", ", BoldStyling);
        contents[4].ShouldMatch<WJudge>("LADY JUSTICE OSCAR", BoldStyling);
        contents[5].ShouldMatch<WText>(" and", BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Fact]
    public void Enrich_SeveralJudgesWithRoleOnOneLine_MarksUpEachJudgeWithItsRole()
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine("Lord Papa, President and Lord Quebec", BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();
        var contents = result[1].ShouldBeOfType<WLine>().Contents.ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        contents.Length.ShouldBe(3);
        contents[0].ShouldMatch<WJudge>("Lord Papa, President", BoldStyling);
        contents[1].ShouldMatch<WText>(" and ", BoldStyling);
        contents[2].ShouldMatch<WJudge>("Lord Quebec", BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Theory]
    [InlineData("JOHN DOE KC", " SITTING AS A DEPUTY HIGH COURT JUDGE")]
    [InlineData("Richard Roe QC", " (sitting as a Deputy Judge of the High Court)")]
    public void Enrich_SittingAsAfterCounselNameOnTheSameLine_IsNotPartOfTheJudge(string judge, string sittingAs)
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine(judge + sittingAs, BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();
        var contents = result[1].ShouldBeOfType<WLine>().Contents.ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        contents.Length.ShouldBe(2);
        contents[0].ShouldMatch<WJudge>(judge, BoldStyling);
        contents[1].ShouldMatch<WText>(sittingAs, BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Fact]
    public void Enrich_SittingAsAfterLineBreakOnSameLine_IsNotMarkedUp()
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            new WLine(LineTemplate,
            [
                new WText("MR CHARLIE DELTA", BoldStyling), new WLineBreak(),
                new WText("sitting as a Deputy Judge of the High Court", BoldStyling)
            ]),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();
        var contents = result[1].ShouldBeOfType<WLine>().Contents.ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        contents.Length.ShouldBe(3);
        contents[0].ShouldMatch<WJudge>("MR CHARLIE DELTA", BoldStyling);
        contents[1].ShouldBeOfType<WLineBreak>();
        contents[2].ShouldMatch<WText>("sitting as a Deputy Judge of the High Court", BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Theory]
    [InlineData("Between")]
    [InlineData("B E T W E E N :")]
    [InlineData("Decided")]
    [InlineData("On: 25 March 2022")]
    [InlineData("Representation:")]
    [InlineData("Hearing date: 25 March 2022")]
    [InlineData("Heard on 25 March 2022")]
    [InlineData("- - - - - - - - - - - - - - - - - - - - -")]
    [InlineData("Appellant:")]
    [InlineData("Applicants")]
    [InlineData("ECHO FOXTROT LIMITED Respondent")]
    [InlineData("Claimant: Golf Hotel")]
    [InlineData("Defendants")]
    [InlineData("Interested Party")]
    [InlineData("JUDGMENT GIVEN ON 10 November 2021")]
    [InlineData("Judgment on Appeal under Regulation 29")]
    [InlineData("IN THE MATTER OF")]
    [InlineData("REGINA")]
    [InlineData("R (on the application of Golf Hotel)")]
    [InlineData("v")]
    public void Enrich_Terminator_EndsTheJudges(string terminator)
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine("Alpha Bravo", BoldStyling),
            StyledLine(terminator, BoldStyling),
            StyledLine("Charlie Delta", BoldStyling),
            StyledLine("LORD JUSTICE ECHO FOXTROT", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(5);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("Alpha Bravo", BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
        result[3].ShouldBeSameAs(blocks[3]);
        result[4].ShouldBeSameAs(blocks[4]);
    }

    [Theory]
    [InlineData("Between :")]
    [InlineData("- - - - - - - - - - - - - - - - - - - - -")]
    [InlineData("JUDGMENT GIVEN ON")]
    public void Enrich_BeforeFollowedOnlyByATerminator_LeavesLinesUnchanged(string terminator)
    {
        IBlock[] blocks =
        [
            StyledLine("Before :", BoldStyling),
            StyledLine(terminator, BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeSameAs(blocks[1]);
    }

    [Theory]
    [InlineData("Whiskey Xray", ".")]
    [InlineData("Whiskey Xray", ";")]
    [InlineData("Lord Justice Yankee", ".")]
    public void Enrich_PunctuationAfterName_IsNotPartOfTheJudge(string judge, string punctuation)
    {
        IBlock[] blocks =
        [
            StyledLine($"Before: {judge}{punctuation}", BoldStyling),
            StyledLine("Between", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();
        var contents = result[0].ShouldBeOfType<WLine>().Contents.ToArray();

        result.Length.ShouldBe(2);
        contents.Length.ShouldBe(3);
        contents[0].ShouldMatch<WText>("Before: ", BoldStyling);
        contents[1].ShouldMatch<WJudge>(judge, BoldStyling);
        contents[2].ShouldMatch<WText>(punctuation, BoldStyling);
        result[1].ShouldBeSameAs(blocks[1]);
    }

    [Fact]
    public void Enrich_JudgeInSeparateRunFromIntroduction_KeepsBothRuns()
    {
        IInline[] lineContents =
            [new WText("Before: ", BoldStyling), new WText("Tribunal Judge Alpha", UnderlineStyling)];
        IBlock[] blocks = [new WLine(LineTemplate, lineContents)];

        var contents = Enricher.Enrich(blocks).ShouldHaveSingleItem().ShouldBeOfType<WLine>().Contents.ToArray();

        contents.Length.ShouldBe(2);
        contents[0].ShouldBeSameAs(lineContents[0]);
        contents[1].ShouldMatch<WJudge>("Tribunal Judge Alpha", UnderlineStyling);
    }

    [Fact]
    public void Enrich_TabBetweenIntroductionAndJudge_KeepsTheTab()
    {
        IInline[] lineContents =
            [new WText("Before:", BoldStyling), Tab(), new WText("Tribunal Judge Alpha", UnderlineStyling)];
        IBlock[] blocks = [new WLine(LineTemplate, lineContents)];

        var contents = Enricher.Enrich(blocks).ShouldHaveSingleItem().ShouldBeOfType<WLine>().Contents.ToArray();

        contents.Length.ShouldBe(3);
        contents[0].ShouldBeSameAs(lineContents[0]);
        contents[1].ShouldBeSameAs(lineContents[1]);
        contents[2].ShouldMatch<WJudge>("Tribunal Judge Alpha", UnderlineStyling);
    }

    [Fact]
    public void Enrich_JudgesSeparatedByLineBreaksInTheBeforeLine_MarksUpEachJudge()
    {
        IInline[] lineContents =
        [
            new WText("before", ItalicStyling),
            new WLineBreak(),
            new WLineBreak(),
            new WText("Lord Alpha, President", BoldStyling),
            new WLineBreak(),
            new WText("Lord Bravo, Deputy President", BoldStyling),
            new WLineBreak(),
            new WText("Lord Charlie-Delta", BoldStyling),
            new WLineBreak(),
            new WText("Lady Echo", BoldStyling)
        ];
        IBlock[] blocks = [new WLine(LineTemplate, lineContents)];

        var contents = Enricher.Enrich(blocks).ShouldHaveSingleItem().ShouldBeOfType<WLine>().Contents.ToArray();

        contents.Length.ShouldBe(10);
        contents[0].ShouldBeSameAs(lineContents[0]);
        contents[1].ShouldBeSameAs(lineContents[1]);
        contents[2].ShouldBeSameAs(lineContents[2]);
        contents[3].ShouldMatch<WJudge>("Lord Alpha, President", BoldStyling);
        contents[4].ShouldBeSameAs(lineContents[4]);
        contents[5].ShouldMatch<WJudge>("Lord Bravo, Deputy President", BoldStyling);
        contents[6].ShouldBeSameAs(lineContents[6]);
        contents[7].ShouldMatch<WJudge>("Lord Charlie-Delta", BoldStyling);
        contents[8].ShouldBeSameAs(lineContents[8]);
        contents[9].ShouldMatch<WJudge>("Lady Echo", BoldStyling);
    }

    [Fact]
    public void Enrich_JudgeSplitAcrossRuns_MarksUpOneJudgeWithTheFirstRunsStyling()
    {
        IBlock[] blocks =
        [
            TextLine("Before"),
            new WLine(LineTemplate,
                [new WText("TRIBUNAL JUDGE ", BoldStyling), new WText("echo foxtrot", ItalicStyling)])
        ];

        var contents = Enricher.Enrich(blocks).ToArray()[1].ShouldBeOfType<WLine>().Contents.ToArray();

        contents.ShouldHaveSingleItem().ShouldMatch<WJudge>("TRIBUNAL JUDGE echo foxtrot", BoldStyling);
    }

    [Fact]
    public void Enrich_WhitespaceRunBeforeJudge_MarksUpOnlyTheName()
    {
        IInline[] lineContents = [new WText("     ", BoldStyling), new WText("Mr JUSTICE HOTEL", UnderlineStyling)];
        IBlock[] blocks =
        [
            TextLine("Before:"),
            new WLine(LineTemplate, lineContents),
            TextLine("- - -")
        ];

        var contents = Enricher.Enrich(blocks).ToArray()[1].ShouldBeOfType<WLine>().Contents.ToArray();

        contents.Length.ShouldBe(2);
        contents[0].ShouldBeSameAs(lineContents[0]);
        contents[1].ShouldMatch<WJudge>("Mr JUSTICE HOTEL", UnderlineStyling);
    }

    [Fact]
    public void Enrich_SpacesAroundJudge_AreNotPartOfTheJudge()
    {
        IBlock[] blocks =
        [
            TextLine("Before:"),
            StyledLine(" Richard Roe Q.C. ", BoldStyling),
            TextLine("- - -")
        ];

        var contents = Enricher.Enrich(blocks).ToArray()[1].ShouldBeOfType<WLine>().Contents.ToArray();

        contents.Length.ShouldBe(3);
        contents[0].ShouldMatch<WText>(" ", BoldStyling);
        contents[1].ShouldMatch<WJudge>("Richard Roe Q.C.", BoldStyling);
        contents[2].ShouldMatch<WText>(" ", BoldStyling);
    }
}
