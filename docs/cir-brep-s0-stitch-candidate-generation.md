# CIR-BREP-S0: stitch candidate generation from emitted topology identity maps

## Purpose

CIR-BREP-S0 adds deterministic, diagnostic-first stitch-candidate planning from emitted identity maps and CIR-F16 token-pairing analysis. It does **not** stitch or merge topology.

## Inputs

- `EmittedTopologyIdentityMap` entries from:
  - `PlanarSurfaceMaterializer.EmitSupportedPlanarPatches(...)`
  - `CylindricalSurfaceMaterializer.EmitRetainedWall(...)`
- `EmittedTokenPairingAnalysisResult` from CIR-F16.
- Optional `ShellStitchingDryRunResult` for seam/orientation evidence.

## Safe-pair policy

Only CIR-F16 `SafePairs` become `SurfaceFamilyStitchCandidate` entries (`SharedTrimIdentity` kind). Candidate carries both emitted entries and token.

## Missing / ambiguous behavior

- Missing mate groups are deferred diagnostics only.
- Ambiguous multiplicity groups are deferred diagnostics only.
- Incompatible role groups are deferred; no arbitrary pairing/selection.

## Orientation policy

- If dry-run orientation says compatible, candidate is `Ready`.
- If orientation evidence is absent, candidate can be accepted by existing planar-inner/cylindrical-boundary convention; otherwise deferred.
- Incompatible/deferred orientation remains deferred with explicit diagnostics.

## Seam policy

Seam evidence from dry-run is surfaced as diagnostics (`seam-accounted` or `seam-deferred`). No seam mutation occurs.

## Execution boundary

`SurfaceFamilyStitchPlanResult.StitchExecutionImplemented` is always `false` in CIR-BREP-S0.
No coedge mutation, edge merge, shell/body assembly, STEP behavior changes, or Boolean behavior changes are introduced.

## Next step

CIR-BREP-T3 can consume ordered stitch candidates and deferred blockers to implement bounded topology merge execution behind explicit admissibility gates.
