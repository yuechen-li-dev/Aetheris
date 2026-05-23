# CIR-SWEEP-V2: Cylindrical hole-family → ProfileStackExtrude executor migration

## Scope

This milestone migrates cylindrical hole-family execution routing from ad hoc repeated 3D subtract paths to the ProfileStackExtrude executor backend.

Migrated in V2:
- Through-hole
- Blind-hole
- Counterbore
- Stepped-hole

Not migrated in V2:
- Countersink
- Chamfered-entry

## Adapter shape

`ProfileStackExtrudePlanAdapter.TryFromHoleRecoveryPlan(...)` now:
- validates explicit placement metadata,
- enforces rectangular box + Z-axis bounded scope,
- accepts only cylindrical segments,
- rejects/defer conical profiles with explicit diagnostics,
- maps ordered profile segments into contiguous `ProfileStackLayer` entries.

## Layer mapping

- Through-hole: single cylindrical through layer.
- Blind-hole: single cylindrical blind layer from explicit segment z-span.
- Counterbore: two cylindrical layers ordered by z-span (entry-relief + through core).
- Stepped-hole: bounded 3-tier cylindrical stack from explicit placement tiers.

## Diagnostics

Execution emits explicit diagnostics for:
- profile-stack adapter selection,
- variant kind,
- per-layer z/radius,
- cylindrical acceptance,
- conical deferral to conical route,
- profile-stack composition invocation/success/failure,
- no 3D subtract route usage for migrated variants.

## STEP smoke

STEP behavior remains unchanged at exporter boundary; migrated variants continue exporting manifold solid BRep in smoke lanes.

## Fallback boundaries

Conical variants (`Countersink`, `ChamferedEntry`) intentionally remain on existing conical primitive/Boolean execution lanes in V2.

## V3 recommendation

CIR-SWEEP-V3 can evaluate conical profile-stack/revolve/sweep route candidates for countersink/chamfered-entry without changing public APIs.
