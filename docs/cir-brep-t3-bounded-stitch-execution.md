# CIR-BREP-T3: bounded stitch execution behind stitch-candidate gate

CIR-BREP-T3 introduces a first bounded stitch execution entrypoint for `Subtract(Box,Cylinder)` emitted planar/cylindrical patches.

## Purpose

Attempt deterministic stitch execution only from CIR-BREP-S0 `SurfaceFamilyStitchCandidate` output, while preserving behavior outside this internal bounded path.

## Stitch gate

`SurfaceFamilyStitchExecutor.TryExecute(...)` enforces:

- no candidates => no mutation,
- only `Readiness=Ready` + `Kind=SharedTrimIdentity` considered,
- orientation policy must be compatible/convention-safe,
- deferred/ambiguous token associations are rejected,
- candidate entries must resolve to emitted local topology keys.

## Topology support diagnosis

Current emitted identity contract carries only `LocalTopologyKey` strings with role/orientation + token bridge metadata.
It does not carry concrete coedge/loop/face ids needed to safely mutate shared-edge/coedge relationships across independent emitted patch bodies.

Because each patch emission currently owns its own minimal `BrepBody`, there is no bounded cross-body remap contract in CIR-BREP-T3 for deterministic id merge/pairing.

## Execution strategy chosen

T3 selects **Outcome B (meaningful progression)** strategy:

- execution scaffold is real and invoked over real canonical inputs,
- candidate gating is strict and deterministic,
- executor returns precise unsupported/deferred blockers instead of guessing topology merges,
- no fake shell assembly is produced.

## Behavior guarantees

- `FullShellClaimed` is always `false` in T3.
- `StepExportAttempted` is always `false` in T3.
- Diagnostics explicitly report start, gate decisions, blockers, and non-claims.

## JudgmentEngine decision

JudgmentEngine is not used in T3 because candidate handling is deterministic gated reduction with no admissible competing strategy scoring.
Ambiguous token multiplicity is deferred, not scored.

## Next milestone

Add bounded emitted topology contract extension carrying concrete emitted face/loop/coedge/edge identifiers and a safe cross-body id remap pathway, then attempt one true shared-edge stitch mutation.
