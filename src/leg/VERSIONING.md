# Leg parser versioning

The leg parser version is `LegVersion.Current` (`src/leg/LegVersion.cs`),
emitted as `<ukm:Parser Name="legislation" Value="…"/>` on every leg AKN.
Downstream consumers (legislation.gov.uk pipelines, regression triage)
read this to identify which leg parser produced a given AKN.

## Scheme

SemVer, scoped to AKN output impact:

| Component | Bump when… |
|---|---|
| **MAJOR** | The AKN's structure or emission contracts change in a way that breaks a downstream consumer. Rare; needs coordination with CLML / legislation.gov.uk. |
| **MINOR** | New leg doc-type support, new metadata fields, new emission behaviour that consumers should notice but doesn't break them. |
| **PATCH** | Bug fix that changes parser output without changing the contract — e.g. a previously-dropped tab marker now preserved, a heading classified correctly that was previously mis-classified. |

A refactor that produces byte-identical AKN output (verified by fixture
regen showing no diff) **does not** bump the version. Likewise, changes
that only affect leg-internal code paths (HTML rendering, CLI, build
config) don't bump.

## Who bumps

The PR author. If your PR changes leg parser output, bump `LegVersion.Current`
in the same PR — at the severity that matches your change. Reviewers
check the bump matches the change. Forgetting to bump is a review comment,
not a CI failure.

## Independence from core

`LegVersion` is separate from the core parser version (`version.targets`
`VersionPrefix`, emitted as `<ukm:Parser Name="core" Value="…"/>`). They
drift independently: a release that only touches shared judgment code
bumps core but not leg, and vice versa. When both layers change in the
same release, both bump.

## Starting point

`1.0.0` was set when the dual-emission `ukm:Parser` design landed
(2026-05-20), in anticipation of a first release that did not happen:
the release was blocked by the flush-left dotted-numbering defect that
`1.1.0` fixes. No AKN produced by `1.0.0` was ever published or exposed
to an external consumer.

Internal artifacts do exist. Batch runs from June and July 2026 wrote
AKN to the transfer bucket after dual emission had landed, so those
objects carry `Name="legislation" Value="1.0.0"`. That is precisely why
the version distinction matters: `ukm:Parser` is what triage reads to
tell pre-fix objects from post-fix ones, and without a bump they are
indistinguishable.

`1.1.0` is therefore the first bump, and — if release proceeds from
here — the first version whose output is public.

**Why MINOR and not MAJOR.** `1.1.0` rebuilds paragraph hierarchy from
dotted numbering, which renumbers `paragraph_N` eIds: they are assigned
by TOC position (`TocGenerator.EmitTocEntry`), so collapsing a flat run
into a hierarchy both drops ids and repoints the survivors at different
content. Against a published corpus that is squarely MAJOR. Against no
external consumers it breaks nothing, and the MAJOR row is conditioned
on breaking one.

The corollary is that identifier stability starts at `1.1.0`, not
before. After first release, removing or reassigning a published eId is
a breaking change and bumps MAJOR. Adding an eId to content that had
none is additive and does not.
