# CIR-RECOVERY-V4.1: FirmamentStepExporter regression for semantic recovery rematerialization

## Purpose

Prove end-to-end that existing `FirmamentStepExporter` CirOnly export flow now benefits from V4 semantic rematerialization wiring for canonical `Subtract(Box,Cylinder)` without changing STEP exporter behavior.

## Export path used

This regression exercises the existing path:

`FirmamentStepExporter` -> `NativeGeometryRematerializer.TryRematerialize` -> `FrepSemanticRecoveryRematerializer` -> `ThroughHoleRecoveryPolicy` -> `ThroughHoleRecoveryExecutor` -> recovered BRep -> existing `Step242Exporter.ExportBody`.

No special export path was added.

## Test coverage

Added/updated exporter regression tests:

- `FirmamentStepExporter_CirOnlyBoxCylinder_ExportsViaSemanticRecovery`
- `FirmamentStepExporter_CirOnlyTranslatedBoxCylinder_ExportsViaSemanticRecovery`
- `FirmamentStepExporter_CirOnlyUnsupportedBoxSphere_FailsClearly`

Assertions include STEP markers for successful paths:

- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `CYLINDRICAL_SURFACE`

## Semantic recovery diagnostics visibility

`FirmamentStepExporter.Export(...)` result does not expose rematerializer semantic diagnostic stream directly.

Therefore V4.1 verifies semantic path usage by asserting rematerialized state transition events contain semantic recovery transition messaging (`semantic recovery policy`).

For unsupported CirOnly cases, V4.1 verifies rematerializer failure diagnostics include semantic attempt summary (`selectedPolicy='none'`) and exporter retains existing clear CirOnly-unavailable failure behavior.

## Unsupported behavior

Unsupported CirOnly `Subtract(Box,Sphere)` remains unsupported:

- semantic recovery does not produce false success,
- rematerialization fails clearly,
- exporter returns existing CirOnly unavailable diagnostic behavior,
- no STEP payload is produced.

## Non-goals preserved

- no `Step242Exporter` behavior change,
- no special-case exporter route,
- no new recovery policy,
- no boolean behavior changes,
- no public API/CLI changes,
- no topology naming expansion (SEM-A0 guardrails preserved).

## Next milestone recommendation (CIR-RECOVERY-V5)

Add bounded diagnostic surfacing from rematerializer semantic decisions into exporter-visible diagnostics/metadata (without changing exporter semantics), so semantic path evidence is observable without indirect state assertions.
