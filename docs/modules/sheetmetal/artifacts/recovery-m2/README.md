# Sheet Metal Recovery M2 — topology-guided source-flat stitching

## Verdict

M2 is **meaningful progression**. CTC-03 now produces a valid, single analytic
source blank with all 17 openings and seven bend lines. Native CTC remains
`NeedsReview`: an attempted ordinary edge-fragment reconstruction proved that
the current authored adapter conflates the right-wall free-edge carrier with
the partial child-flange attachment edge. Those source edges are offset by
2.6416 mm, and the R12.7 end transitions consume both adjacent edges. Keeping
the approximation disconnected the native blank, so it was removed.

## Recovery firewall and data flow

```text
strict imported line/arc regions
  -> strict material classification and analytic splitting
  -> recovery-only retained-fragment graph
  -> strict-key nodes (1e-5 mm import bucket)
  -> same-source complementary dangling repair (0.02 mm maximum policy)
  -> bounded micro-closure removal / pairing candidates
  -> one connected, non-self-intersecting PlanarContour2
```

`RecoveryContourStitcher` is called only when the strict
`ProfileArrangementBuilder` rejects a recovered-source composition. Native
Firmament compilation, authored Profiles, and `PlanarContourKernel` tolerances
are unchanged. The recovered contour is validated by the strict kernel after
repair.

## CTC junction diagnosis

All three failures were analytic split remnants at a point tangency, not
missing engineering spans. The old arrangement saw each remnant as an extra
incoming and outgoing edge and rejected 2-in/2-out angular order.

| junction | source segments | recovered point (mm) | remnant length (mm) | relation and new action |
|---|---|---:|---:|---|
| J0 | `region-p-0088-0089.source-e0049`, `e0052` | (177.800000001, -97.237930176) | 0.000003020708 | line/arc point tangent; collapse the one-fragment micro-closure and continue |
| J1 | `region-p-0088-0089.source-e0052`, `e0055` | (63.499998007, -97.237930176) | 0.000001992971 | complementary line/arc point tangent; same bounded repair |
| J2 | `region-p-0109-0111.source-e0326`, `e0334` | (348.831502633, 130.962397483) | 0.000002517547 | R12.7 arc/line point tangent; same bounded repair |

The corresponding micro-loop areas are 0.000146864, 0.000096896, and
0.000439100 mm². Each repair is `PointTangentContinuation`, confidence
`Strong`, and retains the original endpoint list, tangent evidence, source
segment IDs, region IDs, canonical point, and displacement.

Implemented taxonomy is `ExactEndpointMatch`,
`WithinToleranceEndpointMatch`, `PointTangentContinuation`,
`TangentContinuation`, `AngularContinuation`, `Ambiguous`, and `Rejected`.
Geometric relation is recorded separately from the repair action.

## Tolerance and acceptance policy

- Exact analytic/native contour validation: 1e-7 mm (unchanged).
- Imported topology vertex bucket: 1e-5 mm, matching strict arrangement graph
  identity and signed-zero normalization.
- Recovery junction tolerance: 0.02 mm.
- Absolute recovery repair ceiling: 0.05 mm.

The wider tolerance can only combine complementary dangling endpoints from the
same source profile. Already balanced nodes are never proximity-merged, and
cross-region proximity alone is never authority. Acceptance requires one
all-fragment closed traversal followed by strict winding, endpoint, and
self-intersection validation. Missing spans or displacement over the ceiling
remain failures.

`JudgmentEngine` is **partially applicable**. Deterministic micro-closure and
single-candidate reductions do not need scoring. When a remaining recovery
node has two legal incoming/outgoing pairings, the stitcher enumerates only
that bounded topology choice (maximum 256 global combinations), applies hard
single-blank/all-fragment constraints, then scores source-profile continuity,
source-operation continuity, and tangent preservation. It does not enumerate
arbitrary contour topology.

## Accepted CTC source flat

| property | result |
|---|---:|
| status | `Valid` / `RecoveredWithRepairs` |
| outer loops / segments | 1 / 81 |
| inner loops | 17 (15 circles, 2 slots) |
| bend lines | 7 |
| logical repaired junctions / unresolved | 3 / 0 |
| bounds | 404.754237721 × 625.305580354 mm |
| deterministic hash | `63d207b9c54fe569c38c63bd2a65d5024929b31b115f5926007fd3f7797eb541` |

Artifacts are generated under `artifacts/sheetmetal-recovery-m2/ctc03`:
`recovered-flat.json`, `recovered-flat.svg`, `recognition-plan.json`,
`recover-report.json`, `compare-flat.json`, and `compare-formed.json`.

One observed run measured 2.97 ms endpoint clustering, 1.95 ms bounded
selection, 10.48 ms strict contour validation, and 50.03 ms total stitching.
Timings are observational and excluded from the hash.

## Right-wall proof and native blocker

The accepted source planar region proves this ordered profile:

1. R12.7 end transition;
2. 25.89022 mm baseline run;
3. 15.24 × 15.24 diagonal into a 15.24 mm recess;
4. 63.5 mm recessed run;
5. vertical return to the shallow service attachment level;
6. 127 mm service span, offset 2.6416 mm from the outer baseline;
7. mirrored 63.5 mm recess and 15.24 mm diagonal;
8. 29.69514 mm baseline run and the second R12.7 end transition.

No persistent edge-state machine is justified. The clean native abstraction is
an ordinary analytic right-wall profile plus a separately addressable inset
attachment path for the partial flange. The present authored lowering exposes
only `RightWall.Outer` for both roles and its region-boundary adapter is
line-only. A trial compound-recess fragment made the service flange an island;
it was reverted rather than hiding the contradiction with fuzzy union.

Consequently native source remains source-independent and unchanged. There are
no STEP paths, recovered topology IDs, or inline recovered polygons in
`ctc03-final.firmament`.

## Comparison evidence

The new accepted global contour changes the comparison authority. It should not
be presented as a like-for-like improvement to M1's per-region fallback.

| direct flat metric | M1 partial-region reference | M2 valid global reference |
|---|---:|---:|
| source -> native RMS / p95 / max (mm) | 4.069438 / 7.836135 / 16.027648 | 4.413550 / 9.554765 / 17.448908 |
| native -> source RMS / p95 / max (mm) | 12.500820 / 17.316571 / 156.843630 | 3.769455 / 8.898990 / 14.341517 |
| width / height residual (mm) | 0.002447 / 0.007820 | 0.002447 / 0.007820 |
| cuts / bend-line delta | 0 / 0 | 0 / 0 |

The major reverse-direction outlier disappears because M2 compares one real
outer boundary rather than a union of all region boundaries. Source-to-native
does not yet improve; the native right-wall profile remains the blocker.

No refold update is claimed. With native source unchanged, formed parity remains
8.500190 / 12.478269 / 52.816066 mm source-to-native RMS/p95/max and
3.607855 / 8.940881 / 12.735724 mm native-to-source. All 17 opening comparisons
and seven bends remain preserved.

## Generic robustness

`RecoveredContourStitchingM2Tests` includes:

- a same-source square with a 1e-6 mm endpoint gap, recovered by one explicit
  `WithinToleranceEndpointMatch` without global merging;
- two same-center/radius complementary semicircles, retained as distinct
  analytic domains rather than tessellated or deduplicated;
- an exported dumb U-channel STEP with signed-zero numeric spelling, reimported,
  recognized, and recovered without access to native construction authority.

## Workflow and remaining friction

```text
aetheris sheetmetal recognize part.step --out plan.json
aetheris sheetmetal recover-flat part.step --plan plan.json --out-dir recovered
# inspect recovered-flat.svg and compact repair evidence
aetheris sheetmetal compare-flat recovered/recovered-flat.json native.firmament
# only after flat parity: refold and compare formed geometry
```

The flat is now trustworthy enough to read the right wall as ordinary 2D
engineering geometry rather than infer it from formed faces. The unavoidable
friction is no longer recovery: it is a missing strict authored distinction
between a free-edge profile carrier and an inset child-flange attachment path,
plus analytic round-corner propagation through the formed-region adapter.

The recovery implementation is an acceptable bounded prototype: isolated,
deterministic, diagnostic-rich, and intentionally not generalized into a
Boolean engine. The largest remaining blocker for arbitrary vendor Sheet Metal
STEP is reliable recognition/topology evidence when the source has genuinely
missing edges, non-developable patches, or multi-blank material—not micron-scale
endpoint noise.
