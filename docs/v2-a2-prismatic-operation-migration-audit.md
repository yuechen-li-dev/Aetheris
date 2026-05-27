# V2-A2 — Prismatic operation migration audit

## 1. Executive summary
V2-V4 established a production-adjacent `LineArcProfileExtrudeEmitter` that can emit STEP-valid analytic BReps from one outer loop plus optional hole loops spanning line segments, circular arcs, and full circles without invoking 3D Boolean.

This materially changes migration posture: multiple existing prismatic operations in Aetheris are structurally “resolved profile + linear extrusion” operations already, but currently route through older polyline primitive helpers or dedicated hole emitters.

This audit inventories current operations, classifies representability against the resolved line/arc profile path, and proposes a lowest-risk migration order based on existing topology/STEP evidence.

No production behavior, routing, public API, STEP exporter/importer, or Boolean core behavior was changed in this milestone.

## 2. Source code/docs inspected

### Core implementation
- `Aetheris.Kernel.Firmament/Materializer/LineArcProfileExtrudeEmitter.cs`
- `Aetheris.Kernel.Firmament/Materializer/ProfileHoleExtrudeEmitter.cs`
- `Aetheris.Kernel.Firmament/Materializer/ProfileExpressionHoleExtrudeEmitter.cs`
- `Aetheris.Kernel.Core/Brep/BrepPrimitives.cs`
- `Aetheris.Kernel.Core/Brep/Features/BrepExtrude.cs`
- `Aetheris.Kernel.Firmament/Execution/FirmamentPrimitiveExecutor.cs`
- `Aetheris.Kernel.Firmament/Execution/FirmamentPrismFamilyTools.cs`
- `Aetheris.Kernel.Firmament/Lowering/FirmamentPrimitiveLowerer.cs`
- `Aetheris.Kernel.Firmament/ParsedModel/FirmamentKnownOpKind.cs`

### Tests / fixtures / examples
- `Aetheris.Kernel.Firmament.Tests/Integration/LineArcProfileExtrudeEmitterTests.cs`
- `Aetheris.Kernel.Firmament.Tests/Integration/ProfileHoleExtrudeEmitterTests.cs`
- `Aetheris.Kernel.Firmament.Tests/Integration/ProfileExpressionHoleExtrudeEmitterTests.cs`
- `Aetheris.Kernel.Firmament.Tests/FirmamentPrimitiveExecutionTests.cs`
- `Aetheris.Kernel.Firmament.Tests/FirmamentBuildAndExportTests.cs`
- `Aetheris.Kernel.Firmament.Tests/FirmamentExamplePackSmokeTests.cs`
- `testdata/firmament/examples/triangular_prism_basic.firmament`
- `testdata/firmament/examples/hexagonal_prism_basic.firmament`
- `testdata/firmament/examples/straight_slot_basic.firmament`
- `testdata/firmament/examples/slot_cut_basic.firmament`

### Architecture / milestone docs
- `docs/aetheris-v2-sweep-first-architecture.md`
- `docs/aetheris-v2-a1-resolved-profile2d-contract.md`
- `docs/frictionlab/v2-x1-resolved-profile2d-lab.md`
- `docs/frictionlab/v2-x6-slot-capsule-profile-lab.md`
- `docs/frictionlab/v2-x7-line-arc-profile-extrude-lab.md`
- `docs/v2-v1-profile-hole-extrude-production-evaluation.md`
- `docs/v2-v2-profile-hole-extrude-through-hole-integration.md`
- `docs/v2-v4-line-arc-profile-extrude-production-evaluation.md`

## 3. Audit criteria
Each candidate operation was evaluated on:
- current execution path (lowering -> primitive/tool body -> emitter),
- API surface (public primitive vs internal emitter vs firmament op family),
- current topology and STEP behavior expectations from tests,
- required profile representation,
- curve-class fit (`line-only`, `line+arc`, `full-circle`, unsupported),
- direct representability by `LineArcProfileExtrudeEmitter`,
- existing parity evidence,
- missing evidence required before migration,
- migration risk,
- recommended next action.

## 4. Candidate inventory table

| Candidate | Current path | Profile shape | AIR/V2 representation | Existing parity evidence | Migration risk | Recommendation |
|---|---|---|---|---|---|---|
| Rectangle/box prism | `BrepPrimitives.CreateBox` -> `BrepExtrude.Create` | Outer rectangle (line-only) | Single outer line loop | Prior AIR-V3 production migration + core tests | Low | Keep as-is (already extrude-backed); use as parity baseline |
| Triangle prism | `BrepPrimitives.CreateTriangularPrism` -> `BrepExtrude.Create`; Firmament primitive routes here | Outer 3-edge polygon (line-only) | Single outer line loop | Face-count=5 in primitive execution tests; build/export fixtures exist | Low | First migration candidate (lab/prod gate based on parity tests) |
| Hexagonal prism | `BrepPrimitives.CreateHexagonalPrism` -> `BrepExtrude.Create`; Firmament primitive routes here | Outer regular 6-edge polygon (line-only) | Single outer line loop | Face-count=8 test evidence; build/export fixtures exist | Low-Med | Second migration candidate after triangle |
| Regular polygon prism (generic) | No explicit generic public primitive found; hex/triangle are specialized | N-gon line-only | Would be single outer line loop | No direct generic op evidence | Med | Defer until generic profile front door/admissibility introduced |
| Straight slot prism | `BrepPrimitives.CreateStraightSlot` approximates slot with polyline semicircle segmentation -> `BrepExtrude.Create` | Obround approximated as many lines today (line-only approximation); geometric intent is line+arc | Could be canonical line+arc outer loop (2 lines + 2 arcs) | Primitive execution + export smoke exist; no parity assertion vs analytic-arc topology | Med-High | Needs lab parity: analytic arc/cylindrical side expectations vs current faceted shape |
| Slot cut primitive | `StandardLibraryPrimitives.CreateSlotCut` (rounded rectangle profile path) via Firmament primitive/tool families | Rounded rectangle likely line+arc intent, current path may polyline | Line+arc outer loop candidate | Execution/export smoke exist; topology semantics broad (`>=10` faces style checks) | Med-High | Audit further in focused slot/capsule evaluation before migration |
| Rectangle with circular holes | Internal `ProfileHoleExtrudeEmitter` and `ProfileExpressionHoleExtrudeEmitter`; through-hole route in V2-V2 | Outer rectangle + full-circle hole loops | Outer line + inner full-circle loops | V2-V1/V2-V2/V2-V3/V2-V4 tests and diagnostics include no-3D-Boolean behavior | Low-Med | Keep route stable now; optionally rehost internals on LineArc emitter behind strict parity tests |
| Rectangle with slot hole | V2-X7/V2-V4 line-arc emitter tests (internal production-adjacent) | Outer rectangle + slot hole loop (line+arc) | Outer line + inner line/arc loop | Direct `LineArcProfileExtrudeEmitterTests` parity (planar/cylindrical counts + STEP markers) | Medium | Strong candidate for next production-adjacent integrations where operation exists |

## 5. Per-candidate analysis

### 5.1 Rectangle/box prism
- Current implementation: `CreateBox` already constructs a rectangle profile and extrudes via `BrepExtrude.Create`.
- Current topology: expected 2 planar caps + 4 planar side faces.
- STEP markers: existing primitive/export tests validate standard STEP shell/solid markers.
- Proposed `ResolvedProfile2D`: one outer line-only rectangle loop.
- V2-V4 support: fully representable.
- Gaps/blockers: none for representability; this path is already effectively “resolved profile extrusion” using core profile type.
- Test needs: none for this milestone beyond baseline retention.
- Recommendation: keep current path as baseline reference; no migration work needed.

### 5.2 Triangle prism
- Current implementation: Firmament triangular_prism lowers to `FirmamentLoweredPrimitiveKind.TriangularPrism` and executes via `BrepPrimitives.CreateTriangularPrism`, which builds a 3-point polyline profile and extrudes.
- Current topology: tests assert 5 faces (2 caps + 3 sides), all planar expected.
- STEP expectations: build/export fixtures exist for `triangular_prism_basic`.
- Proposed `ResolvedProfile2D`: single outer loop of three line segments.
- V2-V4 support: supported directly as line-only loop.
- Gaps/blockers: need direct parity tests if execution route is ever switched (face-kind and STEP smoke, not just face count).
- Test needs: valid/invalid dimensions, exact face-kind counts, no-3D-Boolean diagnostics if routed through new emitter.
- Recommendation: ready for lab-first migration probe; likely first production migration target once parity suite exists.

### 5.3 Hexagonal prism
- Current implementation: lowered and executed via `BrepPrimitives.CreateHexagonalPrism`; profile generated from regular hex vertices; extruded.
- Current topology: tests assert 8 faces (2 caps + 6 side planar faces).
- STEP expectations: export fixture exists for `hexagonal_prism_basic`.
- Proposed `ResolvedProfile2D`: single outer loop with six line segments.
- V2-V4 support: supported directly as line-only loop.
- Gaps/blockers: vertex ordering/orientation and across-flats convention parity must be locked.
- Test needs: coordinate convention checks + topology/STEP markers.
- Recommendation: second migration candidate after triangle parity is proven.

### 5.4 Straight slot prism
- Current implementation: `CreateStraightSlot` currently approximates semicircular ends with segmented polyline vertices (`semicircleSegments = 8`) then extrudes.
- Current topology: test only asserts `Faces.Count() >= 10`, indicating tolerance for faceted sides.
- STEP expectations: export fixture exists, but no strict analytic cylinder expectation.
- Proposed `ResolvedProfile2D`: single outer loop = line + arc (2 lines + 2 semicircular arcs).
- V2-V4 support: representable and already evidenced in internal slot-hole cases.
- Gaps/blockers: migration would change topology family (faceted planar sides -> cylindrical side faces on arcs), so parity contract must be explicitly redefined.
- Test needs: semantic assertions for planar vs cylindrical side-face counts and arc orientation stability.
- Recommendation: defer production migration until dedicated slot/capsule production evaluation (X9-style) lands.

### 5.5 Slot cut primitive
- Current implementation: `slot_cut` lowers as prism-family tool but body creation is delegated to `StandardLibraryPrimitives.CreateSlotCut`; footprint resolver uses rounded-rectangle profile conversion.
- Current topology: broad smoke assertions only.
- STEP expectations: example/fixture export coverage exists.
- Proposed `ResolvedProfile2D`: likely outer line+arc loop; exact corner-radius admissibility must be matched.
- V2-V4 support: likely representable for bounded rounded-rectangle cases.
- Gaps/blockers: incomplete parity specifics and potential dependence on existing standard-library shaping conventions.
- Test needs: explicit corner-radius boundary, orientation, and analytic family assertions.
- Recommendation: treat as medium/high risk; migrate after straight-slot evidence and only with strict parity tests.

### 5.6 Rectangle with circular holes
- Current implementation: bounded internal emitter routes (`ProfileHoleExtrudeEmitter`, expression front door, and V2-V2 through-hole integration path).
- Current topology: rectangle outer with circular hole loops, cylindrical hole side faces.
- STEP expectations: production-adjacent tests already validate STEP validity and no 3D Boolean diagnostics.
- Proposed `ResolvedProfile2D`: outer line loop + one/many full-circle hole loops.
- V2-V4 support: directly representable.
- Gaps/blockers: routing stability and diagnostic contract continuity.
- Test needs: if rehosted, preserve current diagnostics and rejection boundaries.
- Recommendation: keep current bounded routes intact; optional internal consolidation only after diagnostic/topology parity tests.

### 5.7 Rectangle with slot hole
- Current implementation: evidenced in V2-X7 and V2-V4 integration tests for `LineArcProfileExtrudeEmitter`.
- Current topology: 2 planar caps + line-derived planar sides + arc-derived cylindrical sides.
- STEP expectations: tests check `MANIFOLD_SOLID_BREP` and `CYLINDRICAL_SURFACE` where expected.
- Proposed `ResolvedProfile2D`: outer rectangle loop + slot/capsule hole loop.
- V2-V4 support: yes (directly proven).
- Gaps/blockers: no broad production front-door route yet.
- Test needs: additional Firmament-facing route tests once integration target is selected.
- Recommendation: ready for controlled production-adjacent adoption where bounded admissibility exists.

## 6. Recommended migration order
Evidence-based proposed order:
1. Triangle prism (line-only, minimal topology, strong existing primitive + fixture evidence).
2. Hexagonal prism (line-only but extra risk on regular polygon convention/orientation).
3. Rectangle with circular holes (if internal emitter consolidation is desired, guarded by diag parity).
4. Rectangle with slot/capsule hole (already line/arc-evidenced; integrate through bounded front door).
5. Straight slot prism / slot cut primitive (after explicit contract decision on faceted-vs-analytic side topology).
6. Broader profile-expression front door/general polygon prism families.

## 7. Required tests before each migration
For each migration candidate, require:
- valid dimension acceptance tests,
- invalid dimension rejection tests,
- topology counts (faces/loops) and semantic family counts (planar/cylindrical),
- STEP smoke markers (`ISO-10303-21`, `MANIFOLD_SOLID_BREP`, expected surface tags),
- CLI coverage for representative `.firmament` fixture builds,
- explicit no-3D-Boolean diagnostics for new line/arc emitter routes,
- fallback behavior assertions if routing is introduced behind existing path.

Candidate-specific minimums:
- Triangle: assert exactly 5 planar faces.
- Hexagon: assert exactly 8 planar faces and stable winding/orientation.
- Rectangle+circle holes: preserve existing `v2-v1/v2-v2/v2-v3` diagnostic semantics where externally observed.
- Slot/capsule: assert expected planar/cylindrical mix and arc orientation correctness.
- SlotCut: assert corner radius admissibility + topology family expectations at boundaries.

## 8. Relationship to existing emitters
- `LineArcProfileExtrudeEmitter` should be preferred internal emitter target for bounded resolved line/arc profile prismatic extrusion.
- `ProfileHoleExtrudeEmitter` should remain active for its current bounded production through-hole route until parity-proven replacement/wrapping exists.
- `ProfileExpressionHoleExtrudeEmitter` remains the bounded expression front door for rectangle-minus-circle(s).
- Emitter collapse/replacement is not recommended before migration-specific parity suites are in place.

## 9. Risks and guardrails

### Risks
- Interpreting audit findings as permission for broad routing changes.
- Losing topology/STEP parity while “simplifying” paths.
- Brittle assumptions about face/edge ordering.
- Arc orientation and cap-loop orientation regressions.
- Invalid profile leakage into BRep emission.
- Scope creep into general clipping/sketch-solver territory.

### Guardrails
- Lab-first, production-second progression.
- Explicit admissibility boundaries on each route.
- Prefer semantic face-kind assertions over brittle ID ordering.
- Require no-3D-Boolean diagnostics on new emitter migrations.
- Keep fallback routes until parity is proven.
- Preserve “no STEP/Boolean core changes” boundary.

## 10. Recommended next milestones
- **V2-X8**: Triangle/hex prism line-profile extrusion parity lab (if direct production parity evidence is still considered insufficient).
- **V2-V5**: Triangle prism production migration behind strict parity + diagnostic checks.
- **V2-X9**: Slot/capsule production evaluation focusing on faceted-to-analytic topology contract decision.
- **V2-A3**: Internal `ResolvedProfile2D` extraction/alignment for production reuse where lab/internal type boundaries currently limit sharing.

## 11. Non-goals
This milestone explicitly does **not** do:
- production routing changes,
- full 2D clipping engine work,
- sketch solver introduction,
- NURBS/freeform support,
- STEP exporter/importer or Boolean core changes,
- blind/counterbore/stepped/cross-axis expansion.
