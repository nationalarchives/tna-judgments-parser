# Dotted-Number Hierarchy in `/leg`

**Status:** Implemented, not accepted — 2026-08-20. The structural pass is in
and the suite is green — 900 passed, 0 failed, 331 skipped, last verified
2026-08-21. Acceptance criterion 6 is unmet and cannot yet be attempted: it asks
for *visual* parity with the
Word original, and the HTML comparisons do not even execute here — they skip
unless Oxygen/Saxon is reachable, and `HtmlBuilder` hardcodes a Windows
`java.exe` path. The rendering follow-up in §7, a run in an environment where
those comparisons execute, and a human check of rendered output are all required
before rollout. The stale `.html` goldens are deliberate: see criterion 6.

**Scope:** the `/src/leg` associated-documents parser only. No change to the judgments parser or to `/src/lawmaker`.

This document records a design decision, the evidence behind it, and the
safety contract the implementation satisfies. §1-§9 were written before any
code; §10 was added afterwards, and §7 and §9 carry two corrections the
fixtures forced.

## 1. The problem

Documents in the `/leg` corpus routinely number their content like this, with
every line flush left and no Word list numbering:

```
1.    Introduction
1.1   Public appointees play an important role...
1.2   This Code sets out the principles...
2.    The Principles of Public Appointments
2.1   The Principles apply to all those involved...
```

The parser emits these as five siblings. We want `1.1` and `1.2` nested inside
`1.`, and `2.1` inside `2.`.

### Why it happens

**Paragraph and subparagraph nesting** in `/leg` is decided entirely by
indentation, inherited from the judgments parser. (Sections, cross-headings and
Level 1/2 subheadings are recognised from Word *styles* — see
`BaseLegislativeDocumentParser.IsSectionHeading` and `IsLevel1Subheading` — and
are unaffected by any of this.) The whole nesting rule is one line in
`ParseParagraphAndSubparagraphs` (`src/parsers/optimized/OptimzedParser.cs:670`):

```csharp
if (nextIndent - MarginOfError <= indent1)
    break;
```

A candidate must be indented more than `MarginOfError` (0.099", declared at
`OptimzedParser.cs:564`) beyond its would-be parent to nest at all. When both
lines sit at indent 0, the candidate becomes a sibling. In the source `.docx`
these paragraphs carry `style="Normal"`, no `<w:ind>` and no `<w:numPr>` — the
numbers are literal text — so the indent rule has nothing to work with.

Nothing anywhere in `/leg` compares a candidate's number to its would-be
parent's number. The only number-aware logic runs in the opposite direction, to
*prevent* nesting that indentation would otherwise create:

- `HasProperParagraphNumber` (`OptimzedParser.cs`) un-nests a `\d+\.?`
  paragraph that was absorbed as a child.
- `CannotBeSubparagraph` (`OptimzedParser.cs`), overridden in
  `src/leg/em/Parser.cs`, blocks a dotted sub-paragraph number from nesting
  inside a bullet.

The raw material for a fix is already present: `HardNumbers`
(`src/parsers/optimized/HardNumbers.cs:88-89`) already recognises
`[1-9]\d*\.\d+\.?` and `[1-9]\d*\.\d+\.\d+\.?`, so these arrive as
`WOldNumberedParagraph` with `Number.Text == "1.1"`. The information is
extracted and then never consulted.

Note the limit: `HardNumbers` recognises **two- and three-component** dotted
decimals only. `1.1.1.1` is not extracted as a number at all, so it cannot be
re-nested regardless of what this pass does. See §7.

## 2. Decision

Re-nest **after** parsing, as a pass over the `IDivision` model tree, before
`Builder.Build`. Do not modify the indentation rule inside
`ParseParagraphAndSubparagraphs`.

Gate the pass per document type through `LegislativeDocumentConfig`, the
existing per-type extension point.

Borrow lawmaker's number-*predicate* vocabulary — depth, child-of,
successor-of — but not its class-per-level architecture. See §6.

### Where it goes

On the model tree, not the output XML. `src/leg/ia/Parser.cs:45-62` already
establishes this shape: IA's `SemanticEnricher` post-processes
`dividedDoc.Body` before the build. A re-nester sits in the same slot.

The alternative placement — inside `BaseHelper.ApplyDocumentSpecificProcessing`
(`BaseHelper.cs`) — is rejected on abstraction grounds, not ordering.
Restructuring serialised AKN would mean reimplementing model semantics against
an `XmlDocument`, and it would have to coordinate with everything that hook
already owns or follows: `LegSimplifier.Simplify` has already run by then
(`BaseHelper`), and per-type TOC generation happens *inside* the hook
(the per-type `Helper` classes for EM, CoP, OD and TN).

eIds cut the other way from how this section first described them. `Builder`
itself assigns none — `MakeDivisionId` returns `null` (`src/leg/Builder.cs`)
— but `TocGenerator` stamps `eId="paragraph_N"` after the build. It walks
`mainBody`'s direct children with a dense counter, and stamps only those that
actually yield a TOC entry (`TocGenerator.cs:115-133`): a `<level>` gets none,
and so does a paragraph that fails `HasParseableNum` or has no first `<p>`.
Nearly all of them qualify — 201 of 202 across the TOC-bearing EM fixtures —
the exception being the `•`-numbered paragraph in `em/…/uksiem_20151873_en`.
So leg *does* emit division eIds, and re-nesting necessarily moves them: a
demoted paragraph loses its identifier, and every later one shifts down.

That is the intended outcome, not a cost to be mitigated. The identifiers the
pass displaces are the ones the flat parse got wrong — in
`em/…/ukdsiem_9780111540145_en`, `paragraph_11` and `paragraph_12` were the
mis-parsed `10.2` and `10.3`. A demoted subparagraph should carry no
`paragraph_N` eId at all and the numbering should pass over it, which is what
the dense walk over TOC targets already does. Published `/paragraph/N`
references are therefore *not* to be preserved across this change.

This makes eIds a useful acceptance test rather than a risk: a division whose
number has more than one component while still holding a `paragraph_N` eId is
one the pass did not demote. It is a *failure* only where the pass accepted the
run — the cases §4 knowingly declines (I2's bullet interruption, I7's
unemphasised interruptions) legitimately keep theirs, so measure against that
set rather than against the corpus as a whole. See §7.

## 3. Why a post-pass rather than fixing it in the parse

The obvious alternative is a `/leg`-side override of
`ParseParagraphAndSubparagraphs` letting a number relationship overrule the
indent test. EM already overrides that method, so this need not touch shared
judgments code. Its genuine merit is that it keeps **one** hierarchy authority
instead of two.

It was rejected because **this problem needs to see a whole run at once.**

A survey of all 120 `.akn` fixtures found 17 files where flat and dotted
numbering appear as siblings under one parent. They fall into three groups:

**Clean runs — the target case (8 files)**

| Fixture | Numbering |
|---|---|
| `em/original filenames/uksiem_20240868_en_001` | `1., 1.1, 1.2, 2., 2.1, 2.2, 3., 3.1, …` |
| `em/original filenames/uksiem_20241017_en_002` | `1., 1.1, 1.2, 2., 2.1, 2.2, …` |
| `em/original filenames/uksiem_20250133_en_001` | `1., 1.1, 2., 2.1, 2.2, 3., 3.1, …` |
| `em/original filenames/ukdsiem_9780111101568_en` | `1., 2., 2.1, 3., 3.1, 4., 4.1, …` |
| `cop/uksicop_20200860_en` | `1, 1.1 … 1.9, 2, 2.1 … 2.6` (no trailing dot on parent) |
| `od/sdsiod_9780111061145_en_003` | `1.–5., 5.1–5.5, 6.–9.` |
| `od/sdsiod_9780111061145_en_002` | `2., 2.1., 3., **3.1. … 3.10.**, 4., 5.` |
| `od/nisrod_20160090_en` | `5., 5.1., 5.2.` (trailing dot on children) |

`sdsiod_9780111061145_en_002` is the reason ordering must compare components
**numerically**: it runs `3.9.` → `3.10.`, which sorts backwards as strings.

**Gapped runs (3 files)** — children do not start at `.1`:

- `em/original filenames/ukdsiem_9780111100349_en` — `9., 9.2` with no `9.1`
- `em/original filenames/uksiem_20140198_en` — same
- `em/original filenames/ukdsiem_9780111540145_en` — `10., 10.2, 10.3`

Any rule requiring a child run to begin at `.1` fails these.

**Runs where naive re-nesting would damage currently-correct output (6 files)**

- `cop/uksicop_20180470_en`, `cop/ukdsicop_9780111162422_en` — the table of
  contents contributes its own `1., 3., 4. … 10.` *before* the body's
  `1., 1.1, 1.2, 1.3, 2., 2.1`. A pairwise prefix test would attach the body's
  children to the TOC's `1.`.
- `od/ukdsiod_9780111169445_en` — `1.1.`, `1.2.`, `1.3.` scattered among
  `2.`–`18.`, far from any `1.` (188 numbered siblings in one `mainBody`).
- `cop/uksicop_20160518_en` — an unrelated `1 … 12` run interleaved between
  `2.2` and `2.3`.
- `em/original filenames/uksiem_20151873_en` — a bullet (`•`) sits between
  parent `7.` and child `7.1.`.
- `ia/original filenames/ukia_20150171_en` — `1.1` appears *before* `1.`;
  `10.1` sits among `59.`–`65.`.

8 + 3 + 6 = 17.

A streaming parser is not *incapable* of this — it could buffer a candidate run
and roll back, which is roughly what lawmaker's `ParseAndMemoize`
(`src/lawmaker/parsers/HContainer.cs`) exists to make affordable. The
argument is weaker but still decisive: the in-parse route would have to *add*
speculative buffering it does not currently have, whereas a post-pass gets the
complete sibling sequence for free.

Two further reasons, both secondary:

- **Testability.** Flat list in, tree out — a pure function, unit-testable
  without a `.docx`. The in-parse version needs a fixture document per case.
- **Blast radius.** `ParseParagraphAndSubparagraphs` interleaves quoted
  structures, wrap-up handling, hanging indents and the
  `HasProperParagraphNumber` guard. Threading a second hierarchy signal through
  all of it is a large change to a function the judgments parser also depends on.

### Accepted costs

- **Two hierarchy authorities.** Documents that *do* indent `1.1` under `1.`
  already parse correctly, and the pass must not double-nest them. Invariant I5
  in §4 is what makes this safe.
- **A post-pass can only rearrange what survived.** If the flat parse ever
  *absorbed* a line into an intro that should have remained a separate child,
  no re-nesting recovers it. In the fixtures examined the information survives
  intact as clean `num` + `content` siblings, but this is the failure mode that
  would force a return to the in-parse approach.

## 4. Run coherence — the safety contract

The rule that separates the three groups in §3 is the substance of this change,
not an implementation detail, so it is fixed here rather than deferred. Each
invariant is followed by the fixture that motivates it.

- **I1 — Locality.** Only a contiguous run of adjacent siblings may be
  restructured. Never search backward through the sibling list for a number
  whose prefix happens to match.
  *Defends `cop/uksicop_20180470_en` and `cop/ukdsicop_9780111162422_en` (TOC
  `1.` capturing body children), `od/ukdsiod_9780111169445_en` (`1.1.`
  scattered among `2.`–`18.`), and `ia/…/ukia_20150171_en`, whose section runs
  `1.1, 1., 2., 3., 4., 1.3, …` — the leading `1.1` has no open parent at all,
  so it is I1/I2 that rejects it, not any ordering rule.*

- **I2 — Interruption closes the run.** Validity is assessed against the
  **whole open ancestor stack**, not a single open parent. A candidate is
  admissible if it is a direct child of *any* currently-open ancestor, which
  then closes every deeper level. Anything admissible under no open ancestor
  terminates the run.
  *Defends `cop/uksicop_20160518_en` (unrelated `1 … 12` between `2.2` and
  `2.3`) and `em/…/uksiem_20151873_en` (bullet between `7.` and `7.1.`).*

  The ancestor stack is not incidental. Phrased against a single open parent,
  this invariant would terminate the run at `1.1.1` — a grandchild, not a
  child — destroying the three-component support §7 puts in scope. Lawmaker's
  `ProvisionRecords` (§6) is the conceptual precedent for holding ancestors on
  a stack, but not an equivalent mechanism: it tracks only `Prov1`/`SchProv1`
  by its own documentation, and every query routes through `Peek`, inspecting
  the **top record only**. `/leg` needs to match a candidate against *any* open
  ancestor, which is a richer whole-stack search than that class performs.

- **I3 — Gaps are permitted.** A child run need not begin at `.1`, and need not
  be free of holes.
  *Required by all three gapped fixtures in §3.*

- **I4 — Monotonic within a sibling group, compared numerically.** Ordering
  applies only among candidates sharing the same immediate prefix, and compares
  the final component as an integer rather than as text. In
  `1., 1.1, 1.1.1, 1.1.2, 1.2` the constrained groups are `{1.1, 1.2}` and
  `{1.1.1, 1.1.2}` independently — a global rule over the run would see final
  components `1, 1, 2, 2` and wrongly reject it.
  *Required by `od/sdsiod_9780111061145_en_002` (`3.9.` → `3.10.`). No fixture
  motivates the strictness itself — see the policy note below.*

  **Strict increase is an unevidenced safety policy**, not an observed rule: no
  fixture exhibits a repeated or regressing sibling number. It is the
  conservative default — decline to restructure rather than guess.

  **Failure is atomic.** If repetition or regression invalidates a candidate
  run, leave the whole run flat; never transform the valid prefix. Otherwise
  `2.1, 2.1` or `2.2, 2.1` yields a half-nested tree that is worse than the
  untouched input.

- **I5 — Existing structure is preserved.** Divisions that already have
  children are recursed into, never flattened and never reattached to the
  parent they already belong to. A document already correctly nested by
  indentation must come through unchanged.
  *Exercised by the corpus, not hypothetical: several affected fixtures already
  carry indentation-derived nesting alongside their flat run —
  `cop/ukdsicop_9780111162422_en` and `cop/uksicop_20180470_en` have 3
  `<subparagraph>` elements each, `cop/uksicop_20160518_en` has 8, and
  `em/…/uksiem_20151873_en` has 18.*

- **I6 — Idempotent.** Applying the pass twice yields the same tree as applying
  it once.

- **I7 — Run-in headings are absorbed, not obeyed.** An unnumbered, unnamed
  single-line division whose text is wholly bold, italic or underlined does not
  terminate a run when a genuine child follows it; it moves inside the parent.
  A heading with no child after it is left where it is, because it introduces
  whatever comes next rather than what precedes it.
  *Required by `em/…/uksiem_20240868_en_001`, `uksiem_20241017_en_002` and
  `uksiem_20250133_en_001`, where a run-in heading such as "What does the
  legislation do?" sits between `4.` and `4.1`. Without this, `4.1`–`4.8` stay
  flat and keep the `paragraph_N` eIds §2 identifies as wrong.*

  "Run-in heading" rather than "cross-heading" throughout, because `/leg`
  already uses the latter for a different, section-level thing:
  `BaseLegislativeDocumentParser.IsCrossHeading` recognises a flush-left bold
  line that `ParseCrossHeading` turns into a `Parse.CrossHeading` holding
  `Section` children. What I7 absorbs is what that parse *rejects* — a bold line
  heading numbered paragraphs rather than sections, which falls through to a
  `WDummyDivision`. The predicate is `IsAbsorbableRunHeading` for the same
  reason.

  This is deliberately narrow. Ordinary unemphasised content still closes the
  run under I2, which is why the sentence fragments interrupting
  `cop/ukdsicop_9780111162422_en` are left alone. The emphasis test reuses
  `IsAllBold`/`IsAllItalicized`/`IsAllUnderlined`, which
  `OptimzedParser.cs:650` already uses to mean "heading-like, not a
  continuation"; a length cap keeps a wholly-bold sentence of body text from
  qualifying.

  **The 90-character cap is a heuristic, chosen before it was measured.** Unlike
  I4's strict ordering, which no fixture evidences at all, this one the corpus
  turns out to corroborate after the fact. In the 120-`.akn` corpus surveyed on
  2026-08-20 there are 215 emphasised single-line divisions preceding a dotted
  child — all of them in EM and CoP, the two enabled types — and among those the
  longest below the cap is 89 characters while the shortest above it is 92. So 90
  falls in a gap rather than through a cluster. Fourteen candidates exceed it,
  running to 557 characters — wholly emphasised passages of body text that the
  cap correctly declines. Treat the
  exact number as conservative policy, not as a derived constant: it should move
  only on evidence, and a fixture that lands between 89 and 92 would be reason to
  re-measure.

  **Where the heading lands is forced by the schema, and is not the right
  model.** `paragraph` and `subparagraph` both allow either `content` or
  `intro? subparagraph+ wrapUp?` (`em-subschema.xsd:143-172`). So a heading
  arriving before any child joins the parent's intro, and one arriving between
  children — half the affected runs — can only become a numberless
  `subparagraph`. Both keep it directly above the child it introduces, which is
  where the Word original puts it, and numberless `subparagraph`s already occur
  in this corpus. Neither expresses what the heading *is*. See §7.

**Known consequence of I2.** `em/…/uksiem_20151873_en` becomes a permanent
no-op — the bullet between `7.` and `7.1.` closes the run, so those paragraphs
stay flat. This is a knowingly unfixed case, chosen because letting a run
survive an arbitrary interruption is what breaks `uksicop_20160518_en`.

## 5. Model conversion — the likely implementation trap

Re-nesting is not simply moving an `IDivision` from one list to another. The
model distinguishes paragraph from subparagraph *by type*
(`src/model2/Subparagraphs.cs`):

| Type | `Name` | Content held as |
|---|---|---|
| `WNewNumberedParagraph` / `BranchParagraph` | `paragraph` | `Contents` / `Intro` + `Children` |
| `LeafSubparagraph` | `subparagraph` | `Contents` |
| `BranchSubparagraph` | `subparagraph` | `Intro` + `Children` |

So a demotion is a type conversion, and three-component input needs a second
one: when `1.1.1` arrives, the already-demoted `1.1` leaf must become a branch,
with its `Contents` becoming `Intro`.

**Demotion has precedent.** Paragraph → subparagraph exists already, though
not in a form this pass can call:

- `BranchSubparagraph.Demote(BranchParagraph)` — live, at `Subparagraphs.cs:48`.
- `LeafSubparagraph.Demote(WNewNumberedParagraph)` — **commented out**, at
  `Subparagraphs.cs:62`.
- `DemoteToSubparagraph` — private to `OptimizedParser`
  (`OptimzedParser.cs`), covering both branch and leaf shapes.

**The three-level conversion does not exist anywhere.** Turning an existing
`LeafSubparagraph` into a `BranchSubparagraph` — moving `Contents` to `Intro`
and attaching `Children` — has no implementation in the repo. Every
`BranchSubparagraph` construction site (`Subparagraphs.cs:48`,
`OptimzedParser.cs:551`, `src/parsers/common/enrich/Enrich.cs:71`,
`src/parsers/ukut/Date2.cs:73`) takes an `Intro` from something that is already
a branch. `PromoteFromSubparagraph` (`OptimzedParser.cs`) does not help: it
moves along the other axis, subparagraph → paragraph.

So the work is reusable precedent for demotion **plus one genuinely new
conversion** — and that new one, exercised by no fixture (§9), is where the
risk sits.

## 6. Why not lawmaker's class-per-level design

`/src/lawmaker` solves a similar-looking problem and solves it well, entirely
from numbering with no reference to indentation. It was examined closely and
deliberately not adopted. The reason is not effort — it is that the shape does
not transfer.

**Lawmaker numbering is format-per-level.** Each level owns a number format,
and the format alone determines the level, context-free:

| Level | Declared at | Pattern | Example |
|---|---|---|---|
| `Prov1` | `models/Prov.cs` | `^[A-Z]*\d+(?:[A-Z]+\d+)*[A-Z]*\.$` | `1.`, `1A.` |
| `Prov2` | `models/Prov.cs` | `^\([A-Z]*\d+…\)$` | `(1)` |
| `Para1` | `models/Para.cs` | `^\([a-z]+\)$` | `(a)` |
| `Para2` | `models/Para.cs` | `^\(z*[ivxl]+[a-z]{0,3}\)$` | `(i)` |
| `Para3` | `models/Para.cs` | `^\([A-Z]+\)$` | `(A)` |

Because `(a)` is a Para1 *wherever it appears*, a static class per level is
meaningful: `Para1.IsValidNumber` can be a pure function of the string.
Admissible children are then a type whitelist — `Para1.IsValidChild`
(`models/Para.cs`), applied in `parsers/Para1.cs`.

**`/leg` numbering is positional.** `1.1` has no level identity in the
abstract; it has a level identity *relative to its parent*. Encoding that as
classes would produce a family of types identical except for a dot count —
data wearing a type costume. Depth here is a property of a value, not a type.

So the class hierarchy does not transfer. Three things do:

1. **Level identity derived from the number, not the indent** — the central
   idea, and the whole point of this change.
2. **A successor test to close a level.** `IsSubsequentAlphanumeric`
   (`parsers/HContainer.cs`), called from `BreakFromProv1`, asks
   "is this number the successor of the one I am
   currently inside?" and ends the level if so. Its companion
   `IsSubsequentAlphabetic` (`HContainer.cs`, used in `parsers/Para1.cs`)
   resolves the roman-versus-letter collision — `(h)` → `(i)` is a Para1
   sibling, not a Para2 child. The `/leg` analogue answers "does `2.` close the
   `1.x` run?" — invariant I2 in §4.
3. **An ancestor-number stack.** `ProvisionRecords`
   (`src/lawmaker/akn/ProvisionRecords.cs`) tracks ancestor numbers properly.
   `/leg` has an embryonic version in EM's `Stack<string> _parentNumbers`
   (`src/leg/em/Parser.cs`), used only to suppress bullet nesting.

Note that `IsSubsequentAlphanumeric` answers the **sibling** question, not the
**parent** question, and normalises dots to spaces
(`HContainer.cs:390-391`) — discarding exactly the structure that matters here.
It is a model to imitate, not code to reuse.

One further caveat on the parallel: lawmaker can be purely number-driven
because Lawmaker documents are machine-generated and rigidly conventional. The
`/leg` corpus is hand-authored Word. The rule here is *not* that indentation
corroborates a nesting decision — in the target case parent and child sit at
the same indent, so it corroborates nothing. It is that **hierarchy already
derived from indentation is preserved**, and numbering only repairs runs that
came out flat. That is invariant I5.

## 7. Scope

**In scope.** Dotted decimal numbering up to **three components** — `1.` /
`1.1` / `1.1.1` — with or without trailing dots, and with or without a trailing
dot on the parent. Three is not an arbitrary choice: it is the limit of what
`HardNumbers` extracts (`src/parsers/optimized/HardNumbers.cs:88-89`). Deeper
numbering would require extending that first, which is not in scope.

Three components stay in scope despite **no fixture exercising them** — the
corpus is entirely two-component. That makes the unit tests in §9 the only
evidence for this path, and the leaf-to-branch conversion in §5 the part most
likely to be wrong on first attempt.

**Deliberately deferred.**

- **Letter-numbered sub-items.** In `cop/uksicop_20180470_en`, `2.1` is
  followed by `A., B., C. …`, conceptually its children. Dot-count depth says
  nothing about these. Out of scope for the first pass; recorded here so the
  omission is a decision rather than a discovery during fixture regeneration.
- **IA.** `ia/original filenames/ukia_20150171_en` shows dotted and flat
  numbering interleaved in no coherent order (`1.1` before `1.`; `10.1` among
  `59.`–`65.`), most likely cover-sheet or form-field content rather than body
  structure. IA also carries by far the heaviest special-casing in `/leg`
  (`src/leg/ia/Helper.cs`, 1,586 lines). Leave IA gated off initially.
- **OD.** Mixed evidence — three clean fixtures, but also
  `od/ukdsiod_9780111169445_en`, the worst case found. Enable only once its
  fixture diff has been inspected.
- **EN and TN.** The survey found *no* affected fixtures for either type.
  Nothing to gain; leave both off.
- **A model for titled groups of subparagraphs — the shape we actually want.**
  I7 absorbs a run-in heading because leaving it in place would forfeit the
  nesting below it, but neither destination the schema offers is honest about
  what the heading is. The right structure is a *titled grouping*: a child of
  the paragraph that carries the heading and contains the subparagraphs it
  introduces, so that

  ```
  4.   Overview of the Instrument
       What does the legislation do?      ← heading
  4.1  …
  4.2  …
  ```

  yields a group headed "What does the legislation do?" holding `4.1` and
  `4.2`, rather than a heading flattened into the parent's intro with the
  children beside it.

  This is deferred because it is a modelling question, not a coding one, and
  the answer is not yet known. Three things block it, and the second is the
  reason it cannot ride along with this change:

  1. **Schema.** `subparagraph` permits `num?` then `content` or
     `intro? subparagraph+ wrapUp?` (`em-subschema.xsd:159-172`). No `heading`.
     `level` and `section` both allow one, but neither may appear inside a
     `paragraph`. The subschemas would have to be widened.
  2. **Model.** `BranchParagraph`, `BranchSubparagraph` and `LeafSubparagraph`
     each override `Heading` to `null` on purpose
     (`src/model2/Subparagraphs.cs`), so the model forbids this today. Those
     types are shared with the **judgments** parser — changing them reaches
     outside `/leg`, which is exactly the blast radius §2 chose this design to
     avoid.
  3. **Builder.** Would need to emit the heading. Small, given the other two.

  So the shape recorded above is the target and I7's placement is an interim
  accommodation of the current model. Whoever takes this on should expect to
  change the model rather than work around it, and to decide whether a titled
  group is a new division type or an existing one gaining a heading.

- **Rendering parity — a known requirement; mechanism deferred.**
  Demoting `1.1` shifts it right by 0.5in in the HTML:
  `associated-docs.css:6,8` sets `margin-left: 0.5in` on both `.paragraph` and
  `.subparagraph`, and `akn2html.xsl:94-110` emits the class from `local-name()`,
  so nesting compounds the margins. These documents are meant to look like
  their Word originals: hierarchy inferred from numbering must not, by itself,
  invent visual indentation absent from the source.

  I7 adds a second case, and a harder one. An absorbed mid-run heading becomes a
  numberless `<subparagraph>`, so it takes the same `margin-left: 0.5in` from
  `associated-docs.css:8` while having no number to anchor a rule to — a
  flush-left bold line in the Word original that renders as an indented,
  unnumbered fragment. Whatever marks demoted paragraphs has to cover these too,
  and they cannot be identified after the fact by their numbering, because they
  have none.

  A marker such as a `no-indent` class is the likely mechanism, but its exact
  representation and CSS are deliberately undecided. A global CSS rule for all
  subparagraphs would conflate a flush-left `1.1` repaired by this pass with a
  genuinely indented subparagraph. The pass has the reliable provenance: it
  knows which divisions it demoted, while the division model has no direct
  rendering-class property and only its contained blocks commonly retain the
  originating `WLine`. That provenance therefore needs carrying through the
  model and `Builder` into the XML/HTML. `akn2html.xsl:105-108` already copies a
  division's XML `@class` into the HTML class list, so that route is plausible,
  not yet decided.

  The eventual CSS must be checked against both the accumulated section margin
  and the number-heading offsets: `associated-docs.css:7,9` gives paragraph and
  subparagraph headings different negative `margin-left` values. Removing only
  the subparagraph's outer margin may not reproduce the original alignment.
  Deferred from the hierarchy algorithm because the right remedy depends on
  inspecting the actual rendering — see §9 — but required before rollout.

- **Mixed indentation under one parent is handled, not deferred.**
  `em/…/ukdsiem_9780111540145_en` indents `10.1`, so the parse already nested
  it, but leaves `10.2` and `10.3` flush. A run therefore has to be able to
  start on a parent that *already has children*, merging both sources under it.
  The original design did not anticipate this and would have stranded the flush
  siblings; see §10.

The wins are concentrated in EM and CoP; those are the types to enable first.

## 8. Downstream effect on statistics

`StatisticsCalculator.Calculate` runs on the **model**, and `BaseHelper` calls
it before `Builder.Build` — so it sees the tree this pass has already rebuilt.
`CountParagraphsInDivisions` (`StatisticsCalculator.cs`) counts paragraph
divisions and excludes their subparagraph descendants. The `if … else if`
structure matters: a division named `paragraph` is counted and then *not*
traversed at all, so anything demoted beneath it disappears from the total.

The corpus confirms the semantics directly —
`cop/ukdsicop_9780111162422_en` and `cop/uksicop_20180470_en` each contain 88
`<paragraph>` and 3 `<subparagraph>` elements, and each reports
`BodyParagraphs=88`.

A run of `1., 1.1, 1.2` therefore drops from 3 counted paragraphs to 1. On
`cop/uksicop_20200860_en` — 80 numbered siblings, most of them dotted — the
reduction is large.

**This is intended.** Placing the pass after statistics would preserve the old
counts, but that games the metric: once `1.1` is a subparagraph, not counting it
as a paragraph is the more accurate answer. The consequence is that
`ukm:BodyParagraphs` and `ukm:TotalParagraphs` will change in exactly the
fixtures the change is meant to improve. **Falling paragraph counts in
clean-case fixtures are expected output, not damage.**

## 9. Verification

### Unit tests — mandatory, not optional

The fixture corpus contains **no three-component numbering at all**: in the
120-`.akn` corpus surveyed on 2026-08-20, **12,596 `<num>` elements** were
examined and **zero** match `^\d+\.\d+\.\d+\.?$`. Three-component support — and
with it the leaf-to-branch conversion identified in §5 as the riskiest part of
the change — would otherwise
ship completely unexercised, and fixture regeneration cannot cover it.

The search must be scoped to `<num>`, and the two searches differ. The `<num>`
values above were tested against the **anchored** `^\d+\.\d+\.\d+\.?$`. An
unscoped **substring** search for `\d+\.\d+\.\d+\.?` over the raw files returns
288 occurrences, dominated by `ukm:Parser` version values (`1.0.0` and `1.9.0`,
120 each) and also including dates (`9.1.2016` in `tn/uksitn_20151392_en`) and
citation fragments (`3.4.1` in `ia/…/ukia_20140204_en`, `101.3.482` inside a
URL in three IA fixtures). None establishes three-component paragraph
numbering.

Unit tests over the pass (flat list in, tree out — no `.docx` required) must
therefore cover at least:

| Case | Property under test |
|---|---|
| `1, 1.1, 1.1.1, 1.1.2, 1.2` | three-component nesting; I2 against the ancestor stack |
| leaf-to-branch conversion | `Contents` becomes `Intro`, `Children` attached (§5) |
| `3.9, 3.10` | numeric, not lexicographic, ordering (I4) |
| `1.`/`1.1`, `1`/`1.1`, `5.`/`5.1.` | trailing-dot normalization — all three forms §7 promises, each present in the corpus |
| `9., 9.2` with no `9.1` | gaps permitted (I3) |
| interrupted run; distant prefix match | I2 and I1 |
| already-nested input | preserved unchanged (I5) |
| pass applied twice | idempotency (I6) |
| `2.1, 2.1` and `2.2, 2.1` | invalid run left **wholly** flat, not half-nested (I4) |

### Fixture regeneration

All 17 affected fixtures currently bake the flat output in as their expected
result, so the fixture diff is the proof. Regenerate the enabled types per
`src/leg/README.md`:

```shell
dotnet test test/test.csproj --filter "FullyQualifiedName~TestEM.RegenerateAllTestFiles" -e UPDATE_XML="true"
dotnet test test/test.csproj --filter "FullyQualifiedName~TestCoP.RegenerateAllTestFiles" -e UPDATE_XML="true"
```

**Do not regenerate the `.html` snapshots during the initial structural
checkpoint.** `RegenerateAllHtml` would bake in the 0.5in shift described in §7
and destroy the signal the rendering work depends on. Regenerate `.akn` only,
then run `TestEMHtml` and `TestCoPHtml` and inspect their failures — they are
diagnostic evidence for how rendering preservation should behave. Coverage is
complete: every one of the ten fixtures the pass changed (seven EM, three CoP)
has an `.html` golden, out of 31 and 10 respectively. Those failures are
expected while developing the hierarchy pass and are not an acceptable final
state — but note that they are also **not** resolved by making them pass against
these goldens, which encode the pre-change DOM. See acceptance criterion 6 for
the sequence.

Then run the **whole** `/leg` suite — not only the regenerated types — to prove
the disabled types are untouched:

```shell
dotnet test test/test.csproj --filter "FullyQualifiedName~UK.Gov.Legislation"
```

Acceptance criteria:

1. Clean-case fixtures **for enabled types only** — the four EM entries plus
   `cop/uksicop_20200860_en`, five in all — gain hierarchy. The three OD clean
   cases must show no diff while OD is gated off.
   *Met: ten fixtures changed, three CoP and seven EM, and no other type moved.*
2. The damaging-case fixtures are not disturbed — which is **not** the same as
   "no diff". In `cop/uksicop_20180470_en` and `cop/ukdsicop_9780111162422_en`
   the hazard was that the *TOC's* `1.` would capture the body's children. The
   body's own `1.` and `2.` runs are adjacent and legitimate and *do* nest;
   what must stay flat is the TOC's `1., 3., 4. … 10.`. The remaining
   damaging-case fixtures (`cop/uksicop_20160518_en`, `em/…/uksiem_20151873_en`,
   and the IA and OD cases while gated off) show no diff at all. Four of these
   files already carry indentation-derived subparagraphs (I5), which must
   survive untouched.
3. The three gapped fixtures nest despite their missing `.1` (invariant I3).
4. Paragraph-count metadata changes only in fixtures whose structure changed —
   see §8.
   *Met, and checked rather than assumed: in each of the ten, the new
   `ukm:BodyParagraphs` equals the number of outermost `<paragraph>` elements,
   which is what `StatisticsCalculator.CountParagraphsInDivisions` computes —
   it counts a paragraph and does not recurse into it.*

   Two other things move in a regenerated fixture and are **not** evidence of
   anything: `ukm:Parser` (1.9.0 → 1.12.0, since the fixtures predate the
   current version) and the `FRBRdate` stamps. Both are stripped by the
   comparison XSLT in each per-type test before assertion, so neither is
   compared; the version stamp merely becomes heterogeneous across the corpus,
   as it does after any regeneration.

   **eIds behave as §2 requires.** No `<subparagraph>` anywhere in the corpus
   carries a `paragraph_N` eId, and the count of divisions whose number has more
   than one component while still holding one falls from 82 to 6, measured over
   the EM and CoP fixtures on 2026-08-21. The remaining
   six are `em/…/uksiem_20151873_en`, the bullet-interruption case I2 knowingly
   declines. TOC hrefs all still resolve — zero dangling before or after — and
   the agreement between an entry's displayed number and its target's own
   `<num>` rises sharply (5/21 → 13/13, 2/29 → 13/13, 10/15 → 13/13,
   5/21 → 13/13), because the entries that vanished were body-text fragments
   from mis-parsed children.

   **No text is lost.** Comparing the multiset of `<p>` and `<num>` strings in
   `mainBody` before and after regeneration gives an exact match in all ten
   fixtures. Do not use `itertext()` over `mainBody` for this: it picks up
   pretty-printing whitespace, which necessarily differs when nesting changes,
   and will report losses that are not there.
5. EN, TN, IA and OD fixtures are byte-identical.
6. **Visual parity with the Word original, verified by looking at rendered
   output.** The oracle is *not* "the `.html` snapshots pass".

   `akn2html.xsl:94-110` builds a section's class from `local-name()` plus any
   `@name`/`@class`, then appends `num` and `heading` when those children exist,
   and emits `id` only when there is an `@eId`. So a demoted `10.2` that was

   ```html
   <section id="paragraph_11" class="paragraph num">
   ```

   (golden line 674 of `em/…/ukdsiem_9780111540145_en.html`) is now
   `<section class="subparagraph num">`, one level deeper and with no `id`.
   Both differences are intended: §2 explains why the old eIds were wrong.

   Under the current stylesheet and the exact string comparator in
   `TestEMHtml`/`TestCoPHtml`, those goldens therefore cannot match. That is a
   property of this stylesheet and this comparator, not a law. Two routes could
   in principle reconcile them, and they fail differently:

   - A **rendering rule** that recreated the old DOM would make the HTML
     misrepresent the hierarchy the AKN now states correctly — the output would
     assert a structure the document no longer has.
   - A **normalising comparator** would change nothing about the HTML; it would
     ignore selected differences when comparing. It misleads in a different way:
     it weakens the snapshot as a regression guard and can conceal real
     structural drift, while still establishing nothing about visual parity.

   Neither is the design chosen here, but the criterion should not pretend the
   options do not exist.

   What follows from this is narrower and more useful: exact agreement with the
   old goldens is neither expected now nor sufficient later to demonstrate
   visual parity.

   The sequence is therefore:

   1. **Portability first.** The comparisons must *execute* rather than skip.
      `TestEMHtml` and `TestCoPHtml` call `Assert.Skip` unless Oxygen/Saxon is
      reachable, and `HtmlBuilder` hardcodes `jre/bin/java.exe`
      (`src/leg/HtmlBuilder.cs:34,94`), so they cannot run on macOS or on the
      `ubuntu-latest` CI at all. **A green run is not evidence of no shift.**
   2. **Leave the goldens stale meanwhile.** Their diff is the diagnostic — it
      is how the rendering consequences become visible. Regenerating them now
      would destroy exactly the signal §7's work depends on.
   3. **Land the rendering follow-up in §7** and confirm parity by inspecting
      rendered output against the Word original. A DOM snapshot cannot
      establish this: it can only report that the DOM changed, which is already
      known and intended.
   4. **Regenerate the goldens only after that confirmation**, as a regression
      guard rather than as the oracle. Intentional DOM differences legitimately
      survive a parity check.

## 10. Mechanism

The core pass is two new files under `src/leg/common/` — `DottedNumber.cs` and
`DottedNumberRenester.cs`. Three existing files integrate it:
`BaseLegislativeDocumentParser.cs` calls it, `LegislativeDocumentConfig.cs`
gates it per document type, and `src/leg/em/Parser.cs` now shares its
dotted-number definition for the bullet guard.

### `DottedNumber.cs` — the number as a value

A readonly struct wrapping the integer components. `TryParse` accepts one to
three components with an optional trailing dot, so `1.`/`1`, `5.1`/`5.1.` all
canonicalise to the same value — every combination occurs in the corpus, so
normalisation happens once here rather than at each call site. The shape regex
uses `[0-9]` rather than `\d`, which in .NET also matches non-ASCII digits.

Two predicates carry the semantics: `Depth` and `IsChildOf` (exactly one level
deeper, sharing every parent component). `Last` exposes the final component as
an **int**, which is what makes `3.9 → 3.10` order correctly. I4's
sibling-group scoping is *not* a predicate here — it is the paired truncation of
`open` and `lastChild` in `MeasureRun`, which is where to look when changing it.

### `DottedNumberRenester.cs` — the pass

`Renest(IEnumerable<IDivision>)` recurses into every child list first, then
walks the siblings looking for runs. Two phases per run, which is what makes
I4's atomicity possible:

- `MeasureRun` validates a whole candidate run and returns its length, or **0**
  if anything invalidates it. Returning 0 is the atomicity mechanism: the caller
  emits the head unchanged and advances one item, so the remainder is reached as
  ordinary flat divisions and no prefix is half-transformed.
- `BuildRun` then constructs the tree, only for runs already known good.

Ancestors are held in a parallel pair of lists — `open` (the numbers) and
`lastChild` (the final component most recently seen under each). Both are
truncated together whenever a candidate resolves to a shallower ancestor, which
is what scopes I4 to a sibling group: returning to `1.2` discards whatever
ordering state `1.1.x` had accumulated.

`FindOwner` searches `open` from the deepest end, so `1.1.1` resolves against
`1.1` rather than failing against `1.` — I2's whole-stack matching.

### The leaf-to-branch conversion

The `Frame` inner class is where §5's missing conversion lives. A frame holds
the source division's content plus any children it already had, and `Close`
picks the output type on the way out:

| Children | Position | Result |
|---|---|---|
| any | run head | `BranchParagraph` (`Intro` = content) |
| none | nested | `LeafSubparagraph` (`Contents` = content) |
| some | nested | `BranchSubparagraph` (`Intro` = content) |

Because a frame's content is only committed at `Close`, a division that starts
as a leaf and acquires children becomes a branch with its former `Contents` as
`Intro` — no separate conversion step, and the case with no prior
implementation in the repo falls out of the construction order.

### Run-in heading absorption

`IsAbsorbableRunHeading` recognises I7's shape: null `Name` (which is what `Builder`
renders as `<level>`), no number, no heading, a single `WLine` whose text is
wholly bold, italic or underlined and no longer than the cap.

`MeasureRun` treats one as transparent only when `ChildFollows` finds an
admissible child past it, so a heading at the end of a run — `uksiem_20240868_en_001`
head `10.` has the shape `H c c H` — is left for whatever comes next.
`BuildRun` holds absorbed headings in `pending` and flushes them through
`Frame.Absorb` **after** `CloseDownTo`, so a heading lands on the level that
owns the child below it rather than on a deeper frame still open: in
`4., 4.1, 4.1.1, H, 4.1.2` the heading belongs to `4.1`, not `4.1.1`.

`Absorb` chooses between the two destinations the schema permits (I7): the
frame's content when it has no children yet, a numberless `LeafSubparagraph`
otherwise. `pending` is also flushed after the loop, which `MeasureRun`'s
contract makes unreachable — it is there so that a future mistake misplaces
content rather than dropping it.

Most affected runs need the second destination, so a leading-heading-only rule
was never an option: of the twenty runs carrying an absorbable heading, twelve
have one between children, and three of those (`9.` in each of the three EM
fixtures) have no leading heading at all.

### Guarded assumptions

Two conversions are assumed rather than implemented, and both now fail loudly
instead of losing content silently.

`Frame.From` **refuses a division that carries a heading.** No type `Close` can
emit is able to hold one — `BranchParagraph`, `BranchSubparagraph` and
`LeafSubparagraph` each override `Heading` to `null` on purpose. This is
reachable, not theoretical: `Models.BranchParagraph`
(`src/leg/models/Structure.cs`) is `"paragraph"`-named, is an `IBranch` and
has a settable `Heading`, and IA's `SemanticEnricher` builds those — so enabling
IA per §7 without the titled-group model would otherwise discard headings with
no error and no failing test.

`Frame.Close` **refuses a childless frame carrying wrap-up**, which a leaf
subparagraph cannot represent. Unreachable from today's parsers
(`OptimzedParser.cs:633` only builds a wrap-up branch alongside subparagraphs).

`BuildRun` likewise throws if `MeasureRun` accepted an item it cannot classify,
rather than letting `owner = -1` drain the frame stack inside `CloseDownTo`.
The two traversals must agree; a disagreement is a bug in one of them, not a
document to fall back on.

### Scope — how repair composes with existing structure

I5 says divisions that already have children are recursed into, and §7 keeps
three components in scope. A partially nested input is where those two meet:
the parser indents `4.1` under `4.` but leaves `4.1.1` and `4.1.2` at the same
depth, so they arrive as *siblings* of `4.1`.

Recognising that run needs to know where it sits. `Scope` carries three things
for one sibling list — the element name a head must have, the number depth it
must have, and whether a local root closes as a paragraph or a subparagraph:

| Sibling list | Head name | Head depth | Local root closes as |
|---|---|---|---|
| body, section, cross-heading | `paragraph` | 1 | `BranchParagraph` |
| inside a numbered paragraph | `subparagraph` | parent + 1 | `BranchSubparagraph` |

`Scope.Inside` returns the inert default when the parent is unnumbered, or too
deep for a run to fit: a head sits one component below its parent and needs a
child below *that*, so with `MaxComponents` = 3 only a depth-1 parent can host a
nested run. The local root closing as a `BranchSubparagraph` is the part that is
easy to get wrong — emitting a `BranchParagraph` there would put a `paragraph`
inside a `paragraph`, which `em-subschema.xsd:143-157` forbids.

No fixture exercises this; the corpus has no three-component numbering at all,
so the unit tests are the only evidence, as §9 already says of that whole path.

### Exhaustive recursion

`RenestWithin` handles each supported branch type explicitly, passes leaves
through, and **throws** on any other `IBranch`. Silently returning an
unrecognised branch reads as a clean pass over a subtree the renester never
entered — the failure mode that hid `Models.BranchParagraph`, which was missing
from the switch while simultaneously being the type the heading guard exists
for. Running the `/leg` suite with the throw in place raises nothing — but note
what that does and does not show. The pass is gated on
`Config.RenestDottedNumbers` (`BaseLegislativeDocumentParser.cs:40`), so only the
41 EM and CoP fixtures reach this switch at all; the 79 IA, OD, EN and TN
fixtures never enter it. The evidence covers the enabled types only, which is
precisely why enabling another type per §7 should be done with this throw in
place rather than trusted in advance.

### One dotted-number definition

`src/leg/em/Parser.cs` previously carried `^\d+\.\d+\.?$` to stop a dotted
body number being absorbed into a bullet. Two components only — so `1.1.1`
slipped past the guard, was nested under the bullet, and was then faithfully
preserved by I5, because the pass cannot undo a mis-nesting that happened
upstream of it. That contradicted the three-component support this ADR keeps in
scope. It now calls `Parser.IsDottedBodyNumber`, which delegates to
`DottedNumber.TryParse` with `Depth > 1`. The corpus has no three-component
numbering, so no fixture changes.

### One correction the fixtures forced

The first implementation refused to start a run on a division that already had
children, on the theory that this kept I5 simple. `ukdsiem_9780111540145_en`
disproved it: `10.1` is indented and already nested, `10.2` and `10.3` are
flush, and the conservative rule stranded them. Branch heads are now allowed,
with `MaxChildComponent` seeding `lastChild` from children the parse already
nested — so ordering continues across both sources, and a flush `10.1`
arriving after an indented one is caught as a repeat rather than duplicated.

I6 still holds: after a pass the flat candidates are gone, so a second pass
finds no run to start.

### Gating

`LegislativeDocumentConfig.RenestDottedNumbers`, default false, set true in
`ForExplanatoryMemoranda()` and `ForCodesOfPractice()` only. Consumed in
`BaseLegislativeDocumentParser.Parse()` immediately after `Body2()` — before
`StatisticsCalculator` runs in `BaseHelper`, hence §8.

### Tests

`test/leg/TestDottedNumberRenester.cs`, 83 cases across the two classes as of
2026-08-21,
covering every entry in §9's table plus the behaviours that only emerged once
the pass met real documents and real review: mixed indentation under one parent;
the deliberate face-value reading of an adjacent prefix match; I7's two
destinations and its trailing-heading rule; the conversion guards; ordering
seeded from the highest existing child; purity of the input tree; scope-aware
recursion into a paragraph's own children; and the exhaustiveness throw.
