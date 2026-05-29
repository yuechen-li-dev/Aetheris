# EDGE-PRISMATIC-X4 — Coplanar split/merge policy audit

## 1. Executive summary

The current `PrismaticSectionTransitionEmitter` preserves section-boundary split faces. In a stacked-section body, every adjacent section pair owns its interval and emits its own transition faces, even when an interval is geometrically unchanged from the interval below or above.

Some adjacent faces in those outputs are coplanar and could theoretically be merged into fewer larger faces. That is especially visible in the top-edge chamfer route, where unchanged rectangular sides remain planar across the lower stable prism interval and the upper transition interval.

This audit decides the current policy: **preserve splits by default** because they encode construction intent, section-stack evidence, and transition intervals. Coplanar merging is an optimization and must not be treated as the default emission behavior.

Merging may become a future optional simplification pass, but only when downstream recognizers, diagnostics, route contracts, corpus artifacts, and STEP smoke agree that the simplified topology preserves required semantics.

## 2. Why this audit exists

EDGE-PRISMATIC-X2/V2 expresses the controlled top `+X` horizontal edge chamfer as three Z-stacked sections:

1. a full rectangle at `z0 = 0`;
2. the same full rectangle at `z1 = height - chamferDistance`;
3. an inset rectangle at `z2 = height`.

The interval `z0..z1` is a stable lower prism. The interval `z1..z2` is the transition interval that contains the chamfer face on the changed `+X` side. The other sides are unchanged or partly unchanged in XY, so their lower stable side faces and upper interval side faces may lie on the same geometric plane.

Those coplanar faces remain split at `z1` today. That split affects:

- topology counts used by labs and tests;
- topology summary parity when comparing emitters and routes;
- feature-recognition and analyzer assumptions about face counts, face ordering, adjacency, and surface families;
- STEP entity shape and downstream consumer observations;
- future production claims about what the prismatic lane guarantees.

The decision is therefore semantic, not merely geometric. A valid closed planar BRep with fewer faces is not automatically equivalent to the split-preserving prismatic result.

## 3. Definitions

- **Section-boundary split**: an emitted topology boundary at a source section Z plane, such as `z = height - chamferDistance`, that separates faces belonging to adjacent section intervals.
- **Stable interval face**: a face emitted for an interval whose corresponding source edge is unchanged between two adjacent sections. In the top-edge chamfer case, the lower prism side faces in `z0..z1` are stable interval faces.
- **Transition interval face**: a face emitted for the interval where corresponding profile edges are connected between adjacent sections. It may represent a changed side, such as the chamfer face, or an unchanged/coplanar side within the transition interval.
- **Coplanar adjacent faces**: two faces that share an edge and lie on the same geometric plane within tolerance, but remain separate topological faces.
- **Semantic face identity**: the meaning attached to a face by construction source, interval, role, diagnostics, lineage, or future PMI/feature recovery, beyond its geometric plane alone.
- **Topology simplification**: a post-emission operation that reduces topology complexity, such as merging adjacent coplanar faces, while preserving the same geometric bounds.
- **Recognizer parity**: evidence that downstream recognizers produce equivalent candidate sets, admissibility, and diagnostics after a topology change.
- **Merge policy**: the route-level decision about whether coplanar adjacent faces are preserved as semantic section output or simplified into merged faces.

## 4. Current emitted topology

### 4.1 Two-section transition

A two-section transition has no internal section boundary. It has only the lower cap, upper cap, and one interval of side transition faces.

For `n` vertices under the current convention, the expected topology is:

- vertices = `2n`;
- edges = `3n`;
- faces = `n + 2`;
- coedges = `6n`.

The count expands as `n` bottom profile edges, `n` top profile edges, `n` transition edges, `2` cap faces, and `n` transition faces. Since there is only one transition interval, there is no internal section-boundary split to merge away.

### 4.2 Three-section stable+transition

A three-section stable+transition stack has an internal section boundary at `z1`. The emitter preserves split faces across intervals:

- the lower stable prism interval emits its own side faces;
- the upper transition interval emits its own side faces;
- the shared `z1` boundary remains visible in topology through profile edges and adjacent face separation.

For the EDGE-PRISMATIC-X2/V2 top-edge chamfer row, the split-preserving counts are:

- vertices = `12`;
- edges = `20`;
- faces = `10`;
- lower prism side faces = `4`;
- transition faces = `4`;
- chamfer transition faces = `1`;
- loops = `10`;
- coedges = `40`.

The four lower prism side faces represent the stable `z0..z1` interval. The four upper interval faces represent the `z1..z2` transition interval. Only the changed `+X` upper interval face is the chamfer face, but unchanged upper interval faces are still distinct from their lower neighbors because they carry transition-interval context and preserve evidence that the route was a three-section construction.

## 5. Arguments for preserving split faces

- **Construction-history intent**: the source model is a section stack, not a minimal plane set. Section boundaries are construction facts.
- **Transition interval diagnostics**: failures, warnings, or future role classifications can point to the exact stable or transition interval.
- **Simple section-stack formula**: topology counts scale directly with section count and vertex count: `faces = 2 + ((S - 1) * n)` under the current first-scope convention.
- **Recognizer stability**: existing tests and analyzers can rely on known split-preserving counts until a separate merge mode is proven.
- **Future feature/PMI lineage**: split faces provide natural anchors for interval provenance, chamfer role labels, and source-section lineage.
- **Inspectable invalid/transition boundaries**: a boundary like `z1` remains visible for artifact review, CLI summaries, and differential debugging.
- **Avoids premature optimization**: no current production requirement demands a minimal-face BRep, and simplifying before parity evidence would risk destroying load-bearing semantics.

## 6. Arguments for future coplanar merge

A future optional merge mode may still be valuable because it could provide:

- simpler topology with fewer faces;
- a result closer to a hand-authored minimal BRep in simple unchanged-side cases;
- smaller STEP output when adjacent planar faces collapse into fewer `ADVANCED_FACE` entities;
- compatibility with downstream consumers that prefer minimal planar patches;
- reduced face-count surprise for users who expect a rectangular side to be one face when no visible geometric break exists.

These benefits are real, but they are optimization benefits. They do not override the current semantic contract.

## 7. Risks of merging too early

Merging coplanar prismatic faces before the ecosystem agrees on policy risks:

- loss of section-boundary evidence;
- feature-recognition divergence when candidate counts, adjacency, or seam ordering changes;
- face ID/order churn in tests, CLI artifacts, analyzers, and downstream references;
- mismatch with the prismatic corpus and topology summaries built around split-preserving counts;
- accidental conflation of stable and transition intervals;
- harder diagnostics when a transition interval fails or when a changed edge must be isolated;
- replay and differential instability if equivalent geometry alternates between split and merged topology depending on tolerance or route detail.

This ties directly to the V2-A3 doctrine: **geometry parity is not feature parity**, and **STEP parity is not recognizer parity**. A merged body may have the same bounds, planes, and STEP smoke markers while still being a different feature-recognition object.

## 8. Recommended policy

The recommended policy is:

1. First-scope `PrismaticSectionTransitionEmitter` preserves split faces by default.
2. Split faces are semantic output, not incidental artifacts.
3. No coplanar merge occurs inside the emitter.
4. Any future merge must be:
   - explicit;
   - optional;
   - post-emission;
   - diagnostics-preserving;
   - feature-recognition parity tested.
5. Merged output must be a separate route/result mode, for example `PreserveSectionSplits` versus `MergeCoplanarFaces`.
6. Production routes should default to preserving splits until enough parity evidence exists to justify a merged mode for a bounded route.

In short: **coplanar merging is optimization; section-boundary preservation is intent**. Do not optimize away intent until downstream recognizers, diagnostics, and route contracts agree.

## 9. Future merge-admissibility gates

A future merge mode must pass all of these gates before it can be trusted for a bounded route:

- same geometric bounds and surface-family expectations as split-preserving output;
- STEP smoke for both split and merged output without exporter/importer changes unless separately scoped;
- stable merged topology summary with documented formulas and fixtures;
- feature-recognition parity for affected chamfer, fillet, corner, primitive, profile-stack, semantic recovery, and analyzer consumers;
- diagnostics and lineage preservation, including a way to recover original section/interval provenance after merging;
- CLI/artifact corpus comparison that records split and merged outputs as distinct modes;
- explicit user or route policy selecting merge behavior;
- no change to default split-preserving behavior.

## 10. Test implications

Current tests should continue asserting split counts for prismatic rows. In particular, the three-section top-edge chamfer and stable+transition rows should remain at the documented split-preserving counts unless an explicitly named merged-mode fixture is added later.

Future tests for a merge mode should:

- assert both split and merged counts;
- verify STEP smoke for both modes;
- verify recognition parity, not only topology summary parity;
- verify diagnostics preserve original section lineage and transition interval roles;
- prove that default routes still emit split-preserving topology unless merge mode is explicitly requested.

## 11. Documentation/compatibility implications

- EDGE-A1 should treat split preservation as the current prismatic contract for prismatic rows.
- EDGE-PRISMATIC-X1/X2/X3/V1/V2 docs should state that split faces are intentional.
- Any downstream route consuming prismatic section transitions must not silently merge coplanar faces.
- Any future merged output should be named as a separate compatibility surface, not a quiet replacement for existing prismatic output.

## 12. Recommended next milestones

Recommended next milestones are:

1. **EDGE-PRISMATIC-X5**: completed as the prismatic artifact/corpus route using split-preserving output.
2. **EDGE-PRISMATIC-X6**: completed as the gated/manual corpus stability and analyzer confirmation check for the split-preserving corpus.
3. **EDGE-PRISMATIC-X7**: optional coplanar merge proof lab, only after the split-preserving corpus remains stable and analyzer limitations are explicitly classified.
4. **EDGE-PRISMATIC-V3**: controlled Firmament/CLI route if the split-preserving corpus and route-admission evidence are stable enough.
5. Or return to chamfer/fillet work if the prismatic audit trail is sufficient for current planning.

## 13. Non-goals

This milestone does not include:

- implementation;
- a merge pass;
- emitter behavior changes;
- production routing;
- STEP exporter/importer changes;
- Boolean core changes;
- AirEdgeSweep changes;
- `ProfileStackExtrudeExecutor` behavior changes;
- production chamfer/fillet behavior changes;
- public API changes;
- test weakening;
- face-count changes;
- geometry implementation;
- triangle migration retry;
- sketch solver, clipping engine, NURBS, or freeform support.

## 14. EDGE-PRISMATIC-X5 corpus evidence note

EDGE-PRISMATIC-X5 adds a split-preserving artifact corpus for the prismatic section-transition lane. The corpus writes deterministic STEP artifacts for rectangle inset, top `+X` edge chamfer, scaled pentagon, scaled hexagon, and asymmetric pentagon cases, plus JSON-only invalid/deferred diagnostics. Its topology assertions intentionally use the X4 policy: section-boundary split faces are preserved by default, and coplanar merging remains a future optional post-emission optimization only.

## 15. EDGE-PRISMATIC-X6 split-preserving stability evidence note

EDGE-PRISMATIC-X6 adds gated artifact stability and analyzer confirmation evidence for the split-preserving output policy recorded here. The manual `PrismaticCorpusStability` test runs the X5 corpus twice, compares stable JSON/topology/marker/diagnostic projections, compares raw STEP SHA256 hashes plus normalized STEP summaries for successful artifacts, and confirms deterministic `analyze section` output for selected rectangle, top-edge chamfer, and hexagon artifacts. It also invokes `analyze map` and documents the current primitive-raycast limitation as a bounded analyzer integration blocker rather than introducing a coplanar merge or production-route change. This strengthens the default split-preserving contract; it does not add merged mode, mutate topology, change exporter/importer behavior, or replace any production chamfer/fillet route.
