using Parse = UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Common;


/// <summary>
/// A division that sat flush with its parent in the source document, and is
/// nested only because <see cref="DottedNumberRenester"/> rebuilt the hierarchy
/// of the dotted-number run around it.
/// </summary>
/// <remarks>
/// <para>
/// Serialised as <c>class="flush-with-parent"</c>. This records
/// <b>provenance, not presentation</b>: the fact that the source put this
/// division at its parent's text column. What a renderer does with that is the
/// renderer's decision. A name like "no-indent" was rejected for encoding a CSS
/// instruction this parser is in no position to give — legislation.gov.uk
/// renders the published AKN with a stylesheet this repository does not own,
/// and <c>src/leg/akn2html.xsl</c> is only a local proxy for it.
/// </para>
/// <para>
/// "With its parent" is the load-bearing half of the name, and is meant
/// literally rather than as "flush with the page". The rule is <b>the parent's
/// text column</b>, whatever that happens to be: all 29 run-in headings across
/// the five affected fixtures were measured against their Word sources and
/// resolve to just two shapes — 27 at <c>left 0.492in</c>
/// (<c>EMLevel1Subheading</c>, matching the EMs' <c>EMSectionHeading</c> parent)
/// and 2 at zero (<c>Normal</c>, in a CoP whose document has no indentation
/// anywhere). In both the heading sits exactly at its parent's text column.
/// </para>
/// <para>
/// Only divisions the pass created a new level for carry this — which is not
/// the same as "divisions with a dotted number": a run-in heading I7 wraps as a
/// numberless subparagraph has no number, and is marked because the run was
/// rebuilt around it. The
/// head of a run keeps its original depth and is therefore never marked, nor is
/// a child the parse had already nested (I5) — the source really did indent
/// that one. Both of those are refusals: marking them would assert something
/// false.
/// </para>
/// <para>
/// A heading absorbed into a parent's <c>intro</c> is a different case: it is
/// not a division at all, so this marker cannot reach it. For 15 of the 17 such
/// lines it does not need to — the parent positions them at the text column the
/// source used. The other two are one source line, in the draft and made
/// versions of the same Code of Practice, where the parent's own rendered
/// column is 0.5in off the source; absorption moves the line to join it. Since
/// that line is the second half of a heading whose first half was already
/// displaced by the same amount, whether the result is better or worse than the
/// split it replaces was examined and left undecided — NUMBERING.md §7 and §9
/// under criterion 6a.
/// Block-level provenance via <c>DecorateBlockElement</c> would be possible and
/// is declined rather than impossible; see NUMBERING.md §7 for why, and for
/// what is lost by declining.
/// </para>
/// <para>
/// One consequence worth knowing before removing any of this: the marker is
/// also what keeps I7's two destinations rendering consistently. A wrapped
/// numberless subparagraph is a nested division and compounds; an absorbed
/// <c>intro</c> paragraph does not. Without the marker the same heading renders
/// 0.5in apart depending on which destination it landed in. See NUMBERING.md §7.
/// </para>
/// <para>
/// The provenance exists only inside the pass — by the time the XML is built,
/// nothing can tell an inferred level from one the source indented — which is
/// why it is carried on the model rather than recovered later.
/// </para>
/// </remarks>
internal interface IFlushWithParent
{
}


/// <summary>A <see cref="Parse.BranchSubparagraph"/> that was flush with its parent.</summary>
internal class FlushBranchSubparagraph : Parse.BranchSubparagraph, IFlushWithParent
{
}


/// <summary>A <see cref="Parse.LeafSubparagraph"/> that was flush with its parent.</summary>
internal class FlushLeafSubparagraph : Parse.LeafSubparagraph, IFlushWithParent
{
}
