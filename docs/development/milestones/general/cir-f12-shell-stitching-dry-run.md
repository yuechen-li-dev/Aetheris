# CIR-F12 — Shell stitching dry-run / coedge pairing validation (`Subtract(Box, Cylinder)`)

## Purpose

CIR-F12 adds a bounded, diagnostic-first **shell-stitching dry-run** that consumes real emitted patch evidence from:

- planar retained base patch-set emission (`PlanarSurfaceMaterializer.EmitSupportedPlanarPatches`), and
- retained cylindrical wall emission (`CylindricalSurfaceMaterializer.EmitRetainedWall`).

It validates stitching evidence only. It does **not** assemble a shell/body and does not change STEP/export behavior.

## Dry-run model

`ShellStitchingDryRunPlanner.Generate` returns `ShellStitchingDryRunResult` with:

- `PlannedPatches` inventory (planar + cylindrical pieces),
- `PlannedPairs` inferred from `InternalTrimIdentityToken` evidence,
- `UnpairedBoundaries` with explicit reasons,
- `Readiness` reduced to `ReadyForAssemblyEvidence/Deferred/Unsupported`, and
- `ShellAssemblyImplemented=false`.

## Pairing / closure evidence behavior

- Pairing is promoted from existing `TopologyPairingEvidenceGenerator` `SharedTrimIdentity` evidence.
- Boundaries without one-to-one token matches remain unpaired and are reported as deferred.
- Cylindrical seam closure is explicitly recorded as deferred diagnostic-only accounting in this milestone.
- Orientation compatibility is currently conservative (`Compatible` only when loop policies match, else `Deferred`).

## Expected box-cylinder outcome

For `Subtract(Box,Cylinder)`, CIR-F12 is expected to report:

- emitted planar retained patch entries,
- emitted (or explicit deferred) cylindrical retained wall patch entry,
- at least one token-driven pairing where evidence exists,
- remaining unpaired/deferred boundaries where closure proof is incomplete,
- `ShellAssemblyImplemented=false`.

## What remains before real shell assembly

- Explicit seam self-closure policy and accounting upgrade for cylindrical side seam.
- Stronger orientation compatibility proof across planar cavity loops and cylindrical mouth loops.
- Boundary completeness proof that all external/open boundaries are consumed by pairing or closure rules.
- Actual BRep shell/body stitcher (out of CIR-F12 scope).

## JudgmentEngine usage decision

JudgmentEngine was **not** introduced in CIR-F12:

- pairing selection reuses deterministic one-to-one token evidence already resolved by prior dry-run stages,
- closure readiness is a deterministic reduction over patch/pair/unpaired states,
- no competing bounded candidate strategies were introduced in this milestone.

## SEM-A0 guardrail status

SEM-A0 remains preserved:

- no generated topology naming,
- no public topology identity surface changes,
- internal diagnostics/evidence only.

## Recommended next step

CIR-F13 should add deterministic seam accounting + orientation proof policy for planar/cylindrical loop pair compatibility, then re-evaluate readiness promotion criteria before any shell assembler implementation.
