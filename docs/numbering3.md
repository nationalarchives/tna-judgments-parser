# Numbering3 Algorithm Overview

The `Numbering3` algorithm calculates the numbering value for every paragraph in a single, linear streaming pass over the document. Unlike the legacy backtracking approach (which was O(N²)), this implementation uses an O(N) state-machine to track numbering contexts. This document details the internal logic of `Numbering3.CalculateAllNumbers` (see `src/docx/Numbering3.cs`) and explains the purpose of each state container.

## 1. Calculating Once Per Document

Because list numbering depends on the sequential state of previous paragraphs, we calculate values for the entire document in a single pass upon the first request (lazy evaluation).

The `NumberingContext` class caches these results using a `ConditionalWeakTable<Paragraph, ParagraphState>`, ensuring that `CalculateN(...)` is an O(1) lookup for any paragraph after the initial pass.

## 2. Algorithm State

The `CalculateAllNumbers` method maintains the state of the document walk using several collections. These track the current numbering values, hierarchy ownership, and override history:

*   **`counters`**: Stores the current value (`LevelCounter`) for every level of every abstract list.
*   **`numIdToAbsNumId`**: Caches the mapping from an instance `numId` to its abstract definition to avoid repeated lookups.
*   **`startOverrideConsumed`**: Tracks which specific `(numId, ilvl)` pairs have already used their `<w:startOverride>`, ensuring they only fire once (see Section 7).
*   **`overrideConsumedAtLevel`**: Tracks whether an abstract level has recently consumed an override. Used to prevent "double dipping" when returning from deeper levels (see Section 7).
*   **`levelOwners`**: records which `numId` currently "owns" a specific level of an abstract list. This is crucial for handling interleaved lists (see Section 8).
*   **`lastIlvls`**: Tracks the deepest level processed for each abstract list.
*   **`seenNumIds`**: Tracks which `numId`s have been encountered globally, distinguishing fresh list instances from continuations (see Section 11).

## 3. Filtering Paragraphs

Before processing numbering for any paragraph, we skip paragraphs that should not participate in numbering:

- **Deleted paragraphs** (tracked revisions): paragraphs marked as deleted
- **Empty section breaks**: paragraphs that contain only section break properties
- **Merged paragraphs** (see `Paragraphs.IsMergedWithFollowing`): paragraphs merged with the following paragraph
- **Empty vertically merged table cells**: empty paragraphs in table cells that are continuations of vertical merges

The table cell check runs after the deleted/section/merge filters and addresses a Word rendering quirk where vertically merged cells often contain empty continuation paragraphs. These shouldn't affect numbering calculations:

```csharp
var merge = (paragraph.Parent as TableCell)?.TableCellProperties?.VerticalMerge;
if (merge is not null
    && (merge.Val is null || merge.Val == MergedCellValues.Continue)
    && string.IsNullOrEmpty(paragraph.InnerText))
{
    continue; // skip empty paragraphs in vertically merged table cells (test97)
}
```

Word uses `<w:vMerge w:val="restart"/>` to mark the first cell in a vertical merge, and `<w:vMerge w:val="continue"/>` (or `<w:vMerge/>` with no `val` attribute) for continuation cells. We skip continuation cells that are empty because they're purely structural and don't represent logical content that should advance numbering.

**Legacy analogue.** `Numbering2` implemented this same logic (lines 709–718) by explicitly checking `prev.Parent is TableCell` and inspecting `TableCellProperties.VerticalMerge`. This check had been introduced to resolve **test97**, where phantom empty paragraphs in merged cells were incorrectly advancing the numbering count.

## 4. Shared Counters Per Abstract List

```csharp
private readonly record struct LevelCounter(int Value, int NumId, string? StyleId, bool HasExplicitNumId);

var counters = new Dictionary<int, Dictionary<int, LevelCounter>>();
var numIdToAbsNumId = new Dictionary<int, int>();
```

For each paragraph we resolve its effective `(numId, ilvl)` (inline `w:numPr`, `LISTNUM`, or paragraph style).
We then map `numId → absNumId` (cached in `numIdToAbsNumId`) and retrieve the counter bucket for that abstract list.
Each bucket stores a `LevelCounter` for every level in that abstract, holding the current value, the `numId` that produced it, the paragraph's style ID, and whether it had explicit numbering properties. We store these properties to support complex reset logic (see Section 5) and ownership checks (see Section 8).
That lets different `numId`s that share the same `abstractNumId` continue one another when Word expects it.

**Legacy analogue.** `Numbering2` lacked a unified state container. Instead, `CalculateN` rebuilt the state for every paragraph by populating two separate `StartAccumulator` instances: `prevAbsStarts` (for abstract-based values) and `prevStarts` (for instance-based overrides). It then had to reconcile these with the `numIdOfStartOverride` variable (lines ~780–890) to determine which value applied. `Numbering3` simplifies this by storing the definitive "current state" directly in `counters`.

## 5. Resetting Deeper Levels

Before emitting a new number we reset any levels deeper than the current `ilvl`. Each counter entry remembers the paragraph’s style id and whether it supplied an inline `w:numPr`. When we loop through deeper levels we now ask `ShouldSkipReset(...)`: if both paragraphs rely purely on style numbering and the stored style lies inside the current style’s `BasedOn` chain (e.g., Heading4CL inherits BodyCL), the reset is skipped. Otherwise we remove the counter as usual. Root-level parents (ilvl = 0) never skip the reset, matching the legacy guard `prevIlvl > 0`—see the comment “`// prevIlvl > 0 needed for test76`” in `Numbering2.cs`.

```csharp
var levelsToReset = ilvlCounters.Keys.Where(l => l > ilvl).ToList();
foreach (var l in levelsToReset)
{
    LevelCounter counter = ilvlCounters[l];
    if (ShouldSkipReset(ctx, style, counter.StyleId, hasExplicitNumId, counter.HasExplicitNumId))
        continue;
    // Also skip reset if the level explicitly declares it never restarts (see Section 14)
    if (LevelNeverRestarts(ctx.Main, absNumId, l))
        continue;
    ilvlCounters.Remove(l);
}
```

This mirrors Word’s behaviour: whenever a shallower level emits, all deeper levels are discarded so that the next
child list will restart with its configured `<w:start>` or override.

**Legacy analogue.** The backtracking code implicitly reset children because it recomputed every counter from scratch for the target paragraph, stopping once it reached the right level. It also contained explicit loops that decremented `count` whenever it saw deeper levels (e.g., the `prevContainsNumOverrideAtLowerLevel` flags around lines 830–860) plus the “BasedOn” exception around lines 744‑750 that told it to keep the child counter alive for style-only inheritance. That exemption required `prevIlvl > 0`; otherwise the reset happened unconditionally—exactly the behavior we enforce here so root-level sections like **test76** reset their children.

## 6. Caching Parent Values

For compound formats (`%1.%2`, `(a)(i)`…), higher-level values are needed when formatting a single paragraph.
`CalculateAllNumbers` caches every parent level’s counter as soon as we process a paragraph:

```csharp
ctx.SetCachedN(paragraph, parentLevel, parentValue);
```

If a parent counter does not exist yet we seed it from `GetBaseStart(...)` (the intrinsic `<w:start>` value for the
level, ignoring overrides). This keeps formatting calls O(1).

**Legacy analogue.** `Numbering2` called `Magic2/Magic3` to fetch parent numbers by recursively invoking `CalculateN` for each level, effectively re-running the walker logic per level per paragraph.

## 7. Start Overrides: Registration and Suppression

Two structures control `<w:startOverride>`:

```csharp
var startOverrideConsumed = new HashSet<(int numId, int ilvl)>();
var overrideConsumedAtLevel = new HashSet<(int absNumId, int ilvl)>();
```

`TryApplyStartOverride(...)` consults `startOverrideConsumed` so each `(numId, ilvl)` consumes its override once.
When an override fires we also add `(absNumId, ilvl)` to `overrideConsumedAtLevel`; `ShouldApplyOverride(...)`
uses that to detect "we just came back from a deeper level that consumed an override" (tests 37 & 87) so parent levels
don't immediately consume their own overrides when returning from a deeper level.

**Legacy analogue.** The old scanner achieved the same thing with the sprawling `numOverrideShouldntApply*` flags, `numIdOfStartOverride*` variables, and calls to `StartOverrideIsOperative(...)`. Every time it revisited a paragraph it re-derived whether the override already fired by examining the history of prior paragraphs.

## 8. Tracking Level Ownership

```csharp
var levelOwners = new Dictionary<(int absNumId, int ilvl), int>();
```

Every time we emit a number we record which `numId` now "owns" that `(absNumId, ilvl)`.
`ShouldApplyOverride(...)` takes a `requiresParentOwner` parameter that is `true` only for paragraphs that inherit
numbering purely from styles (`!hasExplicitNumId && styleNumId.HasValue`). When `requiresParentOwner` is true,
`ShouldApplyOverride(...)` checks `levelOwners` for the immediate parent level (`ilvl - 1`). If the parent level is
currently owned by a different `numId`, we suppress the child's override. The parent ownership check is skipped entirely
for paragraphs with explicit inline numbering, allowing them to apply their overrides regardless of what the parent level
is doing. This reproduces Word's behaviour in fixtures like **test46** where an inline numId temporarily borrows
the parent level: style-only children do not restart while that borrowed parent is active.

**Legacy analogue.** The streaming backtracker modeled ownership indirectly via `prevNumIdOfStyle`, `prevNumIdWithoutStyle`, and the `numOverrideShouldntApplyToStyle*` switches (lines ~820–880). Whenever the prior paragraph’s style/inline mix differed from the current one, it blocked the override—achieved by recomputing those flags for every paragraph. This approach struggled specifically with **interleaved lists** (like `test46`), where ownership flips back and forth between `numIds`.

## 9. Choosing the Base Start Value

When we need a default seed (no override, first time we see a level, or returning from a deeper level),
`GetBaseStart(...)` determines the intrinsic start value. It first checks the numbering instance for a `<w:start>` value (which might be inside a `<w:lvl>` element nested within a `<w:lvlOverride>`), and falls back to the abstract definition (`Numbering2.GetAbstractStart`) if none is found.

Crucially, `GetBaseStart` **ignores** `<w:startOverride>` elements. This enforces a strict separation of concerns: `GetBaseStart` retrieves the static **definition** of the list, while `TryApplyStartOverride` handles the one-time **exception**. This prevents the "leakage" of override values into the base count seen in previous attempts.

**Legacy analogue.** `Numbering2` conflated these concepts. It calculated the start value (lines 882–887) inside the main loop, often letting an active override (`numIdOfStartOverride`) obscure the underlying base start value. `Numbering3` isolates the base start calculation, making it immutable for a given `(numId, ilvl)`.

## 10. Writing the Result

Once the `newValue` is determined, we commit the state for this paragraph. This updates the shared history for the abstract list so subsequent paragraphs can build upon it:

```csharp
ilvlCounters[ilvl] = new LevelCounter(newValue, numId, styleId, hasExplicitNumId);
lastIlvls[absNumId] = ilvl;
levelOwners[(absNumId, ilvl)] = numId;
ctx.SetCachedN(paragraph, ilvl, newValue);
```

*   `ilvlCounters`: Updates the running count for this level.
*   `lastIlvls`: Records that this `ilvl` was the most recent active level, enabling the "returning from deeper" checks in Section 7.
*   `levelOwners`: Claims ownership of this level for the current `numId`.
*   `ctx.SetCachedN`: Memoizes the final integer so future lookups are O(1).

**Legacy analogue.** `Numbering2` was stateless. It performed the calculation, returned the integer, and discarded all intermediate context. This forced the next paragraph to reconstruct the entire history from scratch. `Numbering3` treats the calculation as a state transition, persisting the context for O(1) access by the next paragraph.

## 11. Handling Interleaved Lists and Continuations

A critical challenge in Word numbering is distinguishing between a **fresh list instance** (which should apply its `startOverride`) and a **continuation** of an existing list (which should respect suppression if it was interrupted by a parent level).

To solve this, we track globally seen `numId`s:

```csharp
var seenNumIds = new HashSet<int>();
```

Inside the main loop, we determine if a `numId` is being encountered for the first time:

```csharp
bool isGloballyNew = seenNumIds.Add(numId);
```

This `isGloballyNew` flag is passed to `ShouldApplyOverride`. The logic for bypassing the "just came from deeper" suppression check is:

```csharp
bool hasExplicit = HasExplicitOverride(ctx.Main, numId, ilvl);
bool justCameFromDeeperWithOverride = (!isGloballyNew || !hasExplicit) && ...
```

*   **Fresh List (`isGloballyNew = true`)**: If the list is new AND has an explicit override (e.g., **test66**), we treat it as a new start. The suppression check is bypassed, and the override is applied.
*   **Continuation (`isGloballyNew = false`)**: If the list has been seen before (e.g., **test37**), it is a continuation. We respect the `justCameFromDeeperWithOverride` check, ensuring that if the list was merely interrupted by a parent level, it doesn't incorrectly consume a new override upon returning.

**Legacy analogue.** The legacy code implicitly handled this by re-scanning the entire document prefix for every paragraph (e.g., iterating through `allPrev`). It could "see" if the `numId` had appeared before through this repeated O(N) iteration. In contrast, `Numbering3` makes this check explicit and direct, using the `seenNumIds` set for an O(1) lookup.

## 12. Returning from Deeper Levels with Start Overrides

When processing a paragraph at a shallower level after visiting deeper levels (e.g., returning from ilvl=1 to ilvl=0), a critical question arises: should we increment from the base start value, or use the start override directly?

**The Rule**: If a `startOverride` applies at this level, use it **as-is** without adding +1, even when returning from a deeper level:

```csharp
if (ShouldApplyOverride(..., out int overrideValue))
{
    // Override applies, use it as-is (don't add +1 even if returning from deeper)
    newValue = overrideValue;
}
else if (returningFromDeeper)
{
    // Returning from deeper level without override: increment from base
    newValue = GetBaseStart(ctx, absNumId, numId, ilvl) + 1;
}
```

**Example (test68)**: Consider a document with three numIds (8, 11, 16) sharing abstractNumId=7:
- Para 0: `numId=11, ilvl=1` displays as `1.2` (parent cached as 1, child uses startOverride=2)
- Para 1: `numId=16, ilvl=0` displays as `5` (startOverride=5 applied directly, NOT 5+1=6)
- Para 2: `numId=11, ilvl=1` displays as `5.1` (parent=5 from counter, child starts fresh)
- Para 3: `numId=8, ilvl=1` displays as `5.2` (parent=5, child increments)

When para 1 is processed at ilvl=0, we're returning from ilvl=1. The `startOverride=5` exists and hasn't been consumed for this `(numId=16, ilvl=0)` pair. The override is applied directly as `5`, not incremented to `6`. This ensures that explicit restart values take precedence over continuation logic.

**Why this matters**: Without this rule, returning from a deeper level would always add +1 to the base start, causing paragraphs with explicit `startOverride` values to display incorrectly. Word treats overrides as absolute restart points that supersede any continuation or increment logic.

**Legacy analogue.** `Numbering2` had difficulty with this exact scenario, often due to the interaction of `prevContainsNumOverrideAtLowerLevel` and `numIdOfStartOverride`. Its logic (lines ~800-820, ~860-880) sometimes incorrectly applied the `+1` increment even when an explicit `startOverride` was present for a newly returned-to level, particularly when `numIdOfStartOverride` was cleared or incorrectly set by an intervening deeper level. The `Numbering3` explicit check for `ShouldApplyOverride` before any increment ensures this behavior is always correct.

## 13. Abstract Start vs Level Start After Reset

When a counter doesn't exist at a level and no override applies, we must choose between two potential start values:

1. **`GetBaseStart(...)`**: Returns the `<w:start>` value from the `<w:lvl>` element inside `<w:lvlOverride>`, which often matches the `<w:startOverride>` value
2. **`Numbering2.GetAbstractStart(...)`**: Returns the `<w:start>` value from the abstract numbering definition

**The Rule**: When starting fresh without an override (typically after a counter has been reset), use the **abstract start**, not the Level start:

```csharp
else
{
    // Fresh start at this level - use abstract start, not Level start,
    // since any startOverride has already been consumed
    newValue = Numbering2.GetAbstractStart(ctx.Main, absNumId, ilvl);
}
```

**Example (test68 continued)**: When para 2 (`numId=11, ilvl=1`) is processed:
- The ilvl=1 counter was reset when para 1 (ilvl=0) was processed
- No counter exists at ilvl=1, so we enter the `else` branch
- `isGloballyNew = false` (we saw numId=11 in para 0)
- `ShouldApplyOverride` returns false (the startOverride=2 was already consumed by para 0)
- `returningFromDeeper = false` (we're going deeper from ilvl=0 to ilvl=1, not returning)
- We use `Numbering2.GetAbstractStart(main, 7, 1)` which returns `1`
- Para 2 displays as `5.1`, not `5.2`

**Why this matters**: The `<w:lvl>` elements inside `<w:lvlOverride>` often have `<w:start>` values that match the `<w:startOverride>`. For example, numId=11 has:
```xml
<w:lvlOverride w:ilvl="1">
  <w:startOverride w:val="2"/>
  <w:lvl w:ilvl="1">
    <w:start w:val="2"/>
    ...
  </w:lvl>
</w:lvlOverride>
```

If we used `GetBaseStart(...)` after the override was consumed, we'd get `2` from the `<w:lvl>/<w:start>` element, causing para 2 to display as `5.2` instead of the correct `5.1`. The abstract numbering definition (abstractNumId=7) has the true base start value of `1`, which is what should be used when restarting the counter after a reset.

**Legacy analogue.** `Numbering2`'s `GetStart(...)` method (used in its `CalculateN`) inherently combined override logic with base start logic. This meant that after an override had fired and its effect was technically "consumed" (as tracked by `numIdOfStartOverride`), calling `GetStart(...)` again for a "fresh start" could still inadvertently retrieve the last override value (if present in `<w:lvl>/<w:start>`) instead of the true abstract base (from `Numbering2.GetAbstractStart(...)`). `Numbering3`'s strict separation ensures that `Numbering2.GetAbstractStart(...)` is correctly used in these "fresh start" scenarios, preventing overrides from polluting the base.

## 14. Levels That Never Restart

Word allows a level to declare `<w:lvlRestart w:val="0"/>`, which means "never restart this level's counter," even when returning from a deeper level. We honor that by inspecting the abstract numbering definition before resetting. If the abstract level at `(absNumId, ilvl)` has `lvlRestart="0"`, we leave the stored counter intact. This surfaces in **test94**, where the child list should flow straight through root interruptions.

We check this before removing any counter during the reset loop:

```csharp
// If lvlRestart=0, this level should never restart (test94)
if (LevelNeverRestarts(ctx.Main, absNumId, l))
    continue;
ilvlCounters.Remove(l);
```

At present we only check the abstract definition; we haven't yet seen a case where a numbering instance overrides `lvlRestart`, but it's something we may need to consider if a future fixture relies on it.

**Legacy analogue.** `Numbering2` handled this implicitly because its backtracking algorithm recomputed the counter state for each paragraph from scratch. A level with `lvlRestart="0"` would naturally carry its counter forward *if* its conditions for continuation were met during the historical replay, as the re-computation simply wouldn't apply a reset. `Numbering3` explicitly preserves the stored counter to achieve the same effect in a single pass.

## 15. Handling Named LISTNUM Fields

Word LISTNUM fields can be “named” sequences (e.g., `LISTNUM "SEQ1" \l 2 \s 5`) that don’t carry `w:numPr`. We detect such fields in the paragraph’s runs and set the counter for the referenced abstract list:

- Resolve the abstract numbering by name via `Numbering.GetAbstractNum(main, name)`.
- Use the field's `\l` level (Word's 1-based converted to 0-based) and optional `\s` start value (default 1).
- Store the start value in the counter bucket and cache it for the paragraph. (The counter's `NumId` is set to `-1` to mark it as field-generated rather than from paragraph properties.)

**Current limitations (intentional for now):**
- Each named LISTNUM sets the level to its `\s` value (or 1); consecutive paragraphs with the same named LISTNUM don't auto-increment—each resets to its declared start.
- Parent levels aren't seeded or reset here; mixed named LISTNUM and normal paragraphs may need refinement.
- `\r` (reset) and custom abstract starts in the named sequence aren't yet honored.

There is no legacy analogue in `Numbering2`; the legacy code assumed LISTNUM would be converted upstream.

## 16. Putting It Together

1. Resolve effective `(numId, ilvl)` from inline properties, LISTNUM fields, or style fallbacks.
2. Map to `absNumId`, load the counter bucket, and check if `numId` is globally new.
3. Reset deeper levels and cache parent values.
4. Decide whether to apply a start override:
   - Style-only paragraphs require the parent level to be owned by the same `numId`.
   - Overrides fire once per `(numId, ilvl)`.
   - **Continuations** are suppressed if returning from a deeper level, but **fresh lists** (new `numId` with explicit override) bypass this suppression.
5. If no override applies, use `GetBaseStart(...)` (or increment the existing counter) to get the next value.
6. Store the new counter, update `lastIlvls`, `levelOwners`, and the per-paragraph cache.

Every paragraph is therefore processed exactly once, and all the tricky Word semantics (style inheritance,
start overrides, shared abstracts) are encoded via these small state dictionaries.

**Legacy analogue.** `Numbering2` effectively "put it together" by replaying the entire document prefix for every paragraph, recalculating all these steps and re-deriving context repeatedly. This made it inherently O(N²) and prone to subtle state interaction bugs. `Numbering3` achieves the same complex Word semantics through a single O(N) pass, relying on explicit state management rather than implicit historical re-derivation.
