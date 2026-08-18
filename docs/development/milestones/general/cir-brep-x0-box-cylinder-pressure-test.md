# CIR-BREP-X0 / X0.1: bounded box-cylinder shell merge pressure test

Purpose: internal-only pressure-test diagnostics for canonical `Subtract(Box,Cylinder)` over existing planar/cylindrical emission + token pairing + stitch planning + remap + bounded shared-edge rewrite.

## Scope / guardrails
- No production materializer path replacement.
- No duplicate-edge cleanup mutation.
- No vertex-merge mutation.
- No STEP exporter behavior changes.
- No generated topology naming exposure (SEM-A0 preserved).

## Required stage report (X0.1)
The pressure-test result now always reports all stages with explicit status:
- InputValidation
- PlanarPatchEmission
- CylindricalWallEmission
- TokenPairingAnalysis
- StitchCandidatePlanning
- CombinedBodyRemap
- SharedEdgeRewrite
- DuplicateEdgeCleanup (diagnostic deferred)
- VertexMergePlanning (diagnostic deferred)
- LoopClosureValidation
- ShellClosureValidation
- BrepBodyValidation
- StepExportSmoke

## Diagnostics payload now includes
- Topology counts: `FaceCount`, `LoopCount`, `EdgeCount`, `CoedgeCount`, `VertexCount`, `ShellCount`, `CurveBindingCount`, `SurfaceBindingCount`.
- Token/stitch counts: `SafeTokenPairCount`, `MissingMateTokenCount`, `AmbiguousTokenCount`, `NullTokenEntryCount`, `StitchCandidateCount`, `ReadyStitchCandidateCount`, `AppliedStitchCount`, `DeferredStitchCandidateCount`.
- Edge/coedge usage categories: `EdgesWithZeroCoedges`, `EdgesWithOneCoedge`, `EdgesWithTwoCoedges`, `EdgesWithMoreThanTwoCoedges`, `CoedgesWithMissingEdge`, `LoopsWithMissingCoedge`, `FacesWithMissingLoop`.
- Bounded sampled IDs in diagnostics for non-zero problematic categories.

## Observed run policy
X0.1 records concrete blockers from observed data (for example `edges-with-one-coedge`, `shell-not-proven-closed`, `step-smoke-skipped-shell-not-closed`) rather than vague “may stop” language.

## Next milestone guidance
Based on X0.1 blocker output, prioritize:
1. bounded duplicate-edge cleanup contract,
2. bounded vertex-merge planning/execution contract,
3. closure re-validation before STEP smoke enablement.
