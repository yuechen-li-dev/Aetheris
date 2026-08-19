# P1-INTEROP-M1 interoperability matrix

Generated 2026-08-08 from the frozen Preview 1 fixtures. The machine-readable
evidence is checked in under [`artifacts/release/interop/`](../../artifacts/release/interop):
`manifest.json` contains Aetheris reimport topology/family data and hashes,
`freecad-smoke.json` contains the FreeCAD 1.0.2 / OCCT smoke output, and
`determinism.json` records a second build of every source route.

## Generated-model route

Route: Firmament -> Aetheris exact B-rep -> canonical STEP AP242 -> Aetheris
reimport -> FreeCAD 1.0.2 / OCCT. Every artifact below has one body and one
closed shell, reimports as `enclosed-manifold`, has contiguous sequential
face/edge/vertex IDs, and passed FreeCAD `isValid`/`isClosed` without an
explicit healing or repair operation. Exact counts, family counts, byte sizes,
and SHA-256 values are in the manifest rather than duplicated here.

| Fixture/model | Release artifact | Aetheris reimport | FreeCAD/OCCT | SolidWorks | Analytic evidence / note | Result |
| --- | --- | --- | --- | --- | --- | --- |
| Box | `primitive-box.step` | pass | pass, no healing | manual smoke pending | Plane | pass |
| Cylinder | `primitive-cylinder.step` | pass | pass, no healing | not selected | Plane, Cylinder | pass |
| RoundedBox | `primitive-rounded-box.step` | pass | pass, no healing | not selected | Plane, Cylinder, Sphere | pass |
| Frustum | `primitive-frustum.step` | pass | pass, no healing | not selected | Plane, Cone | pass — V2 bridge regression fixed |
| Shaft ThroughAll | `hole-shaft-throughall.step` | pass | pass, no healing | not selected | Plane, Cylinder; inner loop | pass |
| Counterbore | `hole-counterbore.step` | pass | pass, no healing | manual smoke pending | Plane, Cylinder; mouth/shoulder/shaft | pass |
| Countersink | `hole-countersink.step` | pass | pass, no healing | manual smoke pending | Plane, Cone, Cylinder | pass |
| Capsule slot | `slot-capsule.step` | pass | pass, no healing | not selected | Plane, Cylinder | pass |
| RoundedRectangle slot | `slot-rounded-rectangle.step` | pass | pass, no healing | not selected | Plane, Cylinder | pass |
| Concept Path L-bracket | `profile-l-bracket.step` | pass | pass, no healing | manual smoke pending | Plane; conventional prismatic topology | pass |
| Four-hole Pattern | `pattern-four-hole.step` | pass | pass, no healing | not selected | Plane, Cylinder | pass |
| Mixed supported Chamfer | `edgefinish-chamfer.step` | pass | pass, no healing | imports, but Parasolid materialization defect observed | Plane, Cone, Cylinder | pass in Aetheris/OCCT/ACIS; see known limitation |
| Bounded reflex Fillet | `edgefinish-fillet-bounded.step` | pass | pass, no healing | manual smoke pending | Plane, Cylinder | pass |
| Projected HoleDiameter PMI | `pmi-projected-hole.step` | pass | pass, no healing | manual smoke pending | semantic PMI emitted; downstream display not asserted | pass |
| Whole-loop Fillet chimera | `experimental-edgefinish-fillet-chimera.step` | pass | pass, no healing | manual smoke pending | Plane/Cylinder/Sphere; **Experimental** | evidence only |
| Whole-loop SphereSeamCompatibility | `experimental-edgefinish-fillet-sphere-compat.step` | pass | pass, no healing | manual smoke pending | Plane/Cylinder/Sphere; **Experimental** | evidence only |

FreeCAD's queried surface class counts are retained in `freecad-smoke.json`.
Aetheris's importer reports zero B-spline surfaces in this corpus. The
experimental entries remain Experimental: successful import does not close the
separate curved-trim mass-verification blocker.

### Known downstream limitation: Parasolid chamfer materialization

Manual evidence shows the generated chamfer artifact renders correctly in
ACIS-based Fusion 360 and OCCT-based CAD Assistant/FreeCAD, while a
Parasolid-based downstream importer does not materialize the chamfer correctly.
The exported STEP remains valid in Aetheris and OCCT and needs no healing.
Classify this as an `INTEROP` limitation in the downstream Parasolid import
path, not as a reason to silently alter or heal Aetheris geometry. Users
targeting Parasolid-based CAD should visually inspect supported chamfers after
import.

## Existing-model route

| Workflow | Fixture / evidence | Result | Boundary |
| --- | --- | --- | --- |
| Analyze | `testdata/firmament/inline-step/canonical-through-hole.step`; `aetheris analyze --json` | pass | Sequential IDs are contiguous and expose raw STEP traceability; `Source.Face(7)` is authoring vocabulary, not `ADVANCED_FACE #…`. |
| InlineStep | `inline-step-recognize-replace.firmament` | pass | Canonical Aetheris AP242 input only; source-relative path, body, and topology map preserved. |
| Recognize | same fixture, `HoleShaft` region `MountHole`, face 7 | pass | Bounded HoleShaft / DatumPlane only; no automatic decompiler claim. |
| Replace | same fixture -> `inline-step-replace.step` | pass | One valid manifold body; Aetheris and FreeCAD reimport pass; ThroughAll Shaft replacement only. |
| Label only | `inline-step-v2-recognized-hole-proposal-report.valid.firmfixture` | supported internally | Preview 1 records bounded semantic labels/proposals and can inspect them; it does not promise persistence of arbitrary foreign label state on re-export. |
| Header variation | CLI baseline foreign-header test | pass | Harmless header/product metadata variation imports. |
| FreeCAD foreign pad | `testdata/step242/syntax-robustness/freecad-pad-repro.step` | limited: one planar, `leaky-or-open` body reported | Analyzer does not crash and explicitly reports non-manifold/open topology, but this is not an admitted replacement input. Classify as INTEROP/DEFERRED rather than silently treating it as a valid solid. |

## Orientation, tolerances, and PMI

The corpus exercises hole inner loops, circular trims, clockwise/reflex profile
finishes, sphere-seam compatibility, and ellipse/arc-capable profile paths.
The exporter contract preserves `EDGE_CURVE.same_sense`; Aetheris self-reimport
and FreeCAD closed/valid checks are direct regression evidence against detached
faces and trim-tab regressions. No tolerance was widened for this milestone.
FreeCAD's successful import is geometry evidence, not a claim that it presents
AP242 PMI visually. PMI is classified separately: semantic entities are emitted
and survive Aetheris inspection; downstream presentation/preservation needs
manual-reader evidence.

## Support table

| Workflow / format / tool | Status | Notes |
| --- | --- | --- |
| Firmament -> STEP -> Aetheris | Supported | 17 release artifacts self-reimported cleanly. |
| Firmament -> STEP -> FreeCAD/OCCT | Supported | 17/17 valid, closed imports; no explicit healing. |
| Firmament -> STEP -> SolidWorks / Parasolid | Smoke-tested with limitation | General subset works as manually checked; supported chamfers may not materialize correctly in Parasolid import. |
| Existing canonical analytic STEP -> Analyze | SupportedBounded | Sequential semantic face IDs and traceability. |
| Existing canonical STEP -> Hole recognition/replacement | SupportedBounded | HoleShaft / DatumPlane recognition; ThroughAll Shaft replacement. |
| Arbitrary spline-heavy foreign STEP | Unsupported/limited | Clear bounded failure is required; no general healing/decompilation. |

## Open release evidence

`RELEASE-BLOCKER`: manual SolidWorks smoke of the compact high-value subset is
still outstanding. `DEFERRED`: independent curved-trim mass cross-check and
tight certification for the two Experimental whole-loop fillets belongs to
P1-HARDEN-M2. No generated advertised artifact required FreeCAD healing.
