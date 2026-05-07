# CIR-F10.8 — Planar retained patch set emission (`Subtract(Box, Cylinder)`)

## Purpose

CIR-F10.8 adds a partial-surface emitter that materializes all **currently supported planar retained base patches** from real dry-run evidence for `Subtract(Box, Cylinder)`.

This milestone is intentionally bounded:

- emits planar base-side retained patches only,
- keeps readiness-gated emission (`no readiness, no emission`),
- preserves diagnostic-first behavior,
- does **not** assemble a shell/solid.

## API and result semantics

`PlanarSurfaceMaterializer.EmitSupportedPlanarPatches(CirNode root, NativeGeometryReplayLog? replayLog = null)` returns a planar patch-set result with:

- success flag,
- emitted standalone planar patch bodies,
- emitted and skipped counts,
- per-candidate emitted/skipped diagnostics,
- `FullMaterialization = false`,
- remaining blockers (including cylindrical side emission).

## Candidate filtering and emission categories

The patch-set emitter consumes dry-run candidates from `FacePatchCandidateGenerator.Generate(...)` and evaluates each candidate with explicit categories:

1. **Skipped non-planar or non-base candidates**
   - not `RetentionRole = BaseBoundaryRetainedOutsideTool`, or
   - not `SurfaceFamily = Planar`.

2. **Skipped readiness-blocked candidates**
   - candidate readiness is deferred/unsupported.

3. **Emitted untrimmed rectangular planar patch**
   - planar + base retained + rectangle geometry,
   - no retained canonical inner circular loops.

4. **Emitted rectangular planar patch with one inner circular loop**
   - planar + base retained + rectangle geometry,
   - exactly one canonical retained circular loop.

5. **Skipped unsupported planar candidates**
   - missing rectangle geometry,
   - multiple inner loops,
   - missing circular loop geometry for trimmed path,
   - readiness blocked.

## Expected `box - cylinder` outcome

For canonical subtract dry-run evidence:

- supported planar retained faces are emitted as standalone one-face bodies,
- at least one rectangular-with-inner-circle planar patch can emit when canonical loop evidence exists,
- cylindrical tool-side wall is not emitted and is reported as a remaining blocker,
- full materialization/shell assembly remains out of scope.

## Why this is not full materialization

CIR-F10.8 deliberately avoids:

- cylindrical side emission,
- multi-family surface completion,
- shell stitching,
- solid closure/materialization claims.

The result is a **partial planar family emission milestone**.

## SEM-A0 alignment

The implementation keeps semantic provenance/readiness guardrails in place:

- candidate provenance remains dry-run sourced,
- emission is bounded to supported readiness-qualified candidates,
- unsupported/deferred paths are explicitly diagnostic.

## Next milestone

Implement cylindrical retained side-surface emission and safe shell assembly planning so planar and cylindrical retained families can be combined into a topology-complete materialization path.
