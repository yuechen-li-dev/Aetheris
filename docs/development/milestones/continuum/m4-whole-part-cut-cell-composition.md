# M4 whole-part Cut-cell composition

## Outcome and scope

M4 establishes the first complete closed-solid composition path for a bounded convex planar exact BRep shell. The production proof is an oriented exact BRep box associated with an analytic/SDF-capable CIR. On the fixed 16³ lattice it produces 912 Cut cells: 768 `SingleFace`, 136 `TwoFaceEdge`, and 8 `ThreeFaceCorner`. Deterministic exact local clipping recovers volume and boundary area to floating-point tolerance and invokes `JudgmentEngine` zero times.

This is meaningful whole-solid progression, not completion of every requested support family. Plane/Cylinder/Cone/Torus trimmed whole-shell integration is not complete. The M3 root-fillet fixture cannot honestly serve as the M4 whole-part fixture: its CIR is the simplified closed shoulder/shaft envelope, while its associated production BRep contains additional prism, cone, cylinder, and frustum sections. The new BRep/CIR consistency gate rejects that mismatch. HexBolt and CTC-01 therefore were not attempted.

## Authority split

- BRep owns exact body/shell/face/edge/vertex identity, support geometry, trims, adjacency, and semantic face provenance.
- CIR owns `Inside`, `Outside`, `Boundary`, and occupied-material side. SDF sign and magnitude are recorded when `ISignedDistanceCapability` is present; exact distance is not required by the compositor.
- `BoundaryOffsetMap` remains a derived patch cache. `CutCellBoundarySet.CompositeBoundaryMaps` permits multiple independent maps and does not require a single height field.
- `JudgmentEngine` is reserved for competing bounded interpretations after direct topology rules fail. It selects an interpretation and records evidence/rejections; it never creates geometry.

`FaceGeometryBinding.SameSense`, coedge reversal, shell traversal, and STEP orientation metadata remain topology/orientation evidence. They may orient the axis along which two CIR probes are made. They never choose the occupied side. The occupied direction is whichever of `P + epsilon*N` or `P - epsilon*N` CIR classifies as material.

## Pre-M4 gap audit

Before this change, `ContinuumGridClassifier` attached `GeometrySamplePlan.BoundaryCandidates` to a `CutCell`. Those candidates were lightweight `BoundaryReference` values supplied by each analytic region. There was no shell/body association, bounded whole-shell query, exact trim candidate index, edge/vertex relationship, composition kind, material-side evidence object, or multi-face integration result.

M3 manually sampled the torus parameter domain to discover root-fillet cells. It selected one torus support per cell except at seams, stored multiple maps only as an unstructured list, and used a hard-coded `exactFaceNormalIsMaterialSide` construction fact. Contact cells fell back to dense CIR sampling. Thus face identity existed, but edge/corner neighborhoods were not composed and no whole closed BRep was integrated.

The orientation audit found no Continuum inside/outside path based directly on STEP `same_sense`; CIR already owned point occupancy. The leak was narrower: `ExactBrepBoundaryQuery.ExactFaceNormal` applies `SameSense`, and M2/M3 support code then negated or accepted that normal using fixture knowledge. CLI section-analysis code also uses `SameSense` to orient 2D material-left fragments. That CLI path is topology/section reconstruction, not Continuum occupancy, and was not broadly redesigned in M4.

## New design

`CirBrepAssociation` minimally binds a continuum region ID to BRep body/shell identity and optional semantic model identity. `BoundaryReference` now preserves these identities plus face semantic provenance.

`WholeShellBoundaryQuery` indexes faces once. It resolves exact trim vertices from BRep vertex bindings or exact edge trim geometry, stores support kind, bounds, edge/vertex incidence, face adjacency, `SameSense` evidence, and semantic identity. Planar candidates are further clipped by their exact outer trim polygon against the cell bounds, avoiding a global face scan and AABB-only false positives. Curved-face indexing remains bounded by exact trim/support data and needs the next support-specific refinement.

`CutCellBoundarySet` contains contributors, composition kind, local CIR classification, optional judgment trace, and integration result. Each contributor retains its exact face reference, support kind, trim topology incidence, CIR-derived material-side evidence, and optional local map. The bounded composition kinds are:

- `SingleFace`
- `TwoFaceEdge`
- `ThreeFaceCorner`
- `MultiFaceTrimJunction`
- `FilletContact`
- `GeneralBoundedMultiFace`

One face, an adjacent pair, and a three-face shared vertex take direct paths. Torus plus Plane/Cylinder is classified as `FilletContact`. Shared-edge multi-face junctions and general overlapping bounded interpretations are the only current judgment candidates.

## Consistency and probing

Construction runs deterministic CIR probes at every exact face centroid, edge midpoint, and vertex, plus known interior and exterior points. Any material disagreement fails explicitly before composition. The tolerance accounts for the current `Transform3D` single-precision matrix storage.

For planar faces, the probe distance is scale-relative and clamped. The BRep oriented normal supplies only an axis. CIR classifications on both sides select the material-side normal; SDF values add confidence/proximity evidence. If exactly one side is not material, the result is resolved. An unresolved planar result fails construction rather than falling back to `SameSense`.

Near edges and corners, material occupancy is the intersection of all CIR-resolved face half-spaces. The compositor therefore does not multiply orientation flags or independently integrate faces.

## Structured integration

The admitted planar whole-shell path clips the Cartesian cell polyhedron successively by all exact CIR-oriented BRep face half-spaces. The resulting convex polyhedron gives exact deterministic occupied volume. Cap polygons retain their source face ID, so boundary area is accumulated once per exact face with no edge/corner double counting. This is a local Cut-cell decomposition, not AMR, a CSG mesher, or SurfaceMeshIR authority.

Non-planar cells currently have an explicit bounded 12³ CIR fallback and zero inferred boundary area. That path is diagnostic scaffolding, not a claim that Cylinder/Cone/Torus M4 composition is complete. Existing M1–M3 single-face map paths remain unchanged and do not incur whole-shell or JudgmentEngine overhead.

## Local evidence

Evidence is generated locally under `docs/development/milestones/continuum/artifacts/m4/` by running
`dotnet run --project tools/Aetheris.Continuum.M4/Aetheris.Continuum.M4.csproj -f net10.0`.
Generated JSON diagnostics are intentionally ignored by Git; use `git add -f` only when a small,
reviewable artifact has a specific reason to become a source-controlled baseline.

- `benchmark-summary.json`
- `whole-part-diagnostics.json`
- `composition-kind-counts.json`
- `material-side-evidence.json`
- `judgment-traces.json`
- `adversarial-orientation-tests.json`
- `orientation-matrix.json`
- `fixed-vs-fine-comparison.json`
- `deterministic-hashes.json`

The baseline 16³ run reports volume 3.0000000000000684 versus exact 3, area 12.999999999999915 versus exact 13, and zero JudgmentEngine calls. Baseline, single-axis rotation, and compound rotation all retain single-face, edge, and corner cells without classification collapse. A controlled body with every `SameSense` bit reversed still resolves all six occupied sides from CIR/SDF and preserves exact face identity.

The fixed exact-composition run is compared with a 32³, 4³-per-Cut-cell brute-force occupancy control. Timings separate setup/material probing, cell classification, candidate discovery, and composition/integration. Repeated geometry projections produce identical SHA-256 hashes; runtime timings are intentionally excluded from the hash.

## Limitations and M5 recommendation

The next geometry milestone must supply a CIR that exactly corresponds to the complete production root-filleted BRep, then add exact trimmed Plane/Cylinder/Cone/Torus projection/domain queries and structured multi-patch union/intersection integration. That is the isolated blocker for root-fillet contacts, HexBolt, and support-family area totals. A generic BRep-to-sampled-SDF conversion is neither needed nor proposed.

Conventional linear elasticity should begin only after that non-planar whole-shell seam closes. At that point, M5 can add the first sparse linear-elasticity solve on the completed whole-part domain and an Abaqus-compatible verification/export seam.
