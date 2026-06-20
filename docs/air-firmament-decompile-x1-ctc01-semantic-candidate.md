# AIR-FIRMAMENT-DECOMPILE-X1 — CTC-01 semantic candidate

## 1. Purpose and scope

This milestone records an experimental semantic decompilation of the NIST CTC-01 AP242/BRep model into a plausible, human-readable Firmament V2 source candidate. It is not an automatic STEP-to-Firmament decompiler and does not change parser, lowering, importer, exporter, BRep topology, DisplayIR, tessellation, AIR, CIR, or Firmasm behavior.

The deliverable is a design-only `.firmfixture` that documents likely feature intent and the missing Firmament V2/AIR/kernel capabilities needed before a comparable model can be authored and lowered from source.

## 2. Inputs used

- Source STEP: `testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp`.
- Generated analysis artifact: `artifacts/air-firmament-decompile-x1/ctc01/analyze.json`.
- Generated canonical STEP artifact: `artifacts/air-firmament-decompile-x1/ctc01/nist_ctc_01_canonical.step`.
- Generated canon status artifact: `artifacts/air-firmament-decompile-x1/ctc01/canon.json`.
- Prior CTC-01 display house-call report and screenshots for visual interpretation.
- STEP entity counts from the source file for independent BRep/surface-family cross-checking.

## 3. Backend facts

`aetheris analyze` successfully imported the model as one enclosed body. The current analyzer reports:

| Fact | Value |
| --- | --- |
| Body count | 1 |
| Shell count | 1 |
| Face count | 117 |
| Edge count | 318 |
| Vertex count | 206 |
| Bounding box | min `[-400, -225, -100]`, max `[400, 225, 50]` |
| Length unit basis | `mm`, assumed because STEP import length units are not yet preserved |
| Surface families | plane 56, cylinder 57, cone 4, sphere 0, torus 0, bspline 0, other 0 |

A source STEP entity scan found 117 `ADVANCED_FACE`, 318 `EDGE_CURVE`, 206 `VERTEX_POINT`, 140 `EDGE_LOOP`, 79 `PLANE`, 57 `CYLINDRICAL_SURFACE`, 4 `CONICAL_SURFACE`, 90 `CIRCLE`, 214 `LINE`, and 14 `B_SPLINE_CURVE_WITH_KNOTS` entities. The STEP-level `PLANE` count includes representation/PMI context; the Aetheris imported topology classifies 56 model faces as planar.

The bounding box is symmetric about X=0 and Y=0, with an overall span of 800 mm by 450 mm by 150 mm. That supports a likely design frame with a main XY plate/web profile extruded along Z, then cut and rounded. The visual reference and cylindrical face count support repeated holes, slots, and rounded perimeter/slot features. The 4 conical surfaces likely correspond to chamfer/draft-like transitions or conical trims rather than freeform design surfaces.

## 4. Visual/design interpretation

The likely design intent is a mostly prismatic bracket/plate:

- a main web/plate body in the XY footprint, with thickness along Z;
- four rounded end/tab regions, visually arranged as repeated left/right and upper/lower lobes;
- circular holes through the upper tabs and lower end lobes;
- two horizontal rounded slots through the left and right web regions;
- several smaller circular through-holes near the central web;
- a central raised boss/block with an angular or chamfered top footprint;
- rectangular or rounded-rectangular pockets/notches in the web;
- side steps and relief notches around the tab roots and central bridge;
- many rounds/fillets around outer lobes, slot ends, pocket corners, and step transitions.

The exact authoring sequence cannot be proven from STEP BRep alone. The candidate therefore records feature groups and confidence instead of claiming exact original CAD design history.

## 5. Candidate Firmament V2 source

The candidate fixture is `fixtures/FirmamentV2/Decompile/ctc-01-candidate-v2.firmfixture`. It is marked `validity: semantic-candidate`, `implementation: design-only-not-implemented`, `expected-stage: semantic-candidate`, and `parser-backed: false`.

The candidate intentionally uses readable future-oriented Firmament V2 constructs such as `profile`, `ProfileExtrude`, `ThroughSlot`, `PrismaticThroughCut`, `HolePattern`, `Mirror`, and `round Edges`. These constructs are explicitly marked with `FIRMAMENT-V2-MISSING` comments where they are not parser-backed/lowerable today.

The machine-readable gap matrix is `fixtures/FirmamentV2/Decompile/ctc-01-missing-capabilities.json`.

## 6. Feature decomposition

| Feature | Evidence | Proposed Firmament V2 construct | Current support | Missing capability | Confidence |
| --- | --- | --- | --- | --- | --- |
| Main plate/web outline | Enclosed body, symmetric bounds, many planar side/top faces, visual plate footprint | `profile mainWebOutline` + `ProfileExtrude` | Not parser-backed for V2 CTC-style sketches | Line/arc profiles, constraints, profile extrude lowering | Medium |
| Rounded end tabs/lobes | Visual four rounded regions; many cylindrical surfaces | Arc-bearing profile and perimeter rounds | No general profile arcs or perimeter round selection | Sketch arcs, edge-set references, rounds | Medium |
| Upper/lower circular holes | Visual cylindrical through-holes; 57 imported cylindrical faces and 90 STEP circles | `PrismaticThroughCut` with `Cylinder` tool | Controlled V2 side-hole route exists for box-like cases only | Arbitrary face/axis through-cuts and feature references | Medium |
| Left/right slots | Visual rounded horizontal slots; cylindrical slot-end candidates | `ThroughSlot` | No slot primitive/lowering | Slot syntax, capsule profile cut, robust slot topology | Medium |
| Smaller central holes | Visual repeated small cylinders near center; high cylinder count | `HolePattern` / repeated `PrismaticThroughCut` | Pattern syntax is design-only in future fixtures | Hole pattern semantics and arbitrary cut sequencing | Low-Medium |
| Central raised boss | Visual central protruding block/boss | `add ProfileExtrude` from `centralBossHexProfile` | No arbitrary additive profile feature in V2 | Additive profile extrusion and feature boolean sequencing | Low |
| Rectangular pockets/notches | Visual pocket/cutout regions on web | `PrismaticThroughCut` with `RoundedRectangle` or `Box` tool | Only constrained region side-hole is currently implemented | Arbitrary prismatic cuts and rounded rectangles | Medium |
| Rounds/fillets/chamfer-like transitions | Cylinders and 4 conical surfaces; visual rounded edges | `round Edges` | Future single-edge fillet fixture exists, not CTC-level parser-backed feature set | Edge-set selection, fillet/round/chamfer lowering, ordering | Medium |
| Symmetry/repetition | Bounds symmetric about X/Y axes and repeated visual features | `pattern Mirror` | Pattern fixtures are design-only | Mirror/pattern source semantics and feature instancing | Medium |

## 7. Missing capability matrix

### Language

- Profile/sketch syntax with closed line/arc chains.
- Dimensions, constraints, and named construction geometry.
- Stable feature references, face aliases, loop aliases, and edge-set references.
- Slots, rounded rectangles, and compound feature declarations.
- Mirror/pattern source semantics.
- Confidence/provenance metadata for decompiled design candidates.

### AIR/lowering

- Profile extrude from line/arc sketches.
- Arbitrary prismatic add/extrude features.
- Arbitrary through-cut beyond controlled box side-hole cases.
- Slot cut feature lowering.
- Multi-feature boolean sequencing and feature region integration.
- Fillet/round/chamfer ordering and rejection policy.

### BRep/kernel

- Robust profile extrusion with arc boundaries.
- Slot cut topology construction.
- Constant-radius fillet/round support across imported-style edge sets.
- Feature adjacency and merge policy for many sequential operations.
- Stable face naming across adds/cuts/rounds.

### Display

No display-only blocker is part of this milestone. Display defects should stay separate from Firmament source/lowering blockers.

## 8. Current parser/lowering gap

Current Firmament V2 support is centered on parser-backed primitives and controlled region cases: `Box`, record `with` derivation, `expose` aliases, face aliases, radius/center checks, and the side-hole controlled route policy. That is enough for simple box side-hole fixtures but not enough for CTC-01.

CTC-01 requires source-level profile authoring, arbitrary prismatic additive and subtractive features, slot primitives, repeated/mirrored features, and rounds/fillets. Those are beyond the current controlled V2 route and should not be implied to work by this candidate fixture.

## 9. Confidence and ambiguity

High confidence:

- CTC-01 imports and canonicalizes as one enclosed BRep body.
- The model is planar/cylindrical/conical rather than freeform.
- The visual design includes a plate/web, rounded tabs/lobes, holes, slots, pockets/notches, and rounds.

Medium confidence:

- The principal authoring frame is an XY footprint extruded along Z.
- The part is intended to exploit left/right and upper/lower repetition.
- Slots and major holes should become semantic source features instead of raw BRep faces.

Low confidence:

- Exact dimensions, feature order, and original CAD constraints.
- Whether the center protrusion was authored as a boss, block, drafted feature, or trimmed extrusion.
- Which cylindrical faces are decorative rounds versus hole/slot side walls without deeper face adjacency grouping.

STEP BRep alone does not preserve original sketch constraints, feature tree, construction axes, or designer naming. Human/CAD confirmation is needed before treating the candidate as exact design intent.

## 10. Recommended next mainline milestones

1. `AIR-FIRMAMENT-X13` — PrismaticThroughCut semantic lowering policy for arbitrary face/axis cuts beyond the controlled side-hole fixture.
2. `AIR-FIRMAMENT-X14` — Profile2D / line-arc sketch source candidate with dimensions and construction geometry.
3. `AIR-FIRMAMENT-X15` — Minimal parser-backed `ProfileExtrude` plate from closed line/arc profile.
4. `AIR-FIRMAMENT-X16` — Slot cut feature syntax and lowering.
5. `AIR-FIRMAMENT-X17` — Round/fillet/chamfer capability audit with edge-set references and ordering rules.
6. `AIR-FIRMAMENT-X18` — Mirror/pattern source semantics for repeated holes and lobes.
7. `AIR-FIRMAMENT-X19` — Feature provenance/semantic-candidate fixture classification so design-only decompilation candidates can be tracked without entering active valid corpus execution.

## 11. Non-goals

- No automatic decompiler.
- No parser/lowering implementation.
- No STEP import/export change.
- No DisplayIR or frontend display fix.
- No BRep topology change.
- No tessellator algorithm change.
- No existing Firmament V2 syntax semantic change.
- No AIR Region route policy change.
- No CIR authority change.
- No Firmasm change.
- No new CAD feature behavior.
