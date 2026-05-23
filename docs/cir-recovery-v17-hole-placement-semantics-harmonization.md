# CIR-RECOVERY-V17: hole-family placement semantics harmonization

## Purpose
Harmonize `HoleProfileSegment` placement semantics across all hole-family variants so executors consume explicit placement metadata.

## Placement contract
Each segment must declare: `AnchorSide`, `DepthFromAnchor`, `ZMin/ZMax`, `IsThrough`, and `PlacementDiagnostics`.

## Inventory (pre-V17 -> V17 action)
| Variant | Segment | Anchor | DepthFromAnchor | Z span | IsThrough | PlacementDiagnostics | Executor behavior | Action |
|---|---|---|---|---|---|---|---|---|
| Through | core cylinder | missing -> explicit Through | missing -> explicit | missing -> explicit host span | missing -> true | missing -> added | previously inferred | populated + validated |
| Blind | core cylinder | missing -> explicit Top/Bottom | missing -> explicit depth | missing -> explicit tool span | missing -> false | missing -> added | previously inferred | populated + validated |
| Counterbore | relief cylinder | missing -> explicit Top/Bottom | missing -> explicit depth | missing -> explicit relief span | missing -> false | missing -> added | partially inferred | populated + validated |
| Counterbore | through core | missing -> explicit Through | missing -> explicit through length | missing -> explicit host span | missing -> true | missing -> added | inferred | populated + validated |
| Countersink | entry cone | missing -> explicit Top/Bottom | missing -> explicit cone depth | missing -> explicit cone span | missing -> false | missing -> added | inferred | populated + validated |
| Countersink | core cylinder | missing -> explicit Through/entry side (blind) | missing -> explicit | missing -> explicit host span | missing -> explicit | missing -> added | inferred | populated + validated |
| Chamfered-entry | entry cone | missing -> explicit Top/Bottom | missing -> explicit cone depth | missing -> explicit cone span | missing -> false | missing -> added | inferred | populated + validated |
| Chamfered-entry | core cylinder | missing -> explicit Through/entry side (blind) | missing -> explicit | missing -> explicit host span | missing -> explicit | missing -> added | inferred | populated + validated |
| Stepped | large/medium/small tiers | already explicit | already explicit | already explicit | already explicit | normalized to `placement:` breadcrumb style | explicit | preserved contract |

## Executor consumption status
A shared `HoleProfileSegmentPlacementValidator` was added as a compact internal helper for contract checks and future enforcement; current executor routing remains behavior-preserving and still performs its existing bounded per-variant checks.

Remaining hidden inference: primitive construction order and boolean sequencing remain architecture-local by design (non-goal).

## Rules for future LLM authors
1. Every profile segment must declare placement.
2. Executors must prefer explicit placement.
3. Unknown anchor is not executable.
4. Through segments must declare through semantics.
5. Non-through segments must declare entry anchor and z-span.
6. Tests must include placement assertions.

## Non-goals
No new variants, no STEP exporter changes, no public API changes, no generic profile-stack executor.

## Next milestone recommendation
CIR-RECOVERY-V18: centralize segment-role taxonomy and validator-backed role/placement compatibility checks.
