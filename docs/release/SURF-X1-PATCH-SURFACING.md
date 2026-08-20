# SURF-X1 — patch surfacing and bounded replacement

## Executive verdict

**Meaningful progression.** Aetheris can now perform one real bounded freeform surface replacement through the authoritative `BodyState -> GeometricDelta -> verified BRep -> STEP` path. The canonical housing has a genuine trimmed non-rational B-spline crown, explicit four-edge correspondence, mixed G0/G1 checks, zero deviation below the authorized top region, shared topology, deterministic export, successful production reimport, and zero rational product surfaces.

SURF-X1 is not fully accepted against the complete milestone: general surface/support intersection trimming, extension beyond an existing knot support, inner loops, imported-face replacement, AP242 PMI/Assembly Interface association persistence, and fresh-agent qualification remain unimplemented. Those gaps are stated rather than represented by a generic NURBS or whole-body fallback.

## Patch architecture

`BoundedSurfacePatch` distinguishes mathematical support from a body. `AnalyticSurfacePatch` retains its elementary `SurfaceGeometry`; `BSplineSurfacePatch` wraps the existing weight-free `BSplineSurfaceWithKnots`. Both carry a stable patch ID, parameter domain, orientation, and one explicit `SurfaceBoundaryLoop`. Public `SurfacePatch` Firmament authoring currently admits only the non-rational tensor-product form and requires degree, complete finite rectangular control net, expanded knots, domain, and four named boundary contracts.

`ReplaceRegionOperation` consumes one predecessor, target region, patch, authorized entities/envelope, preservation contracts, and postconditions. Validation completes before a successor exists. The housing materializer creates a preserved planar top frame and a separately bound spline face. Its four edges and vertices are shared topology with the frame. It is not a Boolean and cannot replace arbitrary imported faces.

`TrimRegion` restricts a B-spline patch to a subdomain already inside its knot support. `ExtendRegion` can enlarge that domain only within the same support; extrapolation fails with `surf-extend-law-unsupported`. General surface-surface intersection and trim classification remain the next geometric blocker.

## Flagship housing

- Source: [`surf-x1-freeform-housing.firmament`](../../fixtures/Canonical/Sculpting/surf-x1-freeform-housing.firmament)
- Base state: `state-4826ec8df531eb593583`
- Replacement state: `state-6a688b75addb5403f978`
- Downstream-hole state: `state-a92a57779cb61433a0fb`
- Patch: `CrownPatch`, degree 3 x 3, 6 x 6 control net, domain `[0,1] x [0,1]`
- Bounds after production STEP reimport: `[-50,-40,0]` to `[50,40,26.26953125]` mm
- Topology after reimport: 1 body, 1 shell, 11 faces, 28 edges, 20 vertices; `enclosed-manifold`
- Authorized envelope: `[-30,-20,20]` to `[30,20,28]` mm
- Maximum realized-BRep deviation below `z=20`: `0 mm` at `1e-6 mm` tolerance

The replacement delta preserves `BottomMountingInterface`, `MountingHolePattern`, `OuterFootprintBoundary`, and `SideWallsLower`; replaces `HousingCrown -> CrownPatch`; and retains explicit South/East/North/West boundary correspondence. The current implementation introduces one crown patch, not separate transition patches.

## Continuity evidence

Each boundary is sampled at 33 deterministic parameters. G0 is point-to-exact-boundary-segment distance, not height alone. G1 compares the patch tangent-plane normal to the neighboring planar frame.

| Boundary | Contract | Maximum G0 error | Maximum G1 angular error | Tolerance |
|---|---|---:|---:|---:|
| South | G0 | 0 mm | not required | 1e-6 mm |
| East | G1 | 0 mm | 0.000574746 degrees | 0.1 degrees |
| North | G0 | 0 mm | not required | 1e-6 mm |
| West | G1 | 0 mm | 0.000574746 degrees | 0.1 degrees |

Every realized edge has exactly two coedge uses. Replacement boundaries use shared edge IDs and shared vertex IDs. The core BRep currently has no separate public pcurve object, so this milestone does not claim independent pcurve round-trip qualification.

## STEP surface inventory

| Artifact | Planes | Cylinders | Cones | Spheres | Tori | Non-rational B-splines | Rational surfaces |
|---|---:|---:|---:|---:|---:|---:|---:|
| Freeform housing | 6 | 4 | 0 | 0 | 0 | 1 | **0** |

The exporter emits one `B_SPLINE_SURFACE_WITH_KNOTS` and blocks `RATIONAL_B_SPLINE_SURFACE`. Production `Step242Importer` reimports the artifact and preserves the spline degree, control net, knots, closed shell, and surface-family inventory. The flagship uses no internal rational algorithm and no approximation. Existing normalization tests retain equal-weight rational plane -> `Plane`, equal-weight non-planar patch -> non-rational B-spline, and non-removable rationality -> blocking diagnostic. General conic rational recovery is not newly claimed by this lane.

## Downstream feature propagation

`CrownWithServiceHole` consumes `FreeformCrown`, resolves `OuterFootprintBoundary` in that current state, and adds exact cylindrical through-hole `H5`. It preserves `CrownPatch`, the four original individual hole identities, lower side walls, and four crown boundaries. Export contains the same single non-rational crown plus five cylinders and zero rational surfaces.

A direct attempt to replace historical `HousingCrown` again from the replacement state fails atomically with `surf-selector-target-replaced` and explains that `CrownPatch` replaced it. No successor is returned.

## Manual artifact

- STEP: [`surf-x1-freeform-housing.step`](../../artifacts/local/surf-x1-freeform-housing.step)
- Delta/evidence: [`surf-x1-freeform-housing.delta.json`](../../artifacts/local/surf-x1-freeform-housing.delta.json)
- SHA-256: `a8cf1b5b7c273cf89d5c2b265d7a105be705ece526781b4c4cf7876e36e49fdb`
- Size: 14,344 bytes

Inspect the overall crown shape, east/west tangent transitions, front/back positional transitions, unexpected creases, dents or warts, unchanged four-hole mounting pattern, unchanged bottom interface, unchanged lower side walls, and the four trim edges. The structured continuity evidence is authoritative; visual inspection is an additional aesthetic check.

## Qualification boundary and remaining blockers

Implemented regression coverage includes patch invariants, bounded Trim/Extend policy, `ReplaceRegion`, mixed G0/G1, locality, preservation, shared topology, failure atomicity, stale references, deterministic state/STEP, downstream hole propagation, rational prohibition, STEP export/reimport, structured CLI metadata, and interior B-spline bounds.

The next blocker is a topology-aware intersection/trim layer that can produce and classify non-rectangular trim curves and their parameter-space representation. Without it, broad `TrimRegion`, arbitrary support extension, inner loops, and imported-face replacement would be brittle special cases. Separately, sculpting state needs actual AP242 PMI and Assembly Interface association storage/rebinding before preservation parity can be claimed. G2, Blend, Loft, Shell, Draft, fitting, and fairness remain deferred by design.

## Qualification results

- zero-warning `Release` solution build: pass;
- focused SURF-X1 tests: 8 pass;
- solution test run: 3,126 pass, 1 unrelated load-sensitive display-fallback test fails during the parallel run and passes immediately in isolation;
- CLI suite, including structured X1 build/analyze and public Markdown links: 400 pass;
- canonical qualification: all 93 fixtures pass;
- client: 82 tests, production build, and lint pass;
- VS Code extension: 13 tests, typecheck, build, and VSIX package pass;
- fresh packaged CLI: builds and analyzes the flagship; STEP hash matches the in-tree CLI artifact;
- repository layout guard and `git diff --check`: pass.

Five context-free agents were restricted to `docs/public/` and `fixtures/Canonical/`. A selected `SurfacePatch` + `ReplaceRegion` and the full preservation/export contract. B selected G1 east/west and G0 north/south with the correct numerical checks. C selected immutable sibling branching; its initial use of `OffsetRegion` exposed missing guidance for two freeform-patch variants, which the public guide now corrects. D selected current-state `HoleFeature`, preserved `CrownPatch` and H1-H4, and explained the stale selector diagnostic. E selected validate/inspect/build/analyze and the one-non-rational/zero-rational checks; its ambiguity over whether `build` itself reimports prompted an explicit command-boundary clarification in the guide.
