# SURF-X0 — geometric state and locality

## Executive verdict

**Accepted.** Aetheris has a trustworthy execution model for the admitted bounded mathematical-sculpting lane. The verdict is intentionally limited to the rectangular-housing `OffsetRegion` witness; it is not a claim of general surfacing.

## State model

`BodyState` is an immutable semantic record containing a deterministic `StateId`, optional single `PredecessorStateId`, stable body identity, BRep, construction state, semantic inventory, delta, and validation evidence. `Base` is `state-8960030e57e7b7d897d9`; the 6 mm state is `state-8f9f9011f77eb90790aa`. Both the 6 mm and 10 mm fixtures derive from `Base`, proving branching without parent or sibling mutation. Construction is attempted, verified, and only then returned as accepted state; failures return no output state.

The full chain is recoverable deterministically from Firmament source. No runtime object identity or hidden predecessor object is serialized as semantic authority.

## GeometricDelta

The flagship delta records:

| Classification | Semantic entities |
|---|---|
| Preserved | `BottomMountingInterface`, `MountingHolePattern`, `OuterFootprintBoundary` |
| Replaced | `HousingCrown` -> `HousingCrown@state-8f9f9011f77eb90790aa` |
| Introduced | `CrownTransitionZone` (four analytic G0 transition planes) |
| Removed | none |

The structured record also includes reads, authorized region, spatial envelope, and correspondence evidence. `aetheris inspect --json` shows both states; `aetheris build --json` writes [`surf-x0-sculpted-housing.delta.json`](../../artifacts/local/surf-x0/surf-x0-sculpted-housing.delta.json).

## Locality

- Authorized semantic domain: `HousingCrown`, `CrownTransitionZone`.
- Spatial envelope: `[-50,-40,20]` to `[50,40,26]` mm.
- Method: independent comparison of the two realized BReps below `z=20` (8 vertices, 5 plane supports, 4 cylinder supports, and 4 bottom circular trims), plus BRep preflight and edge-incidence enclosure verification.
- Evidence level: `ExactAnalytic` outside the modified crown and `CertifiedBounded` for result validity.
- Maximum observed deviation outside the domain: `0 mm` at `1e-6 mm` locality tolerance.

The negative locality fixture starts its declared envelope at `z=21`; it fails with `sculpt-outside-authorized-region`. An envelope reaching protected lower geometry fails before materialization.

## Preservation contracts

The bottom mounting interface retains its stable identity, `z=0` plane, 100 x 80 mm outer trim, and four circular inner trims. The hole pattern retains stable hole identities, axes, centers `(±20,±12)` mm, and 4 mm diameters. The upper cylindrical extent grows only inside the authorized crown domain. The lower outer footprint remains an exact 100 x 80 mm boundary.

This witness does not author AP242 PMI or an Assembly Interface object, so no PMI/interface round-trip claim is made. Persistence of those richer associations is classified `SURFX1`; their absence is not hidden behind a geometric-similarity claim.

## Surface representation

Public/product representations used by the flagship are `Plane` and `Cylinder`. Aetheris also has a non-rational `BSplineSurfaceWithKnots` product type with no weights. SURF-X0 adds an internal-only rational candidate normalization boundary to prove three behaviors: equal weights plus a bilinear planar net recover an exact `Plane`; equal-weight non-planar data recovers an exact non-rational `BSplineSurfaceWithKnots`; non-removable rationality produces `surf-surface-export-normalization-failed`.

STEP surface inventory after serialization and independent reimport:

| Artifact | Plane | Cylinder | Cone | Sphere | Torus | Non-rational B-spline | Other | Rational NURBS |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Base | 6 | 4 | 0 | 0 | 0 | 0 | 0 | **0** |
| Sculpted | 10 | 4 | 0 | 0 | 0 | 0 | 0 | **0** |

**Rational NURBS product surfaces: ZERO.** `SculptStepExporter` scans the emitted STEP text and blocks the artifact if `RATIONAL_B_SPLINE_SURFACE` occurs.

## Export normalization

The flagship is analytic throughout: internal planes -> STEP `PLANE`; internal cylinders -> STEP `CYLINDRICAL_SURFACE`. No approximation occurs. Regression witnesses cover internal equal-weight rational bilinear planar data -> exact `Plane`, and equal-weight non-planar data -> exact non-rational `BSplineSurfaceWithKnots`. Non-removable rational data has no fallback route.

## Flagship witness

- Source: [`fixtures/Canonical/Sculpting/sculpted-housing.firmament`](../../fixtures/Canonical/Sculpting/sculpted-housing.firmament)
- Generation: `dotnet run --project Aetheris.CLI -c Release -- build fixtures/Canonical/Sculpting/sculpted-housing.firmament --output artifacts/local/surf-x0/surf-x0-sculpted-housing.step --json`
- Base bounds: `[-50,-40,0]` to `[50,40,20]` mm
- Final bounds: `[-50,-40,0]` to `[50,40,26]` mm
- Base topology: 1 body, 1 shell, 10 faces, 24 edges, 16 vertices, enclosed manifold
- Final topology: 1 body, 1 shell, 14 faces, 32 edges, 20 vertices, enclosed manifold
- Changed region: top/crown at `z >= 20 mm`
- Preserved: bottom mounting interface, four-hole placement/diameter pattern, lower footprint
- Base SHA-256: `0852fa0bf3b69be53c0f073c846eb872a42e702ed5f3324ce7d7a7730f6198db`
- Final SHA-256: `be05e0547b116b52f99277183843457f2e7957bf87c5f02cd46b7e53e8eb2274`
- Reimport: both artifacts report `enclosed-manifold`; final surface inventory is 10 planes and 4 cylinders
- Determinism: repeated final build produced identical STEP SHA-256

Manual artifacts are [`surf-x0-base-housing.step`](../../artifacts/local/surf-x0/surf-x0-base-housing.step) and [`surf-x0-sculpted-housing.step`](../../artifacts/local/surf-x0/surf-x0-sculpted-housing.step). Inspect the intended raised crown, unchanged bottom and footprint, unchanged hole placement, four planar transitions, and any unexpected dents or warts.

## Fresh-agent tests

One context-free agent used only `docs/public/` and `fixtures/Canonical/`; it did not inspect implementation source or edit files. All four tasks succeeded on the first attempt:

- A selected `Base`, authored a 6 mm `OffsetRegion`, bounded `HousingCrown`, and preserved the mounting interface, hole pattern, and footprint.
- B authored `CrownHigh` as a sibling derived from `Base`, leaving the existing 6 mm state unchanged and inspectable.
- C used `Region: [60mm, 40mm]`, semantic `MayModify`, and the conservative transition-containing envelope. It correctly distinguished the crown plateau from the larger G0 influence volume.
- D used `aetheris build`, reinspected with `aetheris analyze`, and required analytic/non-rational product surfaces with `RationalNURBS = 0`.

The agent built and inspected all five canonical sculpting fixtures with exit code 0. The 6 mm artifact reimported as an enclosed manifold with maximum Z 26 mm, 10 planes, 4 cylinders, and no B-splines; the 10 mm sibling had maximum Z 30 mm with the same rational-free surface families. The one documentation friction item—explicit `[width, depth]` ordering and crown-plateau versus transition-envelope distinction—was corrected in the public guide.

## Qualification

Focused qualification covers state identity, single predecessor, branching, failure atomicity, locality, preservation, BRep enclosure/orientation, self-intersection rejection, deterministic STEP, product-boundary scanning, rational normalization/blocking, STEP reimport, and CLI build/inspect/validate structured output. Repository qualification completed with a zero-warning Release solution build; all 3,118 discovered .NET tests; all 92 canonical fixtures; 82 client tests plus build/lint; 13 VS Code extension tests plus typecheck/build/package; deterministic STEP comparison; and a freshly installed CLI NuGet tool that rebuilt the flagship successfully. Forge/NativeAOT was not affected by this Surfacing/CLI lane.

## Bugs and friction

| Classification | Finding |
|---|---|
| DocsFix | Existing public support material had no sculpting/effect-system vocabulary; the guide, CLI note, support matrix, and canonical cookbook now agree. |
| SURFX1 | Persist authored AP242 PMI and Assembly Interface associations through `BodyState` and verify reimport parity. |
| SURFX1 | Admit safe features after a sculpted state and extend semantic correspondence beyond the bounded housing inventory. |
| SURFX1 | Generalize locality comparison beyond exact rectangular analytic construction, using layered semantic/analytic/topological/CIR evidence. |
| Deferred | Non-rational freeform export with explicit approximation tolerance/error is existing surfacing infrastructure but is not exercised by `OffsetRegion` X0. |

## Deferred surfacing

General NURBS authoring, general freeform patches, general Trim/Extend, G2 network blending, Loft, arbitrary 3D deformation, global surface replacement, arbitrary imported-face sculpting, and a timeline UI remain unsupported. Aetheris does not claim full general surfacing.
