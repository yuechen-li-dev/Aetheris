# BENCHCAD-PREP-X1 — reconstruction primitive closure

## Status and executive answer

This milestone reaches **meaningful progression**. Seven of the same eight X0 smoke records now score exactly `1.0000`; the eighth rises from `0.0570` to `0.9409`. The remaining error is isolated: the axial bore crosses both the stem and sphere, while public Firmament can express the annular stem but does not apply `Hole` to a `Sphere`. Adding a record-specific Boolean, `BallKnob`, or premature `Revolve` would violate this milestone's scope.

The X0 failures primarily reflected missing basic orientation, inspection, and semantic-decomposition tools—not missing complex geometry. The rotated washer is an annular profile on a Y-normal construction plane. In fact, the audit found that ordinary `Profile Using <Construction Plane>` extrusion already had arbitrary-frame authority; X0 had failed to discover and use it. X1 reuses that path and makes Compose's explicit placement honor its declared signed frame instead of rejecting it. The battery holder is rectangular stock modified by two repeated major-arc profile deltas and extruded along X. The ball knob is a two-component sphere-and-cylinder assembly with an explicit interface. Once those facts were visible and authorable, three formerly difficult records became exact.

This remains an open-book, human-assisted smoke qualification. It is not blind, is not an official BenchCAD score, and is not leaderboard-comparable.

## Reproducibility

| Item | Value |
| --- | --- |
| BenchCAD commit | `52087ef4b08811c21c09b8b16d0ae36c70844865` |
| Aetheris base commit | `4605ac2355b5ff5aeca334225eb18c3df8e3d19b` plus this milestone's working-tree changes |
| Corpus | The exact eight committed X0 smoke records |
| Scorer | BenchCAD `iou_step_vs_step`, normalized 64-cube voxel IoU |
| Harness | `scripts/benchcad-aetheris-x1.py`, reusing `scripts/benchcad-aetheris-x0.py` |
| Generated evidence | `artifacts/local/benchcad-aetheris-x1/` (ignored) |

The X0 baseline is supplied explicitly rather than inferred from an intermediate run:

```powershell
python scripts/benchcad-aetheris-x1.py `
  --aetheris-root (Get-Location).Path `
  --benchcad-root (Resolve-Path ..\BenchCAD-main).Path `
  --aetheris-cli artifacts/local/benchcad-aetheris-x1/publish/aetheris.exe `
  --baseline-summary artifacts/local/benchcad-aetheris/results-summary.json
```

## X0 to X1 scores

| Record | X0 raw IoU | X1 raw IoU | Delta | X1 normalized IoU | Primary reason |
| --- | ---: | ---: | ---: | ---: | --- |
| `bolt_000008_s20260505` | 1.0000 | 1.0000 | 0.0000 | n/a | Baseline preserved |
| `hex_nut_000006_s20260505` | 1.0000 | 1.0000 | 0.0000 | n/a | Baseline preserved |
| `spacer_ring_000007_s20260505` | 1.0000 | 1.0000 | 0.0000 | n/a | Geometry preserved; trimmed-major-arc bounds fixed |
| `washer_000001_s20260505` | 0.0325 | 1.0000 | +0.9675 | n/a | Construction-plane profile and Y-axis extrusion |
| `ball_knob_cylinder_radius_f130` | 0.0716 | 1.0000 | +0.9284 | 1.0000 | Sphere + cylinder assembly, not a unioned feature |
| `battery_holder_box_y_f130` | 0.6813 | 1.0000 | +0.3187 | 1.0000 | Rectangle plus two major-arc deltas, extruded along X |
| `topup_ball_knob_axial_hole` | 0.0570 | 0.9409 | +0.8839 | 0.4911 | Exact annular stem; sphere-side continuation remains unsupported |
| `topup_ball_knob_axial_hole_rev` | 0.0620 | 1.0000 | +0.9380 | 1.0000 | Sphere + narrower cylinder assembly |

Vision2Code mean raw IoU rises from `0.7581` to `1.0000`. CodeEdit mean raw IoU rises from `0.2180` to `0.9852`, and its mean normalized IoU is `0.8728`. All eight sources build; seven are exact.

## Corrected geometry interpretation

Human visual review corrected three X0 hypotheses:

- **Washer:** the geometry was already trivial; only the coordinate frame was wrong. The X1 source uses an ordinary annular `Profile Using WasherFrame` whose plane normal is world `-Y`.
- **Ball knob:** the STEP contains two disconnected simple solids. Compound inspection reports one cylindrical solid and one spherical solid, so the reconstruction uses definitions, occurrences, an `Interface`, and a `FrameCoincident` mate.
- **Battery holder:** it is not a blind-cylinder feature. Its constant YZ section is a rectangle whose top carrier is replaced by two inward major circular arcs. Following the supplied red/green sketch, X1 starts with the red rectangular loop and applies two green arc deltas; thin full-stock end slices preserve the end walls.

This human intervention is part of the experiment and is why the result must not be described as blind.

## Inspection evidence

The new section command makes the battery-holder construction explicit:

```text
plane: YZ at x=0
closed loops: 1
segments: 6 lines + 2 arcs
each arc: radius 5.25 mm, sweep 4.381389 rad (251.04 degrees)
section bounds: [-19.5,-4.4] to [19.5,4.4] mm
```

The deterministic SVG is generated at `artifacts/local/benchcad-aetheris-x1/battery-holder-target-section.svg`. Both sweeps exceed 180 degrees; their cardinal extrema are included only when they lie in the periodic trim interval.

Compound inspection of `ball_knob_cylinder_radius_f130` reports `RootCount=2`, `SolidCount=2`, aggregate bounds `[-12.5,-12.5,-62.5]` to `[12.5,12.5,12.5]`, and separate inventories of one cylinder plus two planes and one sphere. It neither rejects the file nor silently selects a root. The assembly remains inspectable as `Knob`, `Stem`, `KnobStemInterface`, and `TangentAttachment`.

The washer source inspection identifies a construction plane with origin `[0,0,0]`, normal `[0,-1,0]`, local up `[0,0,1]`, and extrusion span 0–5 mm. STEP export and reimport preserve those world-space bounds.

The spacer-ring major-arc regression now reports the authoritative full bounds rather than endpoint-only bounds. Periodic cardinal candidates at 0, 90, 180, and 270 degrees are admitted only when contained by the actual wrapped sweep.

## Before and after authoring

The washer changed from an implicitly XY/+Z profile to the same profile on shared plane authority:

```firmament
Concept Struct WasherDatum {
    Plane: Plane { Origin: [0mm,0mm,0mm]; Normal: [0,-1,0]; Up: [0,0,1] }
}
Construction Plane WasherFrame { Trace: WasherDatum.Plane }
Profile WasherProfile Using WasherFrame { ... }
Struct Washer { Extrude Body { Profile: WasherProfile; From: 0mm; To: 5mm } }
```

The ball witness changed from a lone `Sphere` to ordinary assembly semantics:

```firmament
Assembly BallKnob {
    <Assembly BallKnob>
        <Part Knob = KnobSphere<R: 12.5mm>> ... </Part>
        <Part Stem = KnobStem<R: 5.85mm>> ... </Part>
    </Assembly>
    Mate TangentAttachment: KnobStemInterface { ... }
}
```

The battery holder now directly encodes the reviewed cross-section:

```firmament
Rect2 Stock { Center: [0mm,0mm]; Size: [8.8mm,39mm] }
ProfileDelta RightPocket { On: Stock.Right ... Transition ArcA { Kind: Round; Radius: 5.25mm; ... } ... }
ProfileDelta LeftPocket  { On: Stock.Right ... Transition ArcA { Kind: Round; Radius: 5.25mm; ... } ... }
Profile GroovedProfile From Stock
Compose Body {
    Placement HolderPlacement { ProfilePlane: YZ; Axis: +X; ReferenceDirection: +Z }
    Base GroovedStock { Profile: GroovedProfile; From: -24.25mm; To: 24.25mm; Role: Stock }
}
```

This uses the existing `ProfileDelta` and Compose lowering authority; there is no benchmark-specific pocket type or second extrusion backend.

## Architectural value

| Capability | Motivating record | General CAD value | Public? |
| --- | --- | --- | --- |
| Existing construction-plane Profile extrusion, plus signed Compose placement closure | Washer | Arbitrarily oriented prismatic construction and signed principal Compose placement through shared frame math | Yes |
| Principal/arbitrary-plane analytic section plus SVG | Battery holder | Reveals constant-section construction before feature invention | Yes, CLI |
| Trim-correct periodic arc bounds and sweep | Spacer ring, battery holder | Correct major/minor arc geometry, bounds, and machine-readable provenance | Yes, CLI analysis |
| Exact compound-root inspection | Ball knobs | Inventories disconnected solids without inventing product semantics | Yes, CLI |
| Model-target assembly definitions | Ball knobs | Reuses analytic model definitions as typed assembly components | Yes |
| Explicit sphere/cylinder interface reconstruction | Ball knobs | Preserves engineering identity and attachment semantics while exporting occupied STEP geometry | Yes |

## Compatibility and scope

Profiles without an explicit plane retain canonical XY/+Z behavior. Existing arbitrary construction-plane Profile extrusion remains authoritative. Compose placements now resolve all signed principal axes `+X/-X/+Y/-Y/+Z/-Z` through the same `ConstructionPlane` math and apply the world transform once at lowering. No `ExtrusionPlane`, axis-specific profile type, multibody Part, automatic feature recognizer, arbitrary Boolean union, `BallKnob`, `BatteryHolderPocket`, or benchmark scoring change was introduced.

Multiple STEP roots remain serialization facts, not automatic Firmament product semantics. `analyze compound` reports them exactly; assembly import/authoring owns product structure and relationships.

## Validation ledger

The release validation for this milestone completed as follows:

- `dotnet build Aetheris.slnx -c Release -m:1 --no-restore`: passed with zero warnings and zero errors;
- `dotnet test Aetheris.slnx -c Release --no-build -m:1`: 3,416 tests passed; the intentionally empty FrictionLab assembly reported no tests;
- default and arbitrary construction-plane Profile binding/materialization, including a non-principal normal;
- all six signed principal Compose extrusion axes and a nonzero placement anchor;
- STEP export/reimport orientation;
- principal and arbitrary section extraction, deterministic SVG, >180-degree arc sweep, wrapped periodic trims, and spacer-ring bounds;
- two-root compound inventory without first-root selection;
- sphere/cylinder assembly materialization, interface inspection, AP242 export, and reimport;
- a freshly published CLI rebuilding/scoring the washer, battery holder, ball-knob family, and the same eight X0 records;
- deterministic repeated packaged-CLI output: 26 reconstructed STEP, inspection JSON, and SVG files checked, zero SHA-256 mismatches;
- modified-document relative-link check: three files checked, zero broken links;
- repository information-architecture guard: 4,069 tracked files inspected, passed;
- Python harness compilation and `git diff --check`: passed (line-ending notices only).

The detailed generated JSON, STEP, SVG, scoring, and preview evidence remains under ignored `artifacts/local/benchcad-aetheris-x1/` in accordance with the generated-artifact policy.

## Honest residual

Inspection of the axial-hole target shows cylindrical bore surfaces in both disconnected solids. The assembly reconstruction therefore must not pretend the hole belongs only to the stem. X1 expresses the stem exactly as an annular extrusion, but the public `Hole` host set does not include `Sphere`; this leaves raw IoU `0.9409`. Closing that gap responsibly requires a separately justified sphere-cylinder subtraction or a general revolved-section capability. This milestone stops at that precise boundary instead of adding brittle record-specific geometry.
