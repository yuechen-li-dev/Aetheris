# SURF-X1a trim/intersection closure

## Executive verdict

**Accepted** for the explicitly bounded SURF-X1a housing class. SURF-X1 is no longer blocked on trim topology, pcurves, circular inner loops, bounded extension, imported-face support replacement, or AP242 geometry-association persistence. General surface networks remain deferred.

## Intersection capability matrix

| Surface A | Surface B | Status | Curve class | Notes |
|---|---|---|---|---|
| Plane | Plane | Qualified | exact line | Transverse line; parallel no-intersection and coincident-region classifications are distinct. |
| Plane | Cylinder | Qualified subset | exact circle | Plane normal parallel to cylinder axis and bounded axial parameter. General oblique ellipse/two-line cases are not claimed. |
| Plane | non-rational B-spline | Qualified subset | exact non-rational B-spline | Clamped boundary isoparametric intersections used by the flagship outer loop. |
| Cylinder | non-rational B-spline | Qualified trim association | exact circle | Existing shared circular edge and both pcurves are qualified; general intersection discovery is deferred. |
| non-rational B-spline | non-rational B-spline | Deferred | — | No arbitrary surface-network claim. |

`SurfaceIntersectionResult` retains support identities, classification, 3D branches, two pcurves, orientation, tolerances, and diagnostics. Multiple-branch selection uses `JudgmentEngine`; an unseeded ambiguous set fails instead of choosing returned order.

## Pcurve and topology evidence

The flagship has 24 topological edges and 48 face-local pcurves: exactly two pcurves per shared edge. AP242 contains 24 `SURFACE_CURVE` and 48 `PCURVE` entities. `BrepPcurveValidator` independently checks domain, orientation, and off-grid sampled `Surface(UV(t))` versus `Curve3D(t)`; the measured maximum reconstruction deviation is `3.935614021344236e-6 mm` against `1e-5 mm`, with valid domains and consistent orientation. A deliberately corrupted pcurve fails while ordinary BRep preflight still passes, proving this is an independent gate.

The replacement B-spline face has five loops: one outer loop and four single-circle inner loops. Each inner circle is one shared topological edge used by the crown and its preserved cylindrical hole wall. The whole body has 10 faces, 24 edges, 16 vertices, and every edge has incidence two.

## Extension

Plane, Cylinder, and Cone use exact analytic continuation. Non-self-intersecting non-rational B-splines of degree at most three use bounded endpoint first-derivative continuation, report the original/extended domains and C1 boundary law, and are limited to 25% of the original span per side. The extended support is an intersection aid; the final solid remains bounded by the trim and authorized spatial envelope. Unsupported or excessive requests fail with `surf-extension-unsupported`.

## Imported replacement

The regression exports the canonical base, reimports its real STEP BRep, selects the top through `FaceGeometryBinding.SourceStepEntityId`, and invokes `ImportedFaceRegionReplacer.Apply`. The result retains the exact imported `TopologyModel` object, vertices, edge curves, neighbor bindings, and neighboring STEP face provenance. Only the selected face's support binding is succeeded by the non-rational B-spline, after which all pcurves and BRep preflight are requalified. Reusing the historical imported selector fails with `surf-selector-target-replaced` and explicit successor provenance.

## PMI and interface persistence

Before and after replacement the state carries explicit current-face associations:

| Semantic object | Before | After | AP242 reinspection |
|---|---:|---:|---:|
| Datum A / bottom interface | 1 face | 1 face | 1 associated `ADVANCED_FACE` |
| Hole diameter, quantity 4 | 4 cylindrical faces | 4 cylindrical faces | 4 associated `ADVANCED_FACE` entities |
| Hole-pattern position to A | 4 cylindrical faces | 4 cylindrical faces | 4 associated `ADVANCED_FACE` entities |
| Bottom assembly interface | 1 face | 1 face | 1 associated `ADVANCED_FACE` |

STEP uses `GEOMETRIC_ITEM_SPECIFIC_USAGE`; reinspection reports one datum, one diameter dimension, one position tolerance, one assembly-interface annotation, and non-empty `GeometricFaceEntityIds` for each. The qualified path carries associations only through explicit `Preserved` correspondence. A removed target, or a `ReplacedBy` target without an explicitly supported face successor, fails instead of invoking name/proximity matching; successor-target PMI remains deferred.

## Product boundary and artifact

Artifact: `artifacts/local/surf-x1a/surf-x1a-trimmed-freeform-housing.step`

| Evidence | Value |
|---|---:|
| SHA-256 | `09348ca464547b7f9875074f9e2e4e3d32318e41c37bc46f9ca75bc0a938b052` |
| Bytes | 2,464,434 |
| Bodies / shells | 1 / 1 |
| Faces / edges / vertices | 10 / 24 / 16 |
| Bounds | `[-50,-40,0]` to `[50,40,27.14236111111111] mm` |
| Planes / cylinders | 5 / 4 |
| Non-rational B-spline surfaces | 1 |
| Rational product surfaces | **0** |
| STEP reimport | enclosed manifold |

The four boundary G0 errors are at most `1.78e-14 mm`; G1 angular error is at most `0.000329°` against a `0.1°` limit. Independent locality below `z=20 mm` reports maximum deviation `0`.

Imported rational surfaces use strict normalized re-export (Policy A): foreign rational supports are not passed through into a newly authoritative Aetheris STEP. They must normalize to an admitted analytic/non-rational form or the path blocks. The canonical source and result are rational-free.

The recorded CLI build timings on the flagship machine were approximately 153 ms base preparation, 1.4 ms operation construction, 379 ms locality/preservation and trim realization, and 60 ms STEP export. Microphase timers for intersection, branch classification, pcurve inversion, loop construction, extension, grafting, and association remapping are not yet separately exposed in CLI JSON; their deterministic functional tests are qualified independently.

## Fresh-agent tests

Fresh documentation-only audits were rerun after the public guide, cookbook, coverage map, and diagnostic reference were updated:

| Test | Result |
|---|---|
| A — imported top-face crown trimmed against neighbors | Pass (advanced imported-BRep API path) |
| B — preserve four through-holes | Pass (canonical X1a Firmament witness) |
| C — preserve hole PMI and bottom datum | Pass |
| D — preserve bottom assembly interface | Pass |
| E — reject stale replaced selector | Pass |
| F — export and independently reinspect STEP | Pass |

## Validation

- Focused SURF-X0/X1/X1a module tests: 25 passed.
- Focused STEP exporter/importer/semantic-PMI tests: 53 passed.
- Full serial .NET suite: 3,135 passed, zero failed.
- Canonical qualification: 94 fixtures passed.
- Canonical X1a `validate`: valid, zero diagnostics.
- CLI `build`: success; STEP plus sibling `GeometricDelta` JSON.
- CLI `analyze`: 1 enclosed body, expected topology/surface inventory, complete semantic PMI associations.
- Client: 82 tests plus typecheck, production build, and lint passed.
- VS Code extension: 13 tests plus typecheck, build, and VSIX package passed.
- Fresh self-contained win-x64 CLI: version and canonical validation passed.
- Repository layout guard (3,719 tracked files), changed-document Markdown links, and `git diff --check`: passed.
- NativeAOT Forge was not rerun because neither Forge nor its host/interop boundary changed.

## Deferred work

General G2 continuity, fairness optimization, general Blend, variable-radius transitions, Loft, Shell, Draft, arbitrary Cylinder/B-spline and B-spline/B-spline intersection discovery, nested islands, and global arbitrary surface networks remain outside SURF-X1a.
