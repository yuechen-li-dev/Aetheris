# CIR-F15.2 — real planar retained-circle evidence promotion for `Subtract(Box,Cylinder)`

## Root cause

Bucket **A (binder diagnostics/evidence visibility gap)**: base-side planar candidates already invoked retained-loop circle binding, but diagnostics did not clearly prove binder invocation/opposite selection/token propagation in the real patch-set path. This made F15.1 investigations appear as if exact-ready retained circle geometry was absent.

## Promotion rule in this milestone

For each base-side planar candidate, retain canonical circle evidence only when all gates hold:

- trim family is `Circle`,
- loop status is `ExactReady` or `SpecialCaseReady`,
- source is planar with bounded geometry,
- opposite source is cylindrical,
- planar normal / cylinder axis relation is circular-safe,
- cylindrical evidence has finite radius,
- deterministic ordering token exists.

Otherwise diagnostics remain explicit and conservative.

## What changed

- Per-loop diagnostics now include binder invocation outcome and opposite provenance (`binder-opposite=...`, `loop-geometry-bind-success`/`loop-geometry-bind-skip`).
- Shell assembler now reports cylindrical boundary token presence vs explicit missing-token state.
- Focused tests now assert real evidence flow from `EmitSupportedPlanarPatches(Subtract(Box,Cylinder))` through planar token emission and shell visibility.

## Safety gates preserved

- No geometry/token fabrication.
- Deferred/unsupported readiness still skips.
- Multi-inner-loop planar emission still skipped.
- Non-planar and non-cylindrical opposite pairings still skipped.

## Shell assembler visibility

`SurfaceFamilyShellAssembler` remains visibility-only. It reports planar/cylindrical emitted token candidates and matching dry-run token keys, but performs no shell merge/stitching.

## SEM-A0 status

Preserved: internal-only evidence diagnostics and identity metadata; no generated user-facing topology naming.

## Recommended F16 next step

Implement topology merge/stitch execution only after adding deterministic multi-boundary token disambiguation rules (especially for cylindrical top/bottom boundary correspondence) and explicit conflict diagnostics.
