# FIRMAMENT-PIPELINE-X2 — pipeline surface coherence and Segment retirement

Date: 2026-09-22

## Executive verdict

**Accepted.** The Firmament V2 Profile pipeline is now a coherent superset of ordinary canonical Profile authoring. Low-level `Segment` remains supported for compatibility, invalid-case coverage, and deliberate parity/identity witnesses; no unexplained production-style canonical or regression fixture requires it.

## Surface contract

### Sub-spans

The admitted bounded form is `Guide To EndPoint`. In a chain, the current endpoint is the implicit start. The opening stage may say `Guide From StartPoint To EndPoint`; `Guide To EndPoint` uses the guide's natural start when that is unambiguous. Both boundaries are named points, and selection is confined to the referenced guide.

Line and circular-arc carriers are supported. A full-circle sub-span is counter-clockwise by default; `Reverse` prefers the clockwise traversal. This removes the pipeline need for low-level `Sweep`. Segment identity remains the inherited guide leaf or explicit `As` alias, and provenance retains the source guide, orientation, pipeline index, and source range.

Migrated fixtures:

- `Regression/CanonicalGeometry/profile-compose-l-bracket.firmament`
- `Regression/CanonicalGeometry/profile-compose-l-bracket-external-modify-counterbore.firmament`
- `Regression/CanonicalGeometry/profile-compose-reflex-chamfer-with-counterbore.firmament`
- `Regression/CanonicalGeometry/profile-compose-reflex-chamfer-with-shaft.firmament`
- `Canonical/PMI/counterbore-shaft-diameter.firmament`
- `Canonical/Revolve/sphere.firmament`
- `Regression/Profile/valid/construction-plane-positive-x.firmament`

The construction-plane fixture also proves that recognized declarations may precede one implicit-loop pipeline expression. A second competing pipeline is rejected.

### Naming

Segment identity is Profile-wide and never silently loop-qualified. Manual and pipeline authoring therefore use the same rule. A repeated name across loops reports:

`firmament-profile-segment-identity-collision:<name>:loops=<loops>:use As alias or rename source guide/stage`

The L-bracket and sphere migrations use `As` to preserve every prior canonical segment name. Four pre-existing annular pipeline sources required an intentional collision migration: their inner full-circle identity changed from the old automatic `Inner.Boundary` to explicit `InnerBoundary` in `Canonical/Assembly/annular-pair.firmament` and the three annular definitions in `DifferenceEngine/parts/Storage.firmament` and `Showcase.firmament`. Repository search found no selector depending on `Inner.Boundary`; assembly/display and Difference Engine regressions pass with the explicit alias.

### TraceLoop

`TraceLoop` is admitted for the closed ordered families supplied by the existing closed-boundary lowering: `Rect2`, `Square2`, `RoundedRect2`, `Polygon2`, `RegularPolygon2`, closed `Concept Path`, `Circle2`, and `Ellipse2` (plus the already supported related closed-boundary families). Linear/path families retain their named edge identities. Full circles and ellipses retain the established single `Boundary` identity; an `As` alias may disambiguate that identity across loops.

The identity-sensitive quadrant decomposition in `Regression/ProfileComposition/mixed-line-arc-additive-overlap.firmament` was intentionally not collapsed to `TraceLoop`.

### Reverse and diagnostics

On the opening stage, `Reverse` selects the initial direction. Mid-chain it is a preference: reversed orientation is tried first, then the ordinary connectable orientation. If neither connects, the normal disconnected-pipeline diagnostic is emitted.

Unknown references now report `firmament-pipeline-unknown-guide:<name>`. `firmament-pipeline-stage-type:<kind>` is reserved for actual stage-kind/position misuse. Unknown or off-guide sub-span endpoints have distinct deterministic diagnostics.

## Corpus audit

The audited `Canonical` and `Regression` trees contain 353 `.firmament` files (219 canonical, 134 regression). X2 rewrote 7 fixtures; together with the preceding X1 rewrite, 46 fixtures have moved from ordinary low-level authoring to the pipeline.

The full `fixtures/` scan finds 193 `Segment` declarations in 23 files:

| Category | Files | Explanation |
|---|---:|---|
| Compatibility | 3 | Legacy V1 reconstruction witnesses |
| Invalid | 14 | Negative diagnostics and failure geometry |
| Low-level parity / identity witnesses | 6 | Manual parity, explicitly named low-level fixtures, and the quadrant-identity mixed arc witness |
| Unexplained production-style usage | **0** | Target met |

No compatibility or unrelated invalid fixture was modernized.

## Validation

- Focused pipeline tests: 36/36 passed. These include line and arc sub-spans, opening and mid-chain selection, aliases, invalid endpoints, disconnected stages, inline declarations, ambiguous second pipelines, naming collisions, TraceLoop families, Reverse fallback, exact diagnostics, and manual/pipeline canonical-dump equality for the L-bracket and sphere.
- Firmament tests: 1,567/1,567 passed, serial Release.
- Kernel.Core: 1,078/1,080 passed in the full run; the two display-corpus failures both passed in isolated reruns (16/16 orientation corpus and 18/18 NIST display corpus). One failure was the known 5-second load-sensitive STEP display timeout; no timeout was changed.
- Server tests: 59/59 passed.
- Full solution: `dotnet build Aetheris.slnx -c Release --no-restore -m:1` succeeded with 0 errors; only existing WebAssembly/trimming warnings were reported.
- Canonical qualification: every configured action passed, including the migrated PMI and sphere fixtures. The script ended only on the pre-existing source-policy guard `Assembly/annular-pair.firmament: legacy explicit assembly placement`; the guard was not weakened.
- Artifact truth: the pipeline sphere exported and reimported as one enclosed analytic spherical face with bounds `[-20,20]` on X/Y/Z. The pipeline L-bracket exported and reimported as an enclosed 8-face/18-edge/12-vertex solid with bounds `[-40,-20,0]..[40,40,12]` and exact asserted volume `28,800 mm^3`.
- Migration oracle: permanent tests compare frame, loop role/order, segment names, and analytic geometry for manual versus pipeline L-bracket and sphere sources; the canonical dumps are equal.
- Document routing: all seven X2 fixtures validate through the production CLI with no diagnostics. The standalone construction-plane Profile now has an explicit bounded V2 document route instead of falling into the unrelated historical edge-finish parser.
- Fresh-author checks using only `docs/public/firmament/syntax.md`: L-bracket, inline `Rect2 |> TraceLoop`, collision repair with `As`, and half-circle sphere revolve all selected the preferred pipeline syntax. The first sphere attempt exposed an incomplete documentation example; the guide was corrected and the repeated fresh-author check produced the valid canonical form without `Segment`/`Sweep`.

Generated STEP evidence is under ignored `artifacts/local/firmament-pipeline-x2/`.

## Scope

No conditionals, loops, recursion, macros, arbitrary slicing, new geometry, second parser/IR/executor, or legacy Segment removal was introduced. Geometry continues to lower through the existing Profile, revolve, BRep, tessellation, and STEP authorities. The existing single-owner face-orientation fix and gear-volume regression remain green in the Firmament suite.
