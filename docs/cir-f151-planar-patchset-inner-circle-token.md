# CIR-F15.1 — real planar patch-set inner-circle token propagation

## Root cause

In the real `PlanarSurfaceMaterializer.EmitSupportedPlanarPatches(...)` path for `Subtract(Box,Cylinder)`, inner-circle patch emission already occurred when canonical retained circular loop evidence existed, but entry-level diagnostics were too weak to distinguish why candidates were skipped and whether emitted inner-circle topology carried a token.

This made the gap look like missing emission/token propagation even when emission occurred, and blocked confident downstream visibility checks.

Root-cause bucket: **G (diagnostic precision gap)**.

## Fix scope

- Tightened planar patch-set diagnostics with explicit skip categories:
  - candidate role mismatch,
  - readiness gate,
  - missing rectangle geometry,
  - missing retained-circle evidence,
  - multiple inner loops.
- Added explicit emitted diagnostics for inner-circle token attachment vs token-missing cases.
- Preserved conservative behavior (no readiness bypass, no multi-loop emission, no non-planar emission).

## Identity propagation behavior

For base-side planar exact-ready candidates with one canonical retained circular loop, the real patch-set path emits a rectangular face with one inner circular loop and retains `InternalTrimIdentityToken` in emitted topology identity metadata (inner edge/coedge/loop entries).

## Shell assembler visibility

No shell merge was added.

`SurfaceFamilyShellAssembler` continues dry-run visibility only, now with stronger diagnostics exercised in tests for:
- planar inner-circle token visibility,
- cylindrical token role visibility,
- emitted token match-candidate visibility.

## Non-goals preserved

- No shell assembly/merge/stitch implementation.
- No STEP export behavior change.
- No boolean behavior expansion.
- No generated user-facing topology naming (SEM-A0 preserved).

## Recommended F16 next step

Consume emitted identity maps + dry-run planned pairings in a bounded stitch planner that proves one deterministic shared-trim closure action for `Subtract(Box,Cylinder)` without introducing coordinate-based matching heuristics.
