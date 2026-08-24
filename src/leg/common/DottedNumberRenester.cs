using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;

using Models = UK.Gov.Legislation.Models;
using Parse = UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Common;


/// <summary>
/// Rebuilds hierarchy from dotted decimal numbering — turning a flat run of
/// <c>1., 1.1, 1.2, 2., 2.1</c> into nested divisions — for documents whose
/// numbering carries the structure that indentation normally would.
/// </summary>
/// <remarks>
/// <para>
/// Runs after parsing, over the <c>IDivision</c> model tree, so the
/// indentation rule in <c>ParseParagraphAndSubparagraphs</c> is untouched and
/// the judgments parser is unaffected. The full rationale, the fixture survey
/// behind it and the invariants below are in <c>src/leg/NUMBERING.md</c>.
/// </para>
/// <para>
/// The safety contract (NUMBERING.md §4), which is the substance of this
/// class rather than a detail of it:
/// </para>
/// <list type="bullet">
///   <item><b>I1 Locality</b> — only a contiguous run of adjacent siblings is
///   restructured; never search backwards for a matching prefix.</item>
///   <item><b>I2 Interruption closes the run</b> — validity is assessed
///   against the whole open ancestor stack, so <c>1.1.1</c> is admissible
///   under <c>1.1</c>; anything admissible under no open ancestor ends the
///   run.</item>
///   <item><b>I3 Gaps permitted</b> — a child run need not start at
///   <c>.1</c> nor be free of holes.</item>
///   <item><b>I4 Monotonic within a sibling group</b>, compared numerically,
///   and failure is <b>atomic</b>: an invalid run is left wholly flat.</item>
///   <item><b>I5 Existing structure preserved</b> — divisions that already
///   have children are recursed into, never flattened or reattached.</item>
///   <item><b>I6 Idempotent</b>.</item>
///   <item><b>I7 Run-in headings are absorbed, not obeyed</b> — an unnumbered,
///   wholly-emphasised one-line division between a parent and a child it
///   introduces does not end the run; it moves inside the parent. A heading
///   with no child after it is left alone, because it belongs to whatever
///   follows.</item>
/// </list>
/// </remarks>
internal static class DottedNumberRenester
{

    /// <summary>
    /// The naming and depth rules that apply to one sibling list. A run is
    /// recognised relative to where it sits: at the top of the body a head is
    /// <c>paragraph</c>-named at depth 1, while inside a numbered paragraph the
    /// same shape appears one component deeper and <c>subparagraph</c>-named.
    /// Without this, I5's promise that indentation-derived structure and
    /// numbering-derived repair compose would hold only at the top level.
    /// </summary>
    private readonly struct Scope
    {

        /// <summary>Element name a head or child must carry; null means no run can start here.</summary>
        internal string Name { get; init; }

        /// <summary>Number depth a run head must have; children are deeper.</summary>
        internal int HeadDepth { get; init; }

        /// <summary>Whether a local run root closes as a paragraph or a subparagraph.</summary>
        internal bool RootIsParagraph { get; init; }

        internal static Scope Top =>
            new() { Name = "paragraph", HeadDepth = 1, RootIsParagraph = true };

        /// <summary>
        /// The scope for <paramref name="parent"/>'s own child list. Returns the
        /// inert default when the parent is unnumbered or too deep for a run to
        /// fit: a head sits at the parent's depth plus one and needs a child
        /// deeper still, so with <c>MaxComponents</c> = 3 only a depth-1 parent
        /// can host one.
        /// </summary>
        internal static Scope Inside(IDivision parent)
        {
            if (!TryGetNumber(parent, out DottedNumber number))
                return default;
            if (number.Depth + 2 > DottedNumber.MaxComponents)
                return default;
            return new Scope
            {
                Name = "subparagraph",
                HeadDepth = number.Depth + 1,
                RootIsParagraph = false
            };
        }

        internal bool Names(IDivision div) => Name is not null && div.Name == Name;
    }

    internal static List<IDivision> Renest(IEnumerable<IDivision> divisions) =>
        Renest(divisions, Scope.Top);

    private static List<IDivision> Renest(IEnumerable<IDivision> divisions, Scope scope)
    {
        if (divisions is null)
            return new List<IDivision>();

        // I5: every nested sibling list gets the same treatment before this
        // level is considered, so hierarchy already present is preserved and
        // deeper flat runs are repaired in their own scope.
        var items = divisions.Select(RenestWithin).ToList();

        List<IDivision> result = new(items.Count);
        var i = 0;
        while (i < items.Count)
        {
            var length = MeasureRun(items, i, scope);
            if (length != 0)
            {   // MeasureRun returns 0 or a length > 1
                result.Add(BuildRun(items, i, length, scope));
                i += length;
            }
            else
            {
                result.Add(items[i]);
                i += 1;
            }
        }
        return result;
    }

    /// <summary>
    /// Recurses into a division's own children without altering it structurally.
    /// Always rebuilds, never assigns through to the argument: NUMBERING.md §3
    /// justifies this design as "flat list in, tree out — a pure function", and
    /// three of these types expose a settable <c>Children</c> that would make it
    /// otherwise. Unmodified leaves are shared, which is safe; caller-owned
    /// containers are not mutated.
    /// </summary>
    private static IDivision RenestWithin(IDivision div)
    {
        switch (div)
        {
            // Structural containers: their children are a body-like sibling list.
            case Models.Section section:
                return new Models.Section
                {
                    Number = section.Number,
                    Heading = section.Heading,
                    Children = Renest(section.Children, Scope.Top)
                };
            case Models.Subheading subheading:
                return new Models.Subheading
                {
                    Heading = subheading.Heading,
                    Children = Renest(subheading.Children, Scope.Top)
                };
            case Parse.CrossHeading crossHeading:
                return new Parse.CrossHeading
                {
                    Heading = crossHeading.Heading,
                    Children = Renest(crossHeading.Children, Scope.Top)
                };
            // Numbered branches: their children sit one component deeper.
            case Parse.BranchParagraph branch:
                return new Parse.BranchParagraph
                {
                    Number = branch.Number,
                    Intro = branch.Intro,
                    Children = Renest(branch.Children, Scope.Inside(branch)),
                    WrapUp = branch.WrapUp
                };
            case Parse.BranchSubparagraph branch:
                return new Parse.BranchSubparagraph
                {
                    Number = branch.Number,
                    Intro = branch.Intro,
                    Children = Renest(branch.Children, Scope.Inside(branch)),
                    WrapUp = branch.WrapUp
                };
            case Models.BranchParagraph branch:
                return new Models.BranchParagraph
                {
                    Number = branch.Number,
                    Heading = branch.Heading,
                    Children = Renest(branch.Children, Scope.Inside(branch))
                };
            // Leaves carry no child list, so passing them through is complete.
            case ILeaf:
                return div;
            // Any other branch would be silently skipped, which reads as a clean
            // pass over a subtree the pass never entered. Refuse instead.
            case IBranch:
                throw new System.NotSupportedException(
                    $"DottedNumberRenester cannot recurse into '{div.GetType().Name}', " +
                    "which would otherwise be left unre-nested without warning. Add a case for it.");
            default:
                return div;
        }
    }

    /* measuring — validate the whole run before transforming any of it (I4) */

    /// <summary>
    /// Length of the valid run starting at <paramref name="start"/>, or 0 if
    /// there is none. Returning 0 for an invalid run is what makes failure
    /// atomic: the caller emits the head unchanged and moves on one item, so
    /// the rest of the run is reached as ordinary flat divisions.
    /// </summary>
    private static int MeasureRun(List<IDivision> items, int start, Scope scope)
    {
        if (!TryGetHeadNumber(items[start], scope, out DottedNumber headNumber))
            return 0;

        List<DottedNumber> open = new() { headNumber };
        // lastChild[d] is the final component of the most recent child under
        // open[d], seeded from any the parse already nested. Truncated
        // alongside `open`, so each sibling group is ordered independently (I4).
        List<int?> lastChild = new() { MaxChildComponent(items[start], headNumber) };

        var i = start + 1;
        for (; i < items.Count; i++)
        {
            if (IsAbsorbableRunHeading(items[i]))
            {
                if (ChildFollows(items, i, open, scope))
                    continue;  // I7: absorbed by BuildRun, does not end the run
                break;         // heads whatever comes after the run
            }
            if (!TryGetChildNumber(items[i], scope, out DottedNumber number))
                break;  // I2: not a candidate at all

            var owner = FindOwner(open, number);
            if (owner < 0)
                break;  // I2: admissible under no open ancestor

            open.RemoveRange(owner + 1, open.Count - owner - 1);
            lastChild.RemoveRange(owner + 1, lastChild.Count - owner - 1);

            // I4, and deliberately strict: a repeated or regressing sibling
            // number invalidates the entire run rather than half-nesting it.
            if (lastChild[owner].HasValue && number.Last <= lastChild[owner].Value)
                return 0;

            lastChild[owner] = number.Last;
            open.Add(number);
            lastChild.Add(MaxChildComponent(items[i], number));
        }

        var length = i - start;
        return length > 1 ? length : 0;
    }

    /// <summary>
    /// Whether an admissible child follows the run-in heading at
    /// <paramref name="i"/>, looking past any further headings. This is what
    /// separates a heading that introduces the children below it from one that
    /// introduces the next section: only the former is absorbed (I7).
    /// </summary>
    private static bool ChildFollows(List<IDivision> items, int i, List<DottedNumber> open, Scope scope)
    {
        var j = i + 1;
        while (j < items.Count && IsAbsorbableRunHeading(items[j]))
            j++;
        return j < items.Count
            && TryGetChildNumber(items[j], scope, out DottedNumber ahead)
            && FindOwner(open, ahead) >= 0;
    }

    /// <summary>Deepest open ancestor that <paramref name="number"/> is a direct child of, or -1.</summary>
    private static int FindOwner(List<DottedNumber> open, DottedNumber number)
    {
        for (var d = open.Count - 1; d >= 0; d--)
            if (number.IsChildOf(open[d]))
                return d;
        return -1;
    }

    /// <summary>
    /// A run starts on any numbered top-level paragraph, whether or not it
    /// already has children. Branch heads matter: ukdsiem_9780111540145_en
    /// numbers 10.1 with an indent (so the parser already nested it) but
    /// leaves 10.2 and 10.3 flush, and refusing branch heads would strand
    /// them. Existing children are preserved by <see cref="Frame.From"/> (I5)
    /// and seed the ordering check via <see cref="MaxChildComponent"/>.
    /// </summary>
    private static bool TryGetHeadNumber(IDivision div, Scope scope, out DottedNumber number)
    {
        number = default;
        if (div is not ILeaf && div is not IBranch)
            return false;
        if (!scope.Names(div))
            return false;
        if (!TryGetNumber(div, out number))
            return false;
        return number.Depth == scope.HeadDepth;
    }

    /// <summary>
    /// Highest final component among the children <paramref name="div"/> already
    /// has under <paramref name="parent"/>, or null. Ordering must continue from
    /// children the parse already nested, so that a flat "10.1" arriving after
    /// an indented "10.1" is rejected as a repeat rather than duplicated.
    /// </summary>
    /// <remarks>
    /// The <b>maximum</b>, not the last in document order: anything appended
    /// lands after every existing child, so the maximum is the correct lower
    /// bound. Seeding from the last one lets a duplicate or a regression through
    /// whenever the parse nested the existing children out of numeric order —
    /// children 10.3 then 10.1 would seed 1, and a flat 10.2 would be accepted,
    /// producing exactly the half-repaired tree I4's atomicity rule exists to
    /// prevent.
    /// </remarks>
    private static int? MaxChildComponent(IDivision div, DottedNumber parent)
    {
        if (div is not IBranch branch || branch.Children is null)
            return null;
        int? max = null;
        foreach (IDivision child in branch.Children)
            if (TryGetNumber(child, out DottedNumber number) && number.IsChildOf(parent))
                max = max.HasValue ? System.Math.Max(max.Value, number.Last) : number.Last;
        return max;
    }

    private static bool TryGetChildNumber(IDivision div, Scope scope, out DottedNumber number)
    {
        number = default;
        if (div is not ILeaf && div is not IBranch)
            return false;
        if (!scope.Names(div))
            return false;
        if (!TryGetNumber(div, out number))
            return false;
        return number.Depth > scope.HeadDepth;
    }

    /// <summary>
    /// Longest line still treated as a run-in heading. A wholly-bold sentence
    /// of body text is emphasis, not a heading, and must not be absorbed.
    /// </summary>
    private const int MaxRunHeadingLength = 90;

    private static bool IsAbsorbableRunHeading(IDivision div) => IsAbsorbableRunHeading(div, out _);

    /// <summary>
    /// An unnumbered, unnamed, single-line <b>leaf</b> division whose text is
    /// wholly bold, italic or underlined — what a Word run-in heading such as
    /// "What does the legislation do?" parses to. A null <c>Name</c> is what
    /// <c>Builder</c> renders as <c>&lt;level&gt;</c>; the emphasis test reuses
    /// the <c>IsAllBold</c>/<c>IsAllItalicized</c>/<c>IsAllUnderlined</c>
    /// vocabulary the parser already uses to reject heading-like lines as
    /// paragraph continuations (<c>OptimzedParser.cs:650</c>).
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> called <c>IsCrossHeading</c>. That name is already
    /// taken twice in this subsystem, for a different, section-level concept:
    /// <c>BaseLegislativeDocumentParser.IsCrossHeading</c> recognises a flush-left
    /// bold line that <c>ParseCrossHeading</c> turns into a <c>Parse.CrossHeading</c>
    /// holding <c>Section</c> children. This predicate <i>rejects</i> that type —
    /// it also has a null <c>Name</c> and <c>Number</c>, but it is an
    /// <c>IBranch</c> carrying a real <c>Heading</c>, so the <c>ILeaf</c> and
    /// <c>Heading</c> gates below exclude it.
    ///
    /// What reaches this predicate is the residue: a bold line that headed
    /// numbered paragraphs rather than sections, so <c>ParseCrossHeading</c>
    /// found no <c>Section</c> children and it fell through to a
    /// <c>WDummyDivision</c>. An emphasised leaf, not a modelled heading.
    /// </remarks>
    private static bool IsAbsorbableRunHeading(IDivision div, out List<IBlock> blocks)
    {
        blocks = null;
        if (div.Name is not null || div.Number is not null || div.Heading is not null)
            return false;
        if (div is not ILeaf leaf || leaf.Contents is null)
            return false;
        var contents = leaf.Contents.ToList();
        if (contents.Count != 1 || contents[0] is not Parse.WLine line)
            return false;
        var text = line.NormalizedContent;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxRunHeadingLength)
            return false;
        if (!line.IsAllBold() && !line.IsAllItalicized() && !line.IsAllUnderlined())
            return false;
        blocks = contents;
        return true;
    }

    private static bool TryGetNumber(IDivision div, out DottedNumber number)
    {
        number = default;
        var text = div.Number?.Text;
        return text is not null && DottedNumber.TryParse(text, out number);
    }

    /* building */

    private static IDivision BuildRun(List<IDivision> items, int start, int length, Scope scope)
    {
        if (!TryGetHeadNumber(items[start], scope, out DottedNumber headNumber))
            throw new System.InvalidOperationException(
                $"BuildRun was given a non-head at index {start} that MeasureRun accepted; " +
                "MeasureRun and BuildRun must classify items identically.");
        List<Frame> frames = new() { Frame.From(items[start], headNumber) };
        List<IBlock> pending = new();

        for (var i = start + 1; i < start + length; i++)
        {
            if (IsAbsorbableRunHeading(items[i], out List<IBlock> heading))
            {
                pending.AddRange(heading);
                continue;
            }
            // MeasureRun already accepted every item in this range, so a
            // disagreement here is a programming error in one of the two
            // traversals, not a document we can fall back on. Fail loudly rather
            // than let owner = -1 drain the frame stack inside CloseDownTo.
            if (!TryGetChildNumber(items[i], scope, out DottedNumber number))
                throw new System.InvalidOperationException(
                    $"BuildRun reached a non-child at index {i} that MeasureRun accepted; " +
                    "MeasureRun and BuildRun must classify items identically.");
            var owner = FindOwner(frames.Select(f => f.Number).ToList(), number);
            if (owner < 0)
                throw new System.InvalidOperationException(
                    $"BuildRun found no open ancestor for '{number}' at index {i}, " +
                    "which MeasureRun accepted; the two traversals have diverged.");
            CloseDownTo(frames, owner);
            // Flush after closing, so the heading lands on the level that owns
            // the child it introduces rather than on a deeper open frame.
            frames[owner].Absorb(pending);
            pending.Clear();
            frames.Add(Frame.From(items[i], number));
        }
        // MeasureRun only consumes a heading when a child follows, so a run
        // cannot end on one. Guessing where a leftover heading belongs would be
        // inventing structure; treat it as the contract violation it is.
        if (pending.Count > 0)
            throw new System.InvalidOperationException(
                $"BuildRun finished the run at index {start} holding an unplaced heading; " +
                "MeasureRun must not end a run on a heading.");
        CloseDownTo(frames, 0);

        return frames[0].Close(root: true, rootIsParagraph: scope.RootIsParagraph);
    }

    /// <summary>Closes frames deeper than <paramref name="keep"/>, attaching each to its parent.</summary>
    private static void CloseDownTo(List<Frame> frames, int keep)
    {
        while (frames.Count > keep + 1)
        {
            Frame deepest = frames[^1];
            frames.RemoveAt(frames.Count - 1);
            frames[^1].Children.Add(deepest.Close(root: false));
        }
    }

    /// <summary>
    /// An open level during construction. Holds the source division's content
    /// so that a leaf which acquires children is converted to a branch with
    /// that content as <c>Intro</c> — the leaf-to-branch conversion that has
    /// no existing implementation in the repo (NUMBERING.md §5).
    /// </summary>
    private class Frame
    {

        internal DottedNumber Number { get; private init; }

        private IFormattedText NumberText { get; init; }

        private List<IBlock> Content { get; } = new();

        private List<IBlock> WrapUp { get; } = new();

        internal List<IDivision> Children { get; } = new();

        /// <summary>
        /// Places an absorbed run-in heading (I7). The schema decides the shape:
        /// <c>paragraph</c> and <c>subparagraph</c> allow either
        /// <c>content</c> or <c>intro? subparagraph+ wrapUp?</c>
        /// (<c>em-subschema.xsd:143-172</c>), so a heading that arrives before
        /// any child can join the intro, while one arriving between children has
        /// only a numberless <c>subparagraph</c> available to it. Both keep the
        /// heading immediately above the child it introduces, which is where the
        /// Word original puts it. Neither is the right model — see
        /// <c>NUMBERING.md</c> §7.
        /// </summary>
        internal void Absorb(List<IBlock> heading)
        {
            if (heading.Count == 0)
                return;
            if (Children.Count == 0)
                Content.AddRange(heading);
            else
                Children.Add(new Parse.LeafSubparagraph { Contents = new List<IBlock>(heading) });
        }

        /// <summary>
        /// Copies a source division's content across. <c>WrapUp</c> is carried
        /// for completeness of the model conversion; no enabled <c>/leg</c> type
        /// can currently produce one (<c>src/leg/models/Structure.cs</c> hard-codes
        /// <c>WrapUp =&gt; null</c>, and <c>IsWrapUp</c> is false outside the
        /// judgments parsers), which matters because <see cref="Close"/> emits it
        /// after the children — so were one ever to appear on a head that then
        /// acquires flat children, those children would precede it.
        /// </summary>
        internal static Frame From(IDivision div, DottedNumber number)
        {
            // Every type Close can emit hard-codes Heading => null
            // (src/model2/Subparagraphs.cs), so a source heading has nowhere to
            // go. Models.BranchParagraph is "paragraph"-named, is an IBranch and
            // has a settable Heading (src/leg/models/Structure.cs:59), so this is
            // reachable rather than theoretical — IA's SemanticEnricher builds
            // those, and NUMBERING.md §7 schedules IA for enabling. Carrying the
            // heading needs the deferred titled-group model, so refuse rather
            // than discard.
            if (div.Heading is not null)
                throw new System.NotSupportedException(
                    $"Cannot re-nest division '{number}': it carries a heading, and no " +
                    "paragraph or subparagraph type can hold one. See NUMBERING.md §7.");
            Frame frame = new() { Number = number, NumberText = div.Number };
            if (div is IBranch branch)
            {
                if (branch.Intro is not null)
                    frame.Content.AddRange(branch.Intro);
                if (branch.Children is not null)
                    frame.Children.AddRange(branch.Children);  // I5
                if (branch.WrapUp is not null)
                    frame.WrapUp.AddRange(branch.WrapUp);
            }
            else if (div is ILeaf leaf)
            {
                if (leaf.Contents is not null)
                    frame.Content.AddRange(leaf.Contents);
            }
            return frame;
        }

        internal IDivision Close(bool root, bool rootIsParagraph = false)
        {
            if (root)
                // A run is only built when length > 1, so the head always has
                // children by the time it closes. Whether it closes as a
                // paragraph or a subparagraph depends on the scope it was found
                // in: a run inside a paragraph's child list has a subparagraph
                // for its local root.
                return rootIsParagraph
                    ? new Parse.BranchParagraph
                    {
                        Number = NumberText,
                        Intro = Content,
                        Children = Children,
                        WrapUp = WrapUp
                    }
                    : new Parse.BranchSubparagraph
                    {
                        Number = NumberText,
                        Intro = Content,
                        Children = Children,
                        WrapUp = WrapUp
                    };
            if (Children.Count == 0)
            {
                // A leaf subparagraph has no wrapUp in the schema or the model,
                // so a childless frame that carried one cannot be represented.
                // Unreachable today (OptimzedParser.cs:633 only builds a wrap-up
                // branch alongside subparagraphs) and guarded rather than
                // silently truncated.
                if (WrapUp.Count > 0)
                    throw new System.NotSupportedException(
                        $"Cannot re-nest division '{Number}': it has wrap-up content but no " +
                        "children, and a leaf subparagraph cannot carry wrap-up.");
                return new Parse.LeafSubparagraph { Number = NumberText, Contents = Content };
            }
            return new Parse.BranchSubparagraph
            {
                Number = NumberText,
                Intro = Content,
                Children = Children,
                WrapUp = WrapUp
            };
        }

    }

}
