# CIR-RECOVERY-V15 — bounded ChamferedEntryHoleVariant recognition + execution + STEP smoke

## Supported bounded CIR shape

`Subtract(Subtract(Box, HoleCylinder), EntryChamferCone)` with translation-only wrappers.

## Chamfer vs countersink distinction

Deterministic admissibility boundary in `HoleRecoveryPolicy`:
- Chamfer admits only when `coneDepth / holeRadius <= 1.0` and `(entryRadius - holeRadius) <= 0.75 * holeRadius`.
- Countersink rejects these chamfer-sized cones.
- Chamfer rejects deeper/larger entry cones as countersink-like.

This prevents cross-steal by admissibility (not scoring).

## Variant behavior

`ChamferedEntryHoleVariant` requires:
- nested subtract shape,
- rectangular host + cylindrical base hole,
- cone entry tool,
- cone/cylinder XY coaxial,
- top-entry face touch (bounded v15),
- valid radius ordering (entry radius > transition radius),
- transition radius compatible with cylinder radius,
- strict host XY clearance.

Rejections are explicit with stable reason codes.

## HoleRecoveryPlan shape

Admitted plans produce:
- `HoleKind = ChamferedEntry`
- `DepthKind = ThroughWithEntryRelief | BlindWithEntryRelief`
- entry feature `Chamfer`
- profile stack `[Conical(entry->transition), Cylindrical(base)]`
- expected patch includes `ChamferedEntryWall`.

## Execution route

`HoleRecoveryExecutor` supports chamfered-entry via existing bounded cone path:
1. create host box
2. subtract base cylinder
3. create conical relief
4. subtract conical relief

No STEP exporter behavior changes.

## STEP smoke expectations

For canonical chamfered-entry outputs:
- contains `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `CONICAL_SURFACE`, `CYLINDRICAL_SURFACE`
- does not contain `BREP_WITH_VOIDS`.

## Non-goals

No generic chamfer recovery, no threaded hole semantics, no generic profile-stack executor, no API/CLI expansion.

## Next milestone suggestion

Add safe bottom-entry chamfer support using the same bounded thresholds and explicit entry-side diagnostics.
