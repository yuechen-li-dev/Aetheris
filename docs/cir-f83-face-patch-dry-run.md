# CIR-F8.3: Face patch dry-run candidates from source surfaces + trim capabilities

## Purpose

CIR-F8.3 introduces a **diagnostic-only dry-run layer** that bridges:

- `SourceSurfaceExtractor` inventory, and
- `TrimCapabilityMatrix` policy facts,

into early `FacePatchDescriptor` candidate generation.

This milestone intentionally **does not emit BRep topology**.

## New dry-run artifact

`FacePatchCandidateGenerator.Generate(CirNode root, NativeGeometryReplayLog? replayLog = null)` returns a `FacePatchCandidateGenerationResult` containing:

- extracted source surfaces,
- dry-run face patch candidates,
- trim capability summaries derived from source-family pairings,
- readiness classification and deferred reasons,
- explicit topology-assembly-not-implemented diagnostics.

## Candidate readiness states

- `ExactReady`: trim capability is `ExactSupported` or `SpecialCaseOnly`; retention classification remains deferred.
- `TrimDeferred`: trim capability is recognized but deferred by matrix policy.
- `Unsupported`: no matrix capability / unsupported source pair for current dry-run.

`RetentionDeferred` is represented by explicit diagnostics while readiness remains trim-focused.

## What is exact-ready vs deferred

### subtract(box, cylinder)

- Source inventory includes planar and cylindrical surfaces.
- Planar/cylindrical capabilities are surfaced as `SpecialCaseOnly` from matrix policy.
- Candidate readiness can be `ExactReady` with retention still deferred.

### subtract(box, sphere)

- Source inventory includes spherical surfaces.
- Planar/spherical capability is `ExactSupported` with circle trims.
- Candidates surface exact trim readiness with retention deferred.

### subtract(box, torus)

- Source inventory includes toroidal surfaces.
- Planar/toroidal and toroidal pairings are surfaced as `Deferred`.
- Candidates are flagged as `TrimDeferred` with matrix reason (quartic/algebraic intersections).

## Why this is not BRep emission

The generator creates descriptor-level candidates only:

- no face/edge/coedge/vertex creation,
- no topology assembly,
- no STEP export behavior changes,
- no pair-specific subtract materializer expansion.

## SEM-A0 guardrails

No topology naming generation is introduced. Provenance remains descriptor-level.

## Next milestone (CIR-F8.4)

Add bounded retention classification for subtract trees (base/tool/trimmed-retained candidate subsets) while still avoiding full topology assembly until classification and boundary contracts are explicit.
