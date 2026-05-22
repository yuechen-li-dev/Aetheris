# CIR-F16 — Emitted token pairing analysis before stitching

CIR-F16 adds a deterministic internal diagnostics layer over emitted topology identity metadata so future shell stitching can distinguish safe one-to-one token matches from missing, ambiguous, or incompatible cases.

## Why this exists

CIR-F15 bridged emitted topology to `InternalTrimIdentityToken`. CIR-F16 hardens that bridge by classifying multiplicity and compatibility before any stitch execution.

## Multiplicity/status rules

For each non-null token key:

- exactly 2 entries + compatible roles → `SafePair`
- exactly 1 entry → `MissingMate`
- more than 2 entries → `AmbiguousMultiplicity`
- exactly 2 entries with incompatible roles → deferred diagnostic (not safe)

Entries with null token are not paired and are reported as unmapped/internal-only diagnostics.

## Role compatibility (current bounded policy)

Safe emitted-token pairing is currently accepted only for:

- `InnerCircularTrim` + `CylindricalTopBoundary`, or
- `InnerCircularTrim` + `CylindricalBottomBoundary`.

Any other role pair remains deferred with explicit incompatibility diagnostics.

## Canonical subtract(box,cylinder)

The analyzer consumes emitted identity maps from:

- `PlanarSurfaceMaterializer.EmitSupportedPlanarPatches(...)`
- `CylindricalSurfaceMaterializer.EmitRetainedWall(...)`

Expected result is either:

- at least one `SafePair`, or
- an exact `MissingMate` diagnostic with the token key.

## SEM-A0 boundary

CIR-F16 is evidence-only:

- no shell merge,
- no coedge mutation,
- no edge merge,
- no STEP/export behavior change,
- no public topology naming.

## Recommended next step

Use `EmittedTokenPairingAnalysisResult` as the admissibility gate for future bounded stitch-candidate generation. Keep >2 multiplicity and incompatible roles as explicit deferred blockers unless a bounded disambiguation strategy is introduced.
