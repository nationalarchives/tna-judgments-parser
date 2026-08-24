using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Common;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

using Models = UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.Common.Test;


/// <summary>
/// Unit tests for the dotted-number re-nesting pass.
/// </summary>
/// <remarks>
/// These are mandatory rather than incidental: the fixture corpus contains no
/// three-component numbering at all (12,596 &lt;num&gt; elements, zero matching
/// <c>^\d+\.\d+\.\d+\.?$</c>), so fixture regeneration cannot exercise the
/// leaf-to-branch conversion. See <c>src/leg/NUMBERING.md</c> §9.
/// Invariant references I1-I7 are to NUMBERING.md §4.
/// </remarks>
public class TestDottedNumberRenester
{

    /* helpers */

    private class TestBlock : IBlock
    {
        internal string Tag { get; init; }
    }

    private static IBlock Block(string tag) => new TestBlock { Tag = tag };

    /// <summary>A flat numbered leaf paragraph, as the parser emits today.</summary>
    private static IDivision Para(string number, string tag = null) =>
        new WNewNumberedParagraph(new WText(number, null),
            new List<IBlock> { Block(tag ?? number) });

    /// <summary>An unnumbered division, used to interrupt a run.</summary>
    private static IDivision Unnumbered(string tag) =>
        new WNewNumberedParagraph(new WText("•", null),
            new List<IBlock> { Block(tag) });

    /// <summary>Compact structural rendering, e.g. "paragraph[1.](subparagraph[1.1])".</summary>
    private static string Describe(IDivision div)
    {
        var number = div.Number?.Text ?? "-";
        var name = div.Name ?? "?";
        if (div is IBranch branch && branch.Children is not null && branch.Children.Any())
            return $"{name}[{number}]({string.Join(",", branch.Children.Select(Describe))})";
        return $"{name}[{number}]";
    }

    private static string Describe(IEnumerable<IDivision> divisions) =>
        string.Join(" ", divisions.Select(Describe));

    private static List<string> Tags(IEnumerable<IBlock> blocks) =>
        blocks.OfType<TestBlock>().Select(b => b.Tag).ToList();

    /* the target case */

    [Fact]
    public void NestsAFlatRun()
    {
        var input = new List<IDivision> {
        Para("1."), Para("1.1"), Para("1.2"), Para("2."), Para("2.1")
    };
        Assert.Equal(
            "paragraph[1.](subparagraph[1.1],subparagraph[1.2]) " +
            "paragraph[2.](subparagraph[2.1])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    /* three components — unexercised by any fixture */

    [Fact]
    public void NestsThreeComponents()
    {
        var input = new List<IDivision> {
        Para("1"), Para("1.1"), Para("1.1.1"), Para("1.1.2"), Para("1.2")
    };
        // 1.1.1 is a grandchild of 1, so I2 must resolve it against the whole
        // open ancestor stack rather than a single open parent.
        Assert.Equal(
            "paragraph[1](subparagraph[1.1](subparagraph[1.1.1],subparagraph[1.1.2])," +
            "subparagraph[1.2])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void LeafBecomingABranchKeepsItsContentAsIntro()
    {
        var input = new List<IDivision> {
        Para("1.", "root-content"),
        Para("1.1", "mid-content"),
        Para("1.1.1", "leaf-content")
    };
        var result = DottedNumberRenester.Renest(input);

        // The root leaf became a BranchParagraph, its Contents now Intro.
        var root = Assert.IsType<BranchParagraph>(Assert.Single(result));
        Assert.Equal(new List<string> { "root-content" }, Tags(root.Intro));

        // 1.1 was demoted AND converted leaf -> branch: this conversion has no
        // pre-existing implementation in the repo (NUMBERING.md §5).
        var mid = Assert.IsType<FlushBranchSubparagraph>(Assert.Single(root.Children));
        Assert.Equal(new List<string> { "mid-content" }, Tags(mid.Intro));

        var leaf = Assert.IsType<FlushLeafSubparagraph>(Assert.Single(mid.Children));
        Assert.Equal(new List<string> { "leaf-content" }, Tags(leaf.Contents));
    }

    /* I4 — numeric, not lexicographic, ordering */

    [Fact]
    public void OrdersComponentsNumericallyNotAsText()
    {
        // "3.10" sorts before "3.9" as a string; od/sdsiod_9780111061145_en_002
        // is the fixture that exercises this.
        var input = new List<IDivision> {
        Para("3."), Para("3.9"), Para("3.10")
    };
        Assert.Equal(
            "paragraph[3.](subparagraph[3.9],subparagraph[3.10])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    /* I3 — gaps permitted */

    [Fact]
    public void AllowsChildrenThatDoNotStartAtOne()
    {
        // ukdsiem_9780111100349_en and uksiem_20140198_en have 9. then 9.2.
        var input = new List<IDivision> { Para("9."), Para("9.2") };
        Assert.Equal(
            "paragraph[9.](subparagraph[9.2])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void AllowsHolesWithinAChildRun()
    {
        var input = new List<IDivision> {
        Para("10."), Para("10.2"), Para("10.3")
    };
        Assert.Equal(
            "paragraph[10.](subparagraph[10.2],subparagraph[10.3])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    /* I2 — interruption closes the run */

    [Fact]
    public void AnInterruptionClosesTheRun()
    {
        // uksiem_20151873_en: a bullet sits between 7. and 7.1.
        // Known consequence: those paragraphs stay flat (NUMBERING.md §4).
        var input = new List<IDivision> {
        Para("7."), Unnumbered("bullet"), Para("7.1")
    };
        Assert.Equal(
            "paragraph[7.] paragraph[•] paragraph[7.1]",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void AnUnrelatedNumberedRunClosesTheRun()
    {
        // uksicop_20160518_en: an unrelated 1..12 run sits between 2.2 and 2.3.
        var input = new List<IDivision> { Para("2."), Para("2.2") };
        for (var n = 1; n <= 12; n++)
            input.Add(Para(n.ToString()));
        input.Add(Para("2.3"));

        var result = DottedNumberRenester.Renest(input);
        // 2.2 nests under 2., then the bare 1 ends the run. 2.3 follows 12,
        // which is no parent of it, so 2.3 is left flat rather than reattached
        // across the interruption.
        Assert.StartsWith("paragraph[2.](subparagraph[2.2]) paragraph[1]", Describe(result));
        Assert.EndsWith("paragraph[12] paragraph[2.3]", Describe(result));
    }

    [Fact]
    public void AdjacentPrefixMatchIsTakenAtFaceValue()
    {
        // Known limitation, pinned deliberately rather than left to chance.
        // A bare "2" immediately followed by "2.3" is nested, even where the
        // "2" came from an unrelated list: nothing at this level distinguishes
        // a coincidental prefix match from a real parent, and I1 admits only
        // the adjacent candidate anyway. Harmless in the corpus — in
        // uksicop_20160518_en the interposed list reaches 12 before 2.3 —
        // but the behaviour is a consequence of locality, not an accident.
        var input = new List<IDivision> { Para("1"), Para("2"), Para("2.3") };
        Assert.Equal(
            "paragraph[1] paragraph[2](subparagraph[2.3])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    /* I1 — locality */

    [Fact]
    public void DoesNotSearchBackwardForADistantParent()
    {
        // ukdsiod_9780111169445_en: 1.1. scattered far from any 1.
        var input = new List<IDivision> {
        Para("1."), Para("2."), Para("3."), Para("1.1")
    };
        // 1.1 must not attach to the distant 1.
        Assert.Equal(
            "paragraph[1.] paragraph[2.] paragraph[3.] paragraph[1.1]",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void DoesNotAttachChildrenToATableOfContentsEntry()
    {
        // uksicop_20180470_en: the TOC's own 1., 3., 4... precedes the body's
        // 1., 1.1. Only the adjacent 1. may capture them.
        var input = new List<IDivision> {
        Para("1.", "toc"), Para("3.", "toc"), Para("4.", "toc"),
        Para("1.", "body"), Para("1.1", "body")
    };
        var result = DottedNumberRenester.Renest(input);
        Assert.Equal(
            "paragraph[1.] paragraph[3.] paragraph[4.] paragraph[1.](subparagraph[1.1])",
            Describe(result));
        // and specifically the *body* 1. is the one that gained the child
        var parent = Assert.IsType<BranchParagraph>(result[3]);
        Assert.Equal(new List<string> { "body" }, Tags(parent.Intro));
    }

    [Fact]
    public void ChildBeforeParentIsNotNested()
    {
        // ukia_20150171_en: 1.1 appears before 1. — rejected by I1/I2 because
        // no parent is open, not by any ordering rule.
        var input = new List<IDivision> {
        Para("1.1"), Para("1."), Para("2."), Para("1.3")
    };
        Assert.Equal(
            "paragraph[1.1] paragraph[1.] paragraph[2.] paragraph[1.3]",
            Describe(DottedNumberRenester.Renest(input)));
    }

    /* I4 — atomicity */

    [Fact]
    public void ARepeatedSiblingNumberLeavesTheWholeRunFlat()
    {
        var input = new List<IDivision> {
        Para("2."), Para("2.1"), Para("2.1")
    };
        // Not "nest the valid prefix and drop the rest" — the whole run stays flat.
        Assert.Equal(
            "paragraph[2.] paragraph[2.1] paragraph[2.1]",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void ARegressingSiblingNumberLeavesTheWholeRunFlat()
    {
        var input = new List<IDivision> {
        Para("2."), Para("2.2"), Para("2.1")
    };
        Assert.Equal(
            "paragraph[2.] paragraph[2.2] paragraph[2.1]",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void AnInvalidRunDoesNotPreventALaterValidOne()
    {
        var input = new List<IDivision> {
        Para("1."), Para("1.1"), Para("1.1"),
        Para("2."), Para("2.1")
    };
        Assert.Equal(
            "paragraph[1.] paragraph[1.1] paragraph[1.1] " +
            "paragraph[2.](subparagraph[2.1])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    /* I5 — existing structure preserved */

    [Fact]
    public void AlreadyNestedInputIsUnchanged()
    {
        var input = new List<IDivision> {
        new BranchParagraph {
            Number = new WText("1.", null),
            Intro = new List<IBlock> { Block("intro") },
            Children = new List<IDivision> {
                new LeafSubparagraph {
                    Number = new WText("1.1", null),
                    Contents = new List<IBlock> { Block("child") }
                }
            }
        }
    };
        Assert.Equal(
            "paragraph[1.](subparagraph[1.1])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    private static IDivision BranchWithChild(string number, string childNumber) =>
        new BranchParagraph
        {
            Number = new WText(number, null),
            Intro = new List<IBlock> { Block("intro") },
            Children = new List<IDivision> {
            new LeafSubparagraph { Number = new WText(childNumber, null),
                                   Contents = new List<IBlock> { Block("existing") } }
            }
        };

    [Fact]
    public void AFlatChildJoinsAHeadThatAlreadyHasIndentedChildren()
    {
        // ukdsiem_9780111540145_en: 10.1 was indented so the parser nested it,
        // but 10.2 and 10.3 were left flush. The flat ones must still join, and
        // the indented one must survive (I5).
        var input = new List<IDivision> {
        BranchWithChild("10.", "10.1"), Para("10.2"), Para("10.3")
    };
        var result = DottedNumberRenester.Renest(input);
        Assert.Equal(
            "paragraph[10.](subparagraph[10.1],subparagraph[10.2],subparagraph[10.3])",
            Describe(result));
        // The pre-existing child keeps its own content — and stays a plain
        // LeafSubparagraph: the pass did not move it, so it is not flush-marked.
        var parent = Assert.IsType<BranchParagraph>(Assert.Single(result));
        var first = Assert.IsType<LeafSubparagraph>(parent.Children.First());
        Assert.Equal(new List<string> { "existing" }, Tags(first.Contents));
    }

    [Fact]
    public void OrderingContinuesFromChildrenTheParseAlreadyNested()
    {
        // A flat 1.1 arriving after an indented 1.1 is a repeat, not a second
        // child: seeding the ordering check from existing children catches it,
        // and I4 then leaves the whole run flat.
        var input = new List<IDivision> { BranchWithChild("1.", "1.1"), Para("1.1") };
        Assert.Equal(
            "paragraph[1.](subparagraph[1.1]) paragraph[1.1]",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void RecursesIntoSectionChildren()
    {
        var section = new Models.Section
        {
            Number = new WText("1.", null),
            Children = new List<IDivision> { Para("1."), Para("1.1") }
        };
        var result = DottedNumberRenester.Renest(new List<IDivision> { section });
        Assert.Equal(
            "section[1.](paragraph[1.](subparagraph[1.1]))",
            Describe(result));
    }

    /* I7 — run-in headings */

    /// <summary>
    /// A Word run-in heading: one line, wholly emphasised, no number. This is
    /// what "What does the legislation do?" in uksiem_20240868_en_001 parses to.
    /// </summary>
    private static IDivision Heading(string text, bool bold = true, bool italic = false,
                                     bool underline = false)
    {
        RunProperties properties = new();
        if (bold)
            properties.AppendChild(new Bold());
        if (italic)
            properties.AppendChild(new Italic());
        if (underline)
            properties.AppendChild(new Underline { Val = UnderlineValues.Single });
        return new WDummyDivision(
            new WLine(LineTemplate, new List<IInline> { new WText(text, properties) }));
    }

    private static readonly WLine LineTemplate = new(null, new Paragraph());

    [Fact]
    public void ARunHeadingBeforeTheFirstChildJoinsTheIntro()
    {
        // uksiem_20240868_en_001: 4. "Overview of the Instrument", then the
        // heading "What does the legislation do?", then 4.1-4.8 flush left.
        var input = new List<IDivision> {
        Para("4.", "overview"), Heading("What does the legislation do?"),
        Para("4.1"), Para("4.2")
    };
        var result = DottedNumberRenester.Renest(input);
        Assert.Equal(
            "paragraph[4.](subparagraph[4.1],subparagraph[4.2])",
            Describe(result));
        // the heading followed the parent's own text into the intro, in order
        var parent = Assert.IsType<BranchParagraph>(Assert.Single(result));
        Assert.Equal(2, parent.Intro.Count());
        Assert.Equal("overview", Tags(parent.Intro).Single());
        Assert.Equal("What does the legislation do?",
            Assert.IsType<WLine>(parent.Intro.Last()).NormalizedContent);
    }

    [Fact]
    public void ARunHeadingBetweenChildrenBecomesANumberlessSubparagraph()
    {
        // Half the affected runs put a second heading mid-run (uksiem_20241017_en_002
        // shape HccHcc). Once children exist the schema offers no intro, so the
        // heading takes the only slot available between siblings.
        var input = new List<IDivision> {
        Para("4."), Heading("First"), Para("4.1"), Para("4.2"),
        Heading("Second"), Para("4.3")
    };
        var result = DottedNumberRenester.Renest(input);
        Assert.Equal(
            "paragraph[4.](subparagraph[4.1],subparagraph[4.2]," +
            "subparagraph[-],subparagraph[4.3])",
            Describe(result));
        // it sits immediately above the child it introduces, as in the source
        var parent = Assert.IsType<BranchParagraph>(Assert.Single(result));
        var heading = Assert.IsType<FlushLeafSubparagraph>(parent.Children.ElementAt(2));
        Assert.Null(heading.Number);
        Assert.Equal("Second", Assert.IsType<WLine>(heading.Contents.Single()).NormalizedContent);
    }

    /* provenance — which divisions carry the flush marker */

    [Fact]
    public void OnlyLevelsThePassCreatedAreMarkedFlushWithParent()
    {
        // The marker records that a division sat flush with its parent in the
        // source. Two things must NOT carry it: the run head, which keeps the
        // depth it already had, and a heading absorbed into the head's intro,
        // which already renders at the parent's indent. Measured on
        // uksiem_20240868_en_001, whose nine run-in headings are all
        // EMLevel1Subheading at 0.492in — the same as the parent's own text —
        // so marking the intro ones would push aligned headings out of line.
        var input = new List<IDivision> {
            Para("4.", "overview"), Heading("Intro heading"), Para("4.1"),
            Heading("Mid-run heading"), Para("4.2")
        };
        var result = DottedNumberRenester.Renest(input);

        var head = Assert.IsType<BranchParagraph>(Assert.Single(result));
        Assert.IsNotAssignableFrom<IFlushWithParent>(head);

        // The intro heading is content of the head, not a level of its own.
        Assert.Equal("Intro heading",
            Assert.IsType<WLine>(head.Intro.Last()).NormalizedContent);

        // Everything the pass gave a level to is marked — the demoted numbered
        // children and the numberless subparagraph the mid-run heading became.
        Assert.All(head.Children, c => Assert.IsAssignableFrom<IFlushWithParent>(c));
        Assert.Equal(
            new List<string> { "4.1", null, "4.2" },
            head.Children.Select(c => c.Number?.Text).ToList());
    }

    [Fact]
    public void AMarkedBranchKeepsItsMarkOnASecondPass()
    {
        // I6 for provenance, not just for shape. FlushBranchSubparagraph derives
        // from Parse.BranchSubparagraph, so RenestWithin's case for the base type
        // catches it as well; rebuilding it as the base would launder the mark
        // away on any later pass. Leaves hid this — RenestWithin returns them
        // untouched — so it only shows on a demoted division that has children.
        var input = new List<IDivision> {
            Para("1.", "root"), Para("1.1", "mid"), Para("1.1.1", "leaf")
        };
        var once = DottedNumberRenester.Renest(input);
        var twice = DottedNumberRenester.Renest(once);

        foreach (var result in new[] { once, twice })
        {
            var root = Assert.IsType<BranchParagraph>(Assert.Single(result));
            var mid = Assert.IsType<FlushBranchSubparagraph>(Assert.Single(root.Children));
            Assert.IsType<FlushLeafSubparagraph>(Assert.Single(mid.Children));
        }
        Assert.Equal(Describe(once), Describe(twice));
    }

    [Fact]
    public void APreExistingChildIsNotMarkedFlushWithParent()
    {
        // I5 keeps a child the parse already nested. The source indented it, so
        // its indent is real and the marker must not claim otherwise — even
        // though its flat siblings, joining alongside it, are marked.
        var input = new List<IDivision> {
            BranchWithChild("10.", "10.1"), Para("10.2")
        };
        var parent = Assert.IsType<BranchParagraph>(
            Assert.Single(DottedNumberRenester.Renest(input)));

        Assert.IsNotAssignableFrom<IFlushWithParent>(parent.Children.ElementAt(0));
        Assert.IsAssignableFrom<IFlushWithParent>(parent.Children.ElementAt(1));
    }

    [Fact]
    public void ATrailingRunHeadingIsLeftWhereItIs()
    {
        // uksiem_20240868_en_001 head 10. has shape HccH: the last heading
        // introduces the NEXT section, so absorbing it would steal it.
        var input = new List<IDivision> {
        Para("10."), Heading("Introduces the children"),
        Para("10.1"), Para("10.2"), Heading("Introduces what comes next")
    };
        var result = DottedNumberRenester.Renest(input);
        Assert.Equal(
            "paragraph[10.](subparagraph[10.1],subparagraph[10.2]) ?[-]",
            Describe(result));
    }

    [Fact]
    public void AnItalicRunHeadingCountsToo()
    {
        var input = new List<IDivision> {
        Para("4."), Heading("Purpose", bold: false, italic: true), Para("4.1")
    };
        Assert.Equal("paragraph[4.](subparagraph[4.1])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void AnUnemphasisedLineStillEndsTheRun()
    {
        // I2 is unchanged for ordinary content; only emphasised headings are
        // absorbed. This is the 38-paragraph bucket the pass still declines.
        var input = new List<IDivision> {
        Para("4."), Heading("ordinary text", bold: false), Para("4.1")
    };
        Assert.Equal("paragraph[4.] ?[-] paragraph[4.1]",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void AWhollyBoldSentenceIsEmphasisNotAHeading()
    {
        var sentence = new string('x', 91);
        var input = new List<IDivision> { Para("4."), Heading(sentence), Para("4.1") };
        Assert.Equal("paragraph[4.] ?[-] paragraph[4.1]",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void ARunHeadingAttachesToTheLevelThatOwnsTheChildBelowIt()
    {
        // The heading precedes 4.1.2, so it belongs to 4.1 — not to 4., and not
        // to 4.1.1, which is the deeper frame still open when it arrives.
        var input = new List<IDivision> {
        Para("4."), Para("4.1"), Para("4.1.1"), Heading("Deeper"), Para("4.1.2")
    };
        Assert.Equal(
            "paragraph[4.](subparagraph[4.1](subparagraph[4.1.1]," +
            "subparagraph[-],subparagraph[4.1.2]))",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void AnUnderlinedRunHeadingCountsToo()
    {
        var input = new List<IDivision> {
        Para("4."), Heading("Purpose", bold: false, underline: true), Para("4.1")
    };
        Assert.Equal("paragraph[4.](subparagraph[4.1])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void ConsecutiveRunHeadingsAreAllAbsorbed()
    {
        // ChildFollows looks past a whole block of headings, so a two-line
        // heading split across divisions is not half-absorbed.
        var input = new List<IDivision> {
        Para("4.", "own-text"), Heading("Part One"), Heading("What does it do?"),
        Para("4.1")
    };
        var result = DottedNumberRenester.Renest(input);
        Assert.Equal("paragraph[4.](subparagraph[4.1])", Describe(result));
        var parent = Assert.IsType<BranchParagraph>(Assert.Single(result));
        Assert.Equal(new List<string> { "own-text", "Part One", "What does it do?" },
            parent.Intro.Select(b => b is WLine line ? line.NormalizedContent
                                                     : Tags(new List<IBlock> { b }).Single())
                        .ToList());
    }

    [Theory]
    [InlineData(90, true)]    // at the cap
    [InlineData(91, false)]   // one over
    public void TheLengthCapIsInclusive(int length, bool absorbed)
    {
        var input = new List<IDivision> {
        Para("4."), Heading(new string('x', length)), Para("4.1")
    };
        Assert.Equal(
            absorbed ? "paragraph[4.](subparagraph[4.1])"
                     : "paragraph[4.] ?[-] paragraph[4.1]",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void AbsorptionSurvivesASecondPass()
    {
        // I6 for the I7 path: the output contains both destinations — a heading
        // folded into the intro and one standing as a numberless subparagraph —
        // and neither may be re-recognised as a run head or as a heading again.
        var input = new List<IDivision> {
        Para("4."), Heading("Leading"), Para("4.1"), Heading("Middle"), Para("4.2")
    };
        var once = DottedNumberRenester.Renest(input);
        var twice = DottedNumberRenester.Renest(once);
        Assert.Equal(Describe(once), Describe(twice));
        Assert.Equal(
            "paragraph[4.](subparagraph[4.1],subparagraph[-],subparagraph[4.2])",
            Describe(twice));

        // Describe reads numbers, not text, so structural equality alone would
        // not notice a second pass emptying either heading.
        var parent = Assert.IsType<BranchParagraph>(Assert.Single(twice));
        Assert.Equal("Leading",
            Assert.IsType<WLine>(parent.Intro.Last()).NormalizedContent);
        var middle = Assert.IsType<FlushLeafSubparagraph>(parent.Children.ElementAt(1));
        Assert.Equal("Middle",
            Assert.IsType<WLine>(middle.Contents.Single()).NormalizedContent);
    }

    /* existing children are not interruptions */

    [Fact]
    public void ExistingChildrenDoNotBlockAFlatContinuation()
    {
        // uksicop_20200860_en heads 4. and 6.: the parse already nested a
        // lettered "(a)", which is a child of the head, not an interruption in
        // the sibling run below it. The flat 4.1 still joins, after it.
        var input = new List<IDivision> {
        new BranchParagraph {
            Number = new WText("4.", null),
            Intro = new List<IBlock> { Block("intro") },
            Children = new List<IDivision> {
                new LeafSubparagraph { Number = new WText("(a)", null),
                                       Contents = new List<IBlock> { Block("lettered") } }
            }
        },
        Para("4.1"), Para("4.2")
    };
        Assert.Equal(
            "paragraph[4.](subparagraph[(a)],subparagraph[4.1],subparagraph[4.2])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void ABranchHeadKeepsItsIntroChildOrderAndWrapUp()
    {
        // Frame copies all three across, but only Intro and child order are
        // reachable from an enabled /leg type: the leg models hard-code
        // WrapUp => null (src/leg/models/Structure.cs) and IsWrapUp is false
        // outside the judgments parsers. This test is the only thing that would
        // catch Frame dropping or reordering a wrap-up if that ever changed.
        var head = new BranchParagraph
        {
            Number = new WText("10.", null),
            Intro = new List<IBlock> { Block("intro-a"), Block("intro-b") },
            Children = new List<IDivision> {
            new LeafSubparagraph { Number = new WText("10.1", null),
                                   Contents = new List<IBlock> { Block("first") } },
            new LeafSubparagraph { Number = new WText("10.2", null),
                                   Contents = new List<IBlock> { Block("second") } }
        },
            WrapUp = new List<IBlock> { Block("wrap") }
        };
        var result = DottedNumberRenester.Renest(
            new List<IDivision> { head, Para("10.3"), Para("10.4") });

        var parent = Assert.IsType<BranchParagraph>(Assert.Single(result));
        Assert.Equal(new List<string> { "intro-a", "intro-b" }, Tags(parent.Intro));
        Assert.Equal(new List<string> { "wrap" }, Tags(parent.WrapUp));
        Assert.Equal(
            "paragraph[10.](subparagraph[10.1],subparagraph[10.2]," +
            "subparagraph[10.3],subparagraph[10.4])",
            Describe(result));
        // Pre-existing children keep their own content, in their original order,
        // and are not flush-marked — the pass did not create their level.
        Assert.Equal(new List<string> { "first" },
            Tags(Assert.IsType<LeafSubparagraph>(parent.Children.ElementAt(0)).Contents));
        Assert.Equal(new List<string> { "second" },
            Tags(Assert.IsType<LeafSubparagraph>(parent.Children.ElementAt(1)).Contents));
    }

    /* conversion guards — assumptions made executable */

    [Fact]
    public void RefusesToReNestADivisionThatCarriesAHeading()
    {
        // Models.BranchParagraph is "paragraph"-named, is an IBranch and has a
        // settable Heading, so it can reach Frame.From as a run head — and no
        // type Close can emit is able to hold a heading. Reachable once IA is
        // enabled (NUMBERING.md §7), so refuse rather than discard.
        var head = new Models.BranchParagraph
        {
            Number = new WText("4.", null),
            Heading = new WLine(LineTemplate, new List<IInline> { new WText("Titled", null) }),
            Children = new List<IDivision>()
        };
        var ex = Assert.Throws<NotSupportedException>(() =>
            DottedNumberRenester.Renest(new List<IDivision> { head, Para("4.1") }));
        Assert.Contains("carries a heading", ex.Message);
    }

    [Fact]
    public void RefusesToDemoteABranchWithWrapUpButNoChildren()
    {
        // A leaf subparagraph has nowhere to put wrap-up content, so this shape
        // cannot round-trip. Unreachable from today's parsers; guarded so it
        // fails loudly rather than truncating.
        var child = new BranchParagraph
        {
            Number = new WText("4.1", null),
            Intro = new List<IBlock> { Block("intro") },
            Children = new List<IDivision>(),
            WrapUp = new List<IBlock> { Block("wrap") }
        };
        var ex = Assert.Throws<NotSupportedException>(() =>
            DottedNumberRenester.Renest(new List<IDivision> { Para("4."), child }));
        Assert.Contains("wrap-up", ex.Message);
    }

    /* I4 — seeding from existing children */

    [Fact]
    public void OrderingSeedsFromTheHighestExistingChildNotTheLast()
    {
        // The parse nested 10.3 before 10.1. Seeding from the last child in
        // document order would give 1, letting a flat 10.2 through; the maximum
        // is 3, so I4 rejects the run and leaves it flat — atomically.
        var head = new BranchParagraph
        {
            Number = new WText("10.", null),
            Intro = new List<IBlock> { Block("intro") },
            Children = new List<IDivision> {
            new LeafSubparagraph { Number = new WText("10.3", null),
                                   Contents = new List<IBlock> { Block("third") } },
            new LeafSubparagraph { Number = new WText("10.1", null),
                                   Contents = new List<IBlock> { Block("first") } }
        }
        };
        var result = DottedNumberRenester.Renest(new List<IDivision> { head, Para("10.2") });
        Assert.Equal(
            "paragraph[10.](subparagraph[10.3],subparagraph[10.1]) paragraph[10.2]",
            Describe(result));
    }

    [Fact]
    public void AFlatChildAboveEveryExistingChildStillJoins()
    {
        // The counterpart: 10.4 exceeds the maximum, so the run is valid.
        var head = new BranchParagraph
        {
            Number = new WText("10.", null),
            Intro = new List<IBlock> { Block("intro") },
            Children = new List<IDivision> {
            new LeafSubparagraph { Number = new WText("10.3", null),
                                   Contents = new List<IBlock> { Block("third") } },
            new LeafSubparagraph { Number = new WText("10.1", null),
                                   Contents = new List<IBlock> { Block("first") } }
        }
        };
        Assert.Equal(
            "paragraph[10.](subparagraph[10.3],subparagraph[10.1],subparagraph[10.4])",
            Describe(DottedNumberRenester.Renest(new List<IDivision> { head, Para("10.4") })));
    }

    /* purity — NUMBERING.md §3 calls this "flat list in, tree out" */

    [Fact]
    public void DoesNotMutateTheCallersContainers()
    {
        var section = new Models.Section
        {
            Number = new WText("1.", null),
            Children = new List<IDivision> { Para("1."), Para("1.1") }
        };
        var before = section.Children;

        var result = DottedNumberRenester.Renest(new List<IDivision> { section });

        // the caller's section still holds its original, untransformed child list
        Assert.Same(before, section.Children);
        Assert.Equal("paragraph[1.] paragraph[1.1]", Describe(section.Children));
        // while the returned tree is the re-nested one
        Assert.Equal("section[1.](paragraph[1.](subparagraph[1.1]))", Describe(result));
        Assert.NotSame(section, Assert.Single(result));
    }

    /* I5 — repair composes with structure the parse already built */

    [Fact]
    public void RepairsAFlatRunNestedInsideAParagraph()
    {
        // The intersection of I5 and three-component support: the parser
        // indented 4.1 under 4. but left 4.1.1 and 4.1.2 at the same depth, so
        // they arrived as siblings of 4.1 rather than its children. A run head
        // here is subparagraph-named and sits at depth 2, and its local root
        // must close as a BranchSubparagraph — not a BranchParagraph.
        var paragraph = new BranchParagraph
        {
            Number = new WText("4.", null),
            Intro = new List<IBlock> { Block("intro") },
            Children = new List<IDivision> {
            new LeafSubparagraph { Number = new WText("4.1", null),
                                   Contents = new List<IBlock> { Block("one") } },
            new LeafSubparagraph { Number = new WText("4.1.1", null),
                                   Contents = new List<IBlock> { Block("one-one") } },
            new LeafSubparagraph { Number = new WText("4.1.2", null),
                                   Contents = new List<IBlock> { Block("one-two") } }
        }
        };
        var result = DottedNumberRenester.Renest(new List<IDivision> { paragraph });

        Assert.Equal(
            "paragraph[4.](subparagraph[4.1](subparagraph[4.1.1],subparagraph[4.1.2]))",
            Describe(result));
        var outer = Assert.IsType<BranchParagraph>(Assert.Single(result));
        // 4.1 heads the run, so it keeps the depth it already had and is not
        // flush-marked; the levels built beneath it are.
        var inner = Assert.IsType<BranchSubparagraph>(Assert.Single(outer.Children));

        // Describe renders numbers, not text, so content assertions are the only
        // thing that would catch a grandchild being emptied on the way down.
        Assert.Equal(new List<string> { "intro" }, Tags(outer.Intro));
        Assert.Equal(new List<string> { "one" }, Tags(inner.Intro));
        Assert.Equal(new List<string> { "one-one" },
            Tags(Assert.IsType<FlushLeafSubparagraph>(inner.Children.ElementAt(0)).Contents));
        Assert.Equal(new List<string> { "one-two" },
            Tags(Assert.IsType<FlushLeafSubparagraph>(inner.Children.ElementAt(1)).Contents));

        // I6 for the nested scope specifically: a second pass must not re-read
        // the new BranchSubparagraph root as a fresh run head.
        var twice = DottedNumberRenester.Renest(result);
        Assert.Equal(Describe(result), Describe(twice));
        var again = Assert.IsType<BranchSubparagraph>(
            Assert.Single(Assert.IsType<BranchParagraph>(Assert.Single(twice)).Children));
        Assert.Equal(
            new List<string> { "one-one", "one-two" },
            again.Children.Select(c => Tags(Assert.IsType<FlushLeafSubparagraph>(c).Contents).Single())
                          .ToList());
    }

    [Fact]
    public void DoesNotTreatASubparagraphRunAsATopLevelParagraph()
    {
        // Guards the scope rule from the other side: 4.1's siblings must not be
        // mistaken for a top-level run, which would emit a paragraph inside a
        // paragraph — a shape em-subschema.xsd forbids.
        var paragraph = new BranchParagraph
        {
            Number = new WText("4.", null),
            Intro = new List<IBlock> { Block("intro") },
            Children = new List<IDivision> {
            new LeafSubparagraph { Number = new WText("4.1", null),
                                   Contents = new List<IBlock> { Block("one") } },
            new LeafSubparagraph { Number = new WText("4.1.1", null),
                                   Contents = new List<IBlock> { Block("one-one") } }
        }
        };
        var result = DottedNumberRenester.Renest(new List<IDivision> { paragraph });
        var outer = Assert.IsType<BranchParagraph>(Assert.Single(result));
        Assert.All(outer.Children, c => Assert.Equal("subparagraph", c.Name));
    }

    [Fact]
    public void TopLevelRunsAreUnaffectedByTheNestedScope()
    {
        var input = new List<IDivision> { Para("1."), Para("1.1"), Para("2."), Para("2.1") };
        Assert.Equal(
            "paragraph[1.](subparagraph[1.1]) paragraph[2.](subparagraph[2.1])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    /* exhaustiveness */

    private class UnknownBranch : IBranch
    {
        public string Name => "mystery";
        public IFormattedText Number => null;
        public ILine Heading => null;
        public IEnumerable<IBlock> Intro => null;
        public IEnumerable<IDivision> Children { get; init; }
        public IEnumerable<IBlock> WrapUp => null;
    }

    [Fact]
    public void RefusesToSkipAnUnrecognisedBranch()
    {
        // Silently returning it unrecursed would look like a clean pass over a
        // subtree the renester never entered.
        var input = new List<IDivision> {
        new UnknownBranch { Children = new List<IDivision> { Para("1."), Para("1.1") } }
    };
        var ex = Assert.Throws<NotSupportedException>(() => DottedNumberRenester.Renest(input));
        Assert.Contains("UnknownBranch", ex.Message);
    }

    [Fact]
    public void PassesOrdinaryLeavesThrough()
    {
        var input = new List<IDivision> { Para("1."), Unnumbered("bullet"), Para("3.") };
        Assert.Equal("paragraph[1.] paragraph[•] paragraph[3.]",
            Describe(DottedNumberRenester.Renest(input)));
    }

    /* the guard upstream of the pass */

    [Theory]
    [InlineData("2.2", true)]      // the case the guard was written for
    [InlineData("2.2.", true)]     // trailing dot
    [InlineData("1.1.1", true)]    // three components — the old regex missed this
    [InlineData("1.", false)]      // a top-level number is not a sub-number
    [InlineData("1", false)]
    [InlineData("(a)", false)]
    [InlineData("•", false)]
    [InlineData(null, false)]
    public void ADottedBodyNumberIsRecognisedAtAnySupportedDepth(string text, bool expected)
    {
        // EM's bullet guard (src/leg/em/Parser.cs) refuses to nest a dotted body
        // number inside a bullet. Its old regex, ^\d+\.\d+\.?$, matched two
        // components only, so "1.1.1" was absorbed under the bullet and then
        // preserved by I5 — the re-nester cannot undo a mis-nesting upstream of
        // it. This pins the predicate, not the end-to-end parse.
        Assert.Equal(expected,
            UK.Gov.Legislation.ExplanatoryMemoranda.Parser.IsDottedBodyNumber(text));
    }

    /* I6 — idempotency */

    [Fact]
    public void IsIdempotent()
    {
        var input = new List<IDivision> {
        Para("1."), Para("1.1"), Para("1.1.1"), Para("1.2"), Para("2."), Para("2.1")
    };
        var once = DottedNumberRenester.Renest(input);
        var twice = DottedNumberRenester.Renest(once);
        Assert.Equal(Describe(once), Describe(twice));
    }

    /* trailing-dot normalisation — all three forms §7 promises */

    [Theory]
    [InlineData("1.", "1.1")]    // dot on parent only (the EM fixtures)
    [InlineData("1", "1.1")]     // no dots at all (cop/uksicop_20200860_en)
    [InlineData("5.", "5.1.")]   // dots on both (od/nisrod_20160090_en)
    [InlineData("5", "5.1.")]    // dot on child only
    public void NormalisesTrailingDots(string parent, string child)
    {
        var input = new List<IDivision> { Para(parent), Para(child) };
        Assert.Equal(
            $"paragraph[{parent}](subparagraph[{child}])",
            Describe(DottedNumberRenester.Renest(input)));
    }

    /* nothing to do */

    [Fact]
    public void LeavesAnOrdinaryFlatListAlone()
    {
        var input = new List<IDivision> { Para("1."), Para("2."), Para("3.") };
        Assert.Equal(
            "paragraph[1.] paragraph[2.] paragraph[3.]",
            Describe(DottedNumberRenester.Renest(input)));
    }

    [Fact]
    public void HandlesAnEmptyList()
    {
        Assert.Empty(DottedNumberRenester.Renest(new List<IDivision>()));
    }

}

/// <summary>Unit tests for the number value type itself.</summary>
public class TestDottedNumber
{

    [Theory]
    [InlineData("1", 1)]
    [InlineData("1.", 1)]
    [InlineData("1.1", 2)]
    [InlineData("1.1.", 2)]
    [InlineData("3.10", 2)]
    [InlineData("1.1.1", 3)]
    [InlineData("1.1.1.", 3)]
    public void ParsesSupportedForms(string text, int expectedDepth)
    {
        Assert.True(DottedNumber.TryParse(text, out DottedNumber number));
        Assert.Equal(expectedDepth, number.Depth);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("1a")]
    [InlineData("(1)")]
    [InlineData("•")]
    [InlineData("1..2")]
    [InlineData(".1")]
    [InlineData("1.1.1.1")]   // beyond what HardNumbers extracts
    public void RejectsUnsupportedForms(string text)
    {
        Assert.False(DottedNumber.TryParse(text, out _));
    }

    [Theory]
    [InlineData("1.1", "1.", true)]
    [InlineData("1.1", "1", true)]
    [InlineData("1.1.1", "1.1", true)]
    [InlineData("1.1.1", "1.", false)]   // grandchild, not child
    [InlineData("2.1", "1.", false)]
    [InlineData("1.", "1.1", false)]
    [InlineData("1.", "1.", false)]
    public void RecognisesDirectChildren(string child, string parent, bool expected)
    {
        Assert.True(DottedNumber.TryParse(child, out DottedNumber c));
        Assert.True(DottedNumber.TryParse(parent, out DottedNumber p));
        Assert.Equal(expected, c.IsChildOf(p));
    }

    [Fact]
    public void ComparesFinalComponentAsAnInteger()
    {
        Assert.True(DottedNumber.TryParse("3.9", out DottedNumber nine));
        Assert.True(DottedNumber.TryParse("3.10", out DottedNumber ten));
        Assert.True(ten.Last > nine.Last);   // 10 > 9, though "3.10" < "3.9" as text
    }

    [Fact]
    public void TreatsTrailingDotAsInsignificant()
    {
        Assert.True(DottedNumber.TryParse("5.1", out DottedNumber withoutDot));
        Assert.True(DottedNumber.TryParse("5.1.", out DottedNumber withDot));
        Assert.Equal(withoutDot, withDot);
    }

}
