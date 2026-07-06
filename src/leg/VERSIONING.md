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

`1.0.0` was set when the dual-emission `ukm:Parser` design landed. It is
the baseline for everything downstream; the next leg behaviour change
that ships will be the first bump.
