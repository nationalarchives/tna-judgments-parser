# Numbering3 Algorithm Overview

The new walker calculates every numbered paragraph in a single streaming pass over `word/document.xml`.
This note explains the moving pieces inside `Numbering3.CalculateAllNumbers` (see `src/docx/Numbering3.cs`) and
why each bit of state exists.

## 1. Calculating Once Per Document

`NumberingContext` caches numbering results per paragraph (`ConditionalWeakTable<Paragraph, ParagraphState>`).
`CalculateN(...)` simply checks the cache; if the document has not been processed yet we call
`CalculateAllNumbers` once. Every subsequent lookup is O(1).

## 2. Shared Counters Per Abstract List

```
var counters = new Dictionary<int, Dictionary<int, (int value, int numId, string styleId, bool hasExplicitNumId)>>();
var numIdToAbsNumId = new Dictionary<int, int>();
```

For each paragraph we resolve its effective `(numId, ilvl)` (inline `w:numPr`, `LISTNUM`, or paragraph style).
We then map `numId → absNumId` (cached in `numIdToAbsNumId`) and retrieve the counter bucket for that abstract list.
Each bucket stores the current value and the `numId` that produced it for every level in that abstract (`(value, numId)`).
That lets different `numId`s that share the same `abstractNumId` continue one another when Word expects it.

**Legacy analogue.** `Numbering2.CalculateN` replays every prior paragraph, recomputing the same information by
tracking `numIdOfStartOverride`, `prevAbsStarts`, and `prevStarts` dictionaries as it scans backwards (see lines ~780–890).
Those structures infer which abstract owns the current paragraph, but only after re-reading the entire prefix each time.

## 3. Resetting Deeper Levels

Before emitting a new number we reset any levels deeper than the current `ilvl`. Each counter entry remembers the paragraph’s style id and whether it supplied an inline `w:numPr`. When we loop through deeper levels we now ask `ShouldSkipReset(...)`: if both paragraphs rely purely on style numbering and the stored style lies inside the current style’s `BasedOn` chain (e.g., Heading4CL inherits BodyCL), the reset is skipped. Otherwise we remove the counter as usual:

```
var levelsToReset = ilvlCounters.Keys.Where(l => l > ilvl).ToList();
foreach (var l in levelsToReset)
{
    if (ShouldSkipReset(ctx, currentStyle, entry.styleId, hasExplicitNumId, entry.hasExplicitNumId))
        continue;
    ilvlCounters.Remove(l);
}
```

This mirrors Word’s behaviour: whenever a shallower level emits, all deeper levels are discarded so that the next
child list will restart with its configured `<w:start>` or override.

**Legacy analogue.** The backtracking code implicitly resets children because it recomputes every counter from scratch
for the target paragraph, stopping once it reaches the right level. It also contains explicit loops that decrement `count`
whenever it sees deeper levels (e.g., the `prevContainsNumOverrideAtLowerLevel` flags around lines 830–860) plus the
“BasedOn” exception around lines 744‑750 that told it to keep the child counter alive for style-only inheritance. `ShouldSkipReset(...)` is the single-pass equivalent of that exception.

## 4. Caching Parent Values

For compound formats (`%1.%2`, `(a)(i)`…), higher-level values are needed when formatting a single paragraph.
`CalculateAllNumbers` caches every parent level’s counter as soon as we process a paragraph:

```
ctx.SetCachedN(paragraph, parentLevel, parentValue);
```

If a parent counter does not exist yet we seed it from `GetBaseStart(...)` (the intrinsic `<w:start>` value for the
level, ignoring overrides). This keeps formatting calls O(1).

**Legacy analogue.** `Numbering2` calls `Magic2/Magic3` to fetch parent numbers by recursively invoking `CalculateN`
for each level, effectively re-running the walker logic per level per paragraph.

## 5. Start Overrides: Registration and Suppression

Two structures control `<w:startOverride>`:

```
var startOverrideConsumed = new HashSet<(int numId, int ilvl)>();
var overrideConsumedAtLevel = new HashSet<(int absNumId, int ilvl)>();
```

`TryApplyStartOverride(...)` consults `startOverrideConsumed` so each `(numId, ilvl)` consumes its override once.
When an override fires we also add `(absNumId, ilvl)` to `overrideConsumedAtLevel`; `ShouldApplyOverride(...)`
uses that to detect "we just came back from a deeper level that consumed an override" (tests 37 & 87) so parent levels
don't immediately consume their own overrides when returning from a deeper level.

**Legacy analogue.** The old scanner achieves the same thing with the sprawling `numOverrideShouldntApply*` flags,
`numIdOfStartOverride*` variables, and calls to `StartOverrideIsOperative(...)`. Every time it revisits a paragraph it
re-derives whether the override already fired by examining the history of prior paragraphs.

## 6. Tracking Level Ownership

```
var levelOwners = new Dictionary<(int absNumId, int ilvl), int>();
```

Every time we emit a number we record which `numId` now "owns" that `(absNumId, ilvl)`.
`ShouldApplyOverride(...)` takes a `requiresParentOwner` parameter that is `true` only for paragraphs that inherit
numbering purely from styles (`!hasExplicitNumId && styleNumId.HasValue`). When `requiresParentOwner` is true,
`ShouldApplyOverride(...)` checks `levelOwners` for the immediate parent level (`ilvl - 1`). If the parent level is
currently owned by a different `numId`, we suppress the child's override. The parent ownership check is skipped entirely
for paragraphs with explicit inline numbering, allowing them to apply their overrides regardless of what the parent level
is doing. This reproduces Word's behaviour in fixtures like **test46 copy** where an inline numId temporarily borrows
the parent level: style-only children do not restart while that borrowed parent is active.

**Legacy analogue.** The streaming backtracker models ownership indirectly via `prevNumIdOfStyle`, `prevNumIdWithoutStyle`,
and the `numOverrideShouldntApplyToStyle*` switches (lines ~820–880). Whenever the prior paragraph’s style/inline mix
differs from the current one, it blocks the override—achieved by recomputing those flags for every paragraph. This approach struggled specifically with **interleaved lists** (like `test46`), where ownership flips back and forth between `numIds`.

## 7. Choosing the Base Start Value

When we need a default seed (no override, first time we see a level, or returning from a deeper level),
`GetBaseStart(...)` returns the `<w:start>` value defined on the numbering instance; if absent we fall back to
`Numbering2.GetAbstractStart(...)`. This avoids re-reading `<w:lvlOverride>`—only `TryApplyStartOverride(...)`
may apply those. This separates the static **definition** (checking for a `<w:start>` inside a `<w:lvlOverride>`) from the **exception** (checking for `<w:startOverride>`), ensuring the base value is stable before any overrides are applied.

**Legacy analogue.** `Numbering2.GetStart(...)` combines the same inputs but it’s called repeatedly during every
backtracking run because the legacy algorithm recomputes the start for each prior paragraph whenever it walks history.

## 8. Writing the Result

After all checks:

```
ilvlCounters[ilvl] = (newValue, numId);
lastIlvls[absNumId] = ilvl;
levelOwners[(absNumId, ilvl)] = numId;
ctx.SetCachedN(paragraph, ilvl, newValue);
```

`lastIlvls` records the deepest level we just processed so that `ShouldApplyOverride` can detect the “returning from
child override” scenario. Finally the per-paragraph cache holds the computed integers for later formatting.

## Putting It Together

1. Resolve effective `(numId, ilvl)` from inline properties, LISTNUM fields, or style fallbacks.
2. Map to `absNumId` and load the counter bucket.
3. Reset deeper levels and cache parent values.
4. Decide whether to apply a start override:
   - Style-only paragraphs require the parent level to be owned by the same `numId`.
   - Overrides fire once per `(numId, ilvl)` and are suppressed right after a child consumed one.
5. If no override applies, use `GetBaseStart(...)` (or increment the existing counter) to get the next value.
6. Store the new counter, update `lastIlvls`, `levelOwners`, and the per-paragraph cache.

Every paragraph is therefore processed exactly once, and all the tricky Word semantics (style inheritance,
start overrides, shared abstracts) are encoded via these small state dictionaries.
